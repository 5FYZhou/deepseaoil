# Data 层实现

> 本文件是 `Data 层设计.md` 的可编译落地。**按文件组织**（草案 `Data 层核心代码_1~6.md` 是按批次切分的，已合并重排）。
> 设计依据与边界理由见 `Data 层设计.md`；契约与总体分层见 `框架蓝图.md`。
>
> 与草案的差异逐条列在 §3，便于回溯。

---

## 1. 文件清单与依赖顺序

```
Assets/Scripts/Framework/Data/
├── ConfigModule/
│   ├── ConfigModule.cs          对外入口（含类型化查询转发）
│   ├── TablesHolder.cs          持有 cfg.Tables + JSON Loader
│   └── StartupValidator.cs      启动抽样校验
├── AssetModule/
│   ├── AssetModule.cs           对外入口（含 D1 重复入队保护）
│   ├── AssetRegistry.cs         Key → 路径转换 + 类型匹配
│   ├── CacheStore.cs            缓存表
│   ├── CacheEntry.cs            缓存条目
│   ├── RefCounter.cs            引用计数
│   ├── LoadScheduler.cs         并发控制 + 队列 + 重试
│   ├── LifecycleMgr.cs          冷却期 + LRU 淘汰 + D2 回收
│   ├── FailureHandler.cs        降级资源注册 + 失败记录
│   └── AsyncHandle.cs           异步句柄
└── DataMetrics/
    ├── DataMetrics.cs           可观测性表面（拉模型）
    └── DataSnapshot.cs          只读快照

Assets/Scripts/Game/
└── TablesMeta.cs                手写表清单（非生成物，扩展 cfg.Tables 的元信息）
```

**共 14 个文件**（`Data/` 下 14 个）＋ `Assets/Scripts/Game/TablesMeta.cs` 1 个，**合计 15 个 `.cs`**。
依赖顺序（也是建议的录入顺序）：`AsyncHandle` → `CacheEntry` → `CacheStore` → `RefCounter` → `AssetRegistry` → `LoadScheduler` → `LifecycleMgr` → `FailureHandler` → `AssetModule` → `ConfigModule` → `TablesMeta` → `DataSnapshot` → `DataMetrics`。

**命名空间**：全部 `DeepseaOil.Data`（`TablesMeta` 为 `DeepseaOil.Config`）。
**程序集**：全部落默认程序集 `Assembly-CSharp`（不需要新 asmdef，理由见蓝图 §8）。
**跨文件可见性**：微块用 `internal`，`DataMetrics` 通过 `AssetModule` 的 `internal` 属性访问——同程序集内可见，无额外机制。

---

## 2. 代码

### 2.1 `AsyncHandle.cs`

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 异步资源句柄。由 AssetModule.LoadAsync&lt;T&gt; 创建并返回。
    /// 调用时机：调用方 await 它拿资源本体。
    /// 边界：
    ///   - 必须是 class，不能是 struct（跨帧持有）
    ///   - Complete 只能在主线程调用；续体在主线程内联执行
    ///   - 故意不传 TaskCreationOptions.RunContinuationsAsynchronously：
    ///     那个选项会让续体走线程池，await 之后调用 Unity API 会崩。
    ///     Unity 的 Task 续体默认 posted 到 UnitySynchronizationContext（主线程）。
    ///   - 用 TrySetResult 而非 SetResult：重复完成不抛异常，只记警告
    ///     （资源降级路径不该因为框架内部时序问题炸掉调用方）
    /// </summary>
    public sealed class AsyncHandle<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();

        public bool IsDone { get; private set; }
        public T Asset { get; private set; }

        /// <summary>await 支持。调用方：var asset = await handle;</summary>
        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        /// <summary>
        /// 完成句柄。调用方：AssetModule（加载完成 / 降级两条路径）。
        /// 边界：只允许 AssetModule 内部调用，调用方无法伪造完成。
        /// </summary>
        internal bool Complete(T asset)
        {
            if (IsDone)
            {
                Debug.LogWarning($"[Asset] AsyncHandle<{typeof(T).Name}> completed twice, ignored");
                return false;
            }

            Asset = asset;
            IsDone = true;
            _tcs.TrySetResult(asset);
            return true;
        }

        /// <summary>创建未完成的句柄。调用方：LoadAsync 未命中缓存时。</summary>
        internal static AsyncHandle<T> Create() => new AsyncHandle<T>();

        /// <summary>
        /// 创建已完成的句柄。调用方：LoadAsync 命中缓存时。
        /// 代价：命中路径也会分配一个句柄对象（一次 GC）。
        ///       换来调用方代码路径统一（命中与否都是 await，无分支）。
        /// </summary>
        internal static AsyncHandle<T> Completed(T asset)
        {
            var h = new AsyncHandle<T>();
            h.Complete(asset);
            return h;
        }
    }
}
```

### 2.2 `CacheEntry.cs`

```csharp
namespace DeepseaOil.Data
{
    /// <summary>
    /// 缓存条目。AssetModule 内部数据结构，不对外暴露。
    /// 生命周期：CacheStore 创建 → RefCounter 改 refCount → LifecycleMgr 改 canEvict → 淘汰时移除。
    /// </summary>
    internal sealed class CacheEntry
    {
        /// <summary>资源本体。由 LoadScheduler 加载完成后写入。</summary>
        public UnityEngine.Object asset;

        /// <summary>调用方持有数。由 RefCounter 维护，不为负。</summary>
        public int refCount;

        /// <summary>最后访问时间（Time.realtimeSinceStartup）。LoadAsync 命中 / TryGet 时更新。</summary>
        public float lastAccessTime;

        /// <summary>预加载标记。true 时 Retain/Release 是 no-op，LifecycleMgr 永不淘汰。</summary>
        public bool isPreloaded;

        /// <summary>冷却期结束时间。refCount 归零时设为 now + COOLDOWN_SECONDS。</summary>
        public float cooldownUntil;

        /// <summary>是否可淘汰。冷却期结束后由 LifecycleMgr 置 true；LRU 只淘汰 canEvict=true 的条目。</summary>
        public bool canEvict;
    }
}
```

### 2.3 `CacheStore.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 缓存表。持有所有已加载资源条目。
    /// 边界：
    ///   - 主线程独占，无锁
    ///   - Key 为字符串，大小写敏感
    /// </summary>
    internal sealed class CacheStore
    {
        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>();

        public int Count => _entries.Count;

        /// <summary>尝试取条目（不改变引用计数）。调用方：RefCounter / LifecycleMgr / AssetModule。</summary>
        public bool TryGetEntry(string key, out CacheEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>
        /// 尝试取类型匹配的条目。调用方：AssetModule.LoadAsync / TryGet。
        /// 边界：Key 存在但类型不匹配返回 false（不抛异常）。
        /// </summary>
        public bool TryGet<T>(string key, out CacheEntry entry) where T : UnityEngine.Object
        {
            if (_entries.TryGetValue(key, out entry) && entry.asset is T)
                return true;

            entry = null;
            return false;
        }

        /// <summary>
        /// 写入条目（已存在则覆盖）。
        /// 调用方：AssetModule 的 onDone 回调。
        /// 边界：调用方需保证不覆盖 refCount &gt; 0 的条目——D1 的 pending 合并已经保证同一 Key 不会并发加载两次。
        /// </summary>
        public void Put(string key, UnityEngine.Object asset, bool isPreloaded)
        {
            _entries[key] = new CacheEntry
            {
                asset = asset,
                refCount = 0,           // Retain 由调用方显式做
                lastAccessTime = Time.realtimeSinceStartup,
                isPreloaded = isPreloaded,
                cooldownUntil = 0f,
                canEvict = false,
            };
        }

        /// <summary>更新访问时间。调用方：LoadAsync 命中 / TryGet 命中。</summary>
        public void Touch(string key)
        {
            if (_entries.TryGetValue(key, out var e))
                e.lastAccessTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 移除条目。调用方：LifecycleMgr 淘汰时。
        /// 边界：调用方保证 refCount == 0 &amp;&amp; canEvict == true &amp;&amp; !isPreloaded。
        /// </summary>
        public void Remove(string key) => _entries.Remove(key);

        /// <summary>
        /// 遍历所有条目。调用方：LifecycleMgr.Tick。
        /// 边界：返回内部集合本身（避免每帧临时数组），**遍历中不得增删**。
        /// </summary>
        public IEnumerable<KeyValuePair<string, CacheEntry>> AllEntries => _entries;

        /// <summary>全部清空。调用方：AssetModule.Dispose。</summary>
        public void Clear() => _entries.Clear();
    }
}
```

### 2.4 `RefCounter.cs`

```csharp
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 引用计数。操作 CacheStore 中条目的 refCount。
    /// 边界：
    ///   - 主线程独占
    ///   - refCount 不为负；Release 未知 Key 或重复 Release 返回 false，由调用方决定怎么记
    ///   - isPreloaded = true 的条目不受引用计数影响（常驻）
    /// </summary>
    internal sealed class RefCounter
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;

        public RefCounter(CacheStore cache, float cooldownSeconds)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// 引用计数 +1。调用方：AssetModule.LoadAsync 命中缓存时、加载完成时。
        /// 边界：Key 不存在返回 false；isPreloaded 条目直接返回 true（no-op）。
        /// </summary>
        public bool Retain(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            entry.refCount++;

            // 从 0 变 1：资源又被持有了，冷却期与可淘汰标记作废
            if (entry.refCount == 1)
            {
                entry.cooldownUntil = 0f;
                entry.canEvict = false;
            }

            return true;
        }

        /// <summary>
        /// 引用计数 −1。调用方：AssetModule.Release。
        /// 边界：Key 不存在 / isPreloaded / 已经是 0 都返回 false 或 no-op，不抛异常。
        ///       归零时设置冷却期，**不立即卸载**。
        /// </summary>
        public bool Release(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            if (entry.refCount == 0)
                return false;   // 调用方多调了一次 Release

            entry.refCount--;

            if (entry.refCount == 0)
            {
                entry.cooldownUntil = Time.realtimeSinceStartup + _cooldownSeconds;
                // canEvict 仍为 false，等 LifecycleMgr 在冷却期结束（或切场景）时置 true
            }

            return true;
        }
    }
}
```

### 2.5 `AssetRegistry.cs`

```csharp
using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 资源 Key 解析与类型匹配。**本类是 Data 层唯一的"路径语义转换点"**。
    /// Key 契约（详见 Data 层设计.md §9）：
    ///   配置表里存的是「相对 Assets/、带扩展名」的路径（Luban #path=unity 在导表期校验它真实存在）；
    ///   Resources.LoadAsync 需要「相对 Assets/Resources/、不带扩展名」的路径。
    ///   本类负责这两者之间的转换。
    /// 未来：量产切 Addressables 时，ResolvePath 返回 Address 地址，其余代码不动。
    /// </summary>
    internal sealed class AssetRegistry
    {
        private const string ResourcesRoot = "Assets/Resources/";

        /// <summary>
        /// 把 Key 转成 Resources.LoadAsync 可用的路径。
        /// 例："Assets/Resources/Icons/weapon.png" → "Icons/weapon"
        ///     "Icons/weapon.png"                 → "Icons/weapon"
        ///     "Icons/weapon"                     → "Icons/weapon"
        /// 边界：只做前缀与扩展名处理，不检查资源是否存在（那是加载的事）。
        /// </summary>
        public string ResolvePath(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var path = key.Replace('\\', '/');

            // 去掉 "Assets/Resources/" 前缀（大小写不敏感，避免策划手写出大小写差异）
            if (path.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(ResourcesRoot.Length);
            else if (path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Resources/".Length);

            path = path.TrimStart('/');

            // 去掉扩展名：Resources.Load 按「路径 + 类型」定位，不接受扩展名
            int dot = path.LastIndexOf('.');
            if (dot > 0)
                path = path.Substring(0, dot);

            return path;
        }

        /// <summary>
        /// 类型匹配检查。调用方：LoadScheduler 加载完成后。
        /// 边界：不做隐式转换；asset 为 null 或类型不匹配都返回 false。
        /// </summary>
        public bool IsTypeMatch(Type expected, UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            return expected.IsAssignableFrom(asset.GetType());
        }
    }
}
```

### 2.6 `LoadScheduler.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 加载调度器。并发控制 + 等待队列 + 失败重试。
    /// 调用时机：Enqueue 由 AssetModule.LoadAsync 未命中时调用；Tick 由 GameRoot 每帧驱动。
    /// 边界：
    ///   - 主线程独占；用普通 int 计数（不用 SemaphoreSlim：Wait() 会阻塞主线程，且主线程独占时信号量多余）
    ///   - 重试是「立即重新入队」，不延时（详细见 Data 层设计.md §4.3-2.4 的裁定）
    ///   - 失败策略不在这里：重试耗尽后交回 AssetModule 决定降级
    /// </summary>
    internal sealed class LoadScheduler
    {
        private const int MAX_RETRY = 2;

        private readonly AssetRegistry _registry;
        private readonly int _maxConcurrent;
        private readonly Queue<LoadRequest> _queue = new Queue<LoadRequest>();

        private int _loadingCount;
        private int _completedCount;
        private int _failedCount;

        // 供 DataMetrics 读取
        public int LoadingCount => _loadingCount;
        public int QueuedCount => _queue.Count;
        public int CompletedCount => _completedCount;
        public int FailedCount => _failedCount;

        public LoadScheduler(AssetRegistry registry, int maxConcurrent)
        {
            _registry = registry;
            _maxConcurrent = maxConcurrent;
        }

        /// <summary>入队。调用方：AssetModule.LoadAsync 未命中缓存、AssetModule.Preload。</summary>
        public void Enqueue(LoadRequest req) => _queue.Enqueue(req);

        /// <summary>
        /// 每帧推进。调用方：GameRoot 顺序表 step ③。
        /// 边界：单帧最多启动到并发上限为止，不在一帧内爆发。
        /// </summary>
        public void Tick(float dt)
        {
            while (_queue.Count > 0 && _loadingCount < _maxConcurrent)
                StartLoad(_queue.Dequeue());
        }

        private void StartLoad(LoadRequest req)
        {
            _loadingCount++;

            string path = _registry.ResolvePath(req.key);

            // 版本留口：切 Addressables 时此处换成 Addressables.LoadAssetAsync
            var request = Resources.LoadAsync(path, req.type);
            request.completed += _ => OnLoadCompleted(req, request);
        }

        private void OnLoadCompleted(LoadRequest req, ResourceRequest request)
        {
            _loadingCount--;

            var asset = request.asset;

            if (asset == null)
            {
                HandleFailure(req, "asset not found: " + _registry.ResolvePath(req.key));
                return;
            }

            if (!_registry.IsTypeMatch(req.type, asset))
            {
                HandleFailure(req, $"type mismatch: expected {req.type.Name}, got {asset.GetType().Name}");
                return;
            }

            _completedCount++;
            req.onDone?.Invoke(asset);
        }

        private void HandleFailure(LoadRequest req, string reason)
        {
            req.retryCount++;

            if (req.retryCount <= MAX_RETRY)
            {
                // 立即重试（排在队尾）。若将来要精确延时，加 nextRetryTime 字段并在 Tick 中过滤。
                _queue.Enqueue(req);
                return;
            }

            _failedCount++;
            req.onFail?.Invoke(reason);
        }

        /// <summary>清空队列。调用方：AssetModule.Dispose。边界：已启动的请求无法取消（D3 决策）。</summary>
        public void Clear() => _queue.Clear();
    }

    /// <summary>加载请求。LoadScheduler 内部数据结构。</summary>
    internal sealed class LoadRequest
    {
        public string key;
        public Type type;
        public Action<UnityEngine.Object> onDone;
        public Action<string> onFail;
        public int retryCount;
    }
}
```

### 2.7 `LifecycleMgr.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 生命周期管理。冷却期标记 + LRU 淘汰 + 淘汰后的资源回收。
    /// 调用时机：Tick 由 GameRoot 每帧驱动；OnSceneSwitch 由 SceneService 切场景前调用。
    /// 边界：
    ///   - 主线程独占
    ///   - isPreloaded = true 的条目永不淘汰
    ///   - 单帧最多淘汰 _maxEvictPerTick 条，避免卡帧
    ///   - 不修改 refCount；不删除 refCount &gt; 0 的条目
    /// </summary>
    internal sealed class LifecycleMgr
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;
        private readonly int _maxEntries;
        private readonly int _maxEvictPerTick;
        private int _evictedCount;

        public int EvictedCount => _evictedCount;

        public LifecycleMgr(CacheStore cache, float cooldownSeconds, int maxEntries, int maxEvictPerTick)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
            _maxEntries = maxEntries;
            _maxEvictPerTick = maxEvictPerTick;
        }

        /// <summary>
        /// 每帧推进。两阶段：先标记冷却期到期，再按需 LRU 淘汰。
        /// 分离理由：标记是每帧全遍历（O(N)），淘汰只在超阈值时发生。
        /// </summary>
        public void Tick(float dt)
        {
            float now = Time.realtimeSinceStartup;

            // 阶段 1：冷却期到期 → 标记 canEvict（只标记，不删除）
            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.refCount > 0) continue;
                if (entry.isPreloaded) continue;
                if (entry.canEvict) continue;
                if (now < entry.cooldownUntil) continue;

                entry.canEvict = true;
            }

            // 阶段 2：超阈值 → 淘汰最久未访问的
            int overflow = _cache.Count - _maxEntries;
            if (overflow > 0)
                EvictLRU(overflow);
        }

        /// <summary>
        /// 按 LRU 淘汰。调用方：Tick 阶段 2。
        /// 边界：只淘汰 canEvict && !isPreloaded；单次最多 _maxEvictPerTick 个；
        ///       先收集候选再删除（不能在遍历 CacheStore 时直接 Remove）。
        /// </summary>
        private void EvictLRU(int count)
        {
            List<KeyValuePair<string, CacheEntry>> candidates = null;

            foreach (var kv in _cache.AllEntries)
            {
                if (!kv.Value.canEvict) continue;
                if (kv.Value.isPreloaded) continue;

                (candidates ?? (candidates = new List<KeyValuePair<string, CacheEntry>>())).Add(kv);
            }

            if (candidates == null || candidates.Count == 0)
                return;

            candidates.Sort((a, b) => a.Value.lastAccessTime.CompareTo(b.Value.lastAccessTime));

            int toEvict = Math.Min(Math.Min(count, candidates.Count), _maxEvictPerTick);

            for (int i = 0; i < toEvict; i++)
            {
                _cache.Remove(candidates[i].Key);
                _evictedCount++;
            }

            if (toEvict > 0)
            {
                // D2：解除了我们的引用之后，让 Unity 回收成为「未引用资源」的那部分。
                // 不 yield 等它完成（协程会打破「GameRoot 唯一驱动」）；
                // 靠「单帧最多淘汰 8 条 + 只在真的淘汰后触发」限流。
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// 切场景时调用。调用方：AssetModule.OnSceneSwitch。
        /// 语义：上一场景的缓存不再享受冷却期保护，可被立即淘汰。
        /// 边界：isPreloaded 不动；refCount &gt; 0 不动（不强制释放）；本方法不删任何条目。
        /// </summary>
        public void OnSceneSwitch()
        {
            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.isPreloaded) continue;
                if (entry.refCount > 0) continue;

                entry.cooldownUntil = 0f;
                entry.canEvict = true;
            }
        }
    }
}
```

### 2.8 `FailureHandler.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 失败处理。降级资源注册 + 失败记录。
    /// 调用时机：RegisterFallback 由业务代码启动时调用；GetFallback / RecordFailure 由 AssetModule 的 onFail 调用。
    /// 边界：
    ///   - **不参与重试**（重试在 LoadScheduler.HandleFailure）
    ///   - 不抛异常，只记录
    ///   - 不创建 Unity 资源（降级资源由业务代码注册）
    /// </summary>
    internal sealed class FailureHandler
    {
        private const int MAX_RECENT_RECORDS = 32;

        private readonly Dictionary<Type, UnityEngine.Object> _fallbacks =
            new Dictionary<Type, UnityEngine.Object>();
        private readonly List<FailureRecord> _recentFailures = new List<FailureRecord>();
        private int _failedCount;

        public int FailedCount => _failedCount;
        public IReadOnlyList<FailureRecord> RecentFailures => _recentFailures;

        /// <summary>
        /// 注册降级资源。调用方：业务代码（如 Bootstrap 阶段）。
        /// 边界：同类型覆盖；不允许 null（记警告后忽略）。
        /// </summary>
        public void RegisterFallback<T>(T fallback) where T : UnityEngine.Object
        {
            if (fallback == null)
            {
                Debug.LogWarning($"[Asset] RegisterFallback<{typeof(T).Name}> ignored: null");
                return;
            }

            _fallbacks[typeof(T)] = fallback;
        }

        /// <summary>
        /// 取降级资源。调用方：AssetModule 的 onFail 回调。
        /// 边界：未注册返回 null；调用方需处理 null（例如完全放弃显示）。
        /// </summary>
        public T GetFallback<T>() where T : UnityEngine.Object
        {
            return _fallbacks.TryGetValue(typeof(T), out var fb) ? fb as T : null;
        }

        /// <summary>
        /// 记录一次最终失败（重试已用尽）。调用方：AssetModule 的 onFail 回调。
        /// 边界：只保留最近 MAX_RECENT_RECORDS 条，避免长跑游戏内存无限增长。
        /// </summary>
        public void RecordFailure(string key, string reason)
        {
            _failedCount++;
            _recentFailures.Add(new FailureRecord
            {
                key = key,
                reason = reason,
                time = Time.realtimeSinceStartup,
            });

            if (_recentFailures.Count > MAX_RECENT_RECORDS)
                _recentFailures.RemoveAt(0);

            Debug.LogError($"[Asset] load failed: {key} | {reason}");
        }
    }

    /// <summary>失败记录。供 DataMetrics / DebugOverlay 展示最近失败。</summary>
    internal struct FailureRecord
    {
        public string key;
        public string reason;
        public float time;
    }
}
```

### 2.9 `AssetModule.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 资源模块。Data 层的资源查询与生命周期管理入口。
    /// 调用时机：
    ///   Init          — GameRoot.Awake，紧随 ConfigModule.Init 之后
    ///   Tick          — GameRoot 顺序表 step ③，每帧
    ///   OnSceneSwitch — SceneService.PrepareForSceneSwitch
    ///   Dispose       — GameRoot.OnDestroy
    ///   LoadAsync / TryGet / Release / Preload / RegisterFallback — 调用方任意时机
    /// 边界：
    ///   - 所有方法仅主线程调用（Unity 资源 API 限制）
    ///   - 不发布/不订阅任何事件（观测走 DataMetrics 拉模型）
    ///   - 不感知调用方身份与业务语义；不知道 Luban 存在
    ///   - Init 失败（重复调用）直接抛异常：装配错误应当在启动时暴露
    /// </summary>
    public static class AssetModule
    {
        // ── 常量：改动集中在此 ──
        private const float COOLDOWN_SECONDS    = 60f;
        private const int   MAX_CACHE_ENTRIES   = 100;
        private const int   MAX_CONCURRENT_LOAD = 4;
        private const int   MAX_EVICT_PER_TICK  = 8;

        // ── 微块实例 ──
        private static AssetRegistry  _registry;
        private static CacheStore     _cache;
        private static RefCounter     _refCounter;
        private static LoadScheduler  _scheduler;
        private static LifecycleMgr   _lifecycle;
        private static FailureHandler _failure;

        // ── D1：同一 Key 并发请求合并 ──
        private static readonly Dictionary<string, List<Action<UnityEngine.Object>>> _pendingLoads =
            new Dictionary<string, List<Action<UnityEngine.Object>>>();

        // ── 统计（仅计数，不参与业务逻辑）──
        private static int _cacheHits;
        private static int _cacheMisses;

        private static bool _initialized;

        // 供 DataMetrics 只读访问（同程序集内可见）
        internal static bool IsInitialized => _initialized;
        internal static CacheStore Cache => _cache;
        internal static LoadScheduler Scheduler => _scheduler;
        internal static LifecycleMgr Lifecycle => _lifecycle;
        internal static FailureHandler Failure => _failure;
        internal static int CacheHits => _cacheHits;
        internal static int CacheMisses => _cacheMisses;

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化。调用方：GameRoot.Awake，必须在 ConfigModule.Init 之后。
        /// 边界：重复调用抛异常；不创建任何 Unity 资源（降级资源由业务注册）。
        /// </summary>
        public static void Init()
        {
            if (_initialized)
                throw new InvalidOperationException("[Asset] AssetModule.Init called twice");

            _registry   = new AssetRegistry();
            _cache      = new CacheStore();
            _refCounter = new RefCounter(_cache, COOLDOWN_SECONDS);
            _scheduler  = new LoadScheduler(_registry, MAX_CONCURRENT_LOAD);
            _lifecycle  = new LifecycleMgr(_cache, COOLDOWN_SECONDS, MAX_CACHE_ENTRIES, MAX_EVICT_PER_TICK);
            _failure    = new FailureHandler();

            _pendingLoads.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            _initialized = true;
        }

        /// <summary>
        /// 每帧推进。调用方：GameRoot 顺序表 step ③。
        /// 边界：未 Init 时 no-op 不抛异常（防装配顺序出错时整帧炸掉）。
        ///       只驱动 Scheduler 与 Lifecycle；FailureHandler 不需要每帧推进（重试在 Scheduler 内）。
        /// </summary>
        public static void Tick(float dt)
        {
            if (!_initialized) return;

            _scheduler.Tick(dt);
            _lifecycle.Tick(dt);
        }

        /// <summary>
        /// 切场景时调用。调用方：SceneService.PrepareForSceneSwitch（必须在 LoadScene 之前）。
        /// 边界：不清挂起请求、不动合并列表（可能是新场景的预加载）；
        ///       不强制释放 refCount &gt; 0 的资源；保留 isPreloaded 条目。
        /// </summary>
        public static void OnSceneSwitch()
        {
            if (!_initialized) return;

            _lifecycle.OnSceneSwitch();
        }

        /// <summary>进程退出时调用。调用方：GameRoot.OnDestroy。</summary>
        public static void Dispose()
        {
            if (!_initialized) return;

            _scheduler.Clear();
            _cache.Clear();
            _pendingLoads.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            _initialized = false;
        }

        // ─────────────────────────────────────────────
        // 主路径：异步加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 异步加载资源。调用方：任何层（Logic 或 Presentation）。
        /// 边界：
        ///   - 命中缓存：refCount++，返回**已完成**句柄（await 不挂起）
        ///   - 未命中：入队并返回未完成句柄，由 Tick 推进；同一 Key 的并发请求合并为一个 IO
        ///   - 加载成功：Put 缓存 → Retain → Complete（顺序严格，见设计文档 §4.5）
        ///   - 失败：重试 2 次后返回降级资源（可能为 null）
        ///   - 调用方拿到句柄后必须成对调用 Release
        /// </summary>
        public static AsyncHandle<T> LoadAsync<T>(string key) where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] LoadAsync before Init");

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[Asset] LoadAsync with empty key");
                return AsyncHandle<T>.Completed(null);
            }

            // ── 命中缓存 ──
            if (_cache.TryGet<T>(key, out var entry))
            {
                _cacheHits++;
                _cache.Touch(key);
                _refCounter.Retain(key);
                return AsyncHandle<T>.Completed(entry.asset as T);
            }

            _cacheMisses++;

            // ── D1：同一 Key 已在加载中 → 合并，不重复入队 ──
            if (_pendingLoads.TryGetValue(key, out var waiters))
            {
                var merged = AsyncHandle<T>.Create();
                waiters.Add(asset => merged.Complete(asset as T));
                return merged;
            }

            // ── 未命中且无在途请求：入队 ──
            var handle = AsyncHandle<T>.Create();
            var list = new List<Action<UnityEngine.Object>> { asset => handle.Complete(asset as T) };
            _pendingLoads[key] = list;

            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(T),
                onDone = asset =>
                {
                    // 顺序严格：先写缓存 → 再 Retain → 最后 Complete
                    // 否则调用方 await 后立即 TryGet/Release 会撞上「句柄已 resolve 但缓存未写入」的窗口
                    _cache.Put(key, asset, isPreloaded: false);
                    _refCounter.Retain(key);
                    DispatchPending(key, asset);
                },
                onFail = reason =>
                {
                    // 重试已用尽：记录 + 降级。fallback 可能为 null，调用方需处理
                    _failure.RecordFailure(key, reason);
                    DispatchPending(key, _failure.GetFallback<T>());
                },
            });

            return handle;
        }

        // ─────────────────────────────────────────────
        // 观测路径：查缓存（不触发加载）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 尝试从缓存取资源。调用方：任何层，用于避免不必要的异步开销。
        /// 边界：不触发加载、不改变引用计数、命中时更新访问时间。
        /// </summary>
        public static bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object
        {
            asset = null;

            if (!_initialized || string.IsNullOrEmpty(key))
                return false;

            if (_cache.TryGet<T>(key, out var entry))
            {
                _cache.Touch(key);
                asset = entry.asset as T;
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        // 释放
        // ─────────────────────────────────────────────

        /// <summary>
        /// 释放一次引用。调用方：LoadAsync 的持有者。
        /// 边界：未知 Key / 重复释放记警告不抛异常；refCount 归零后进冷却期，不立即卸载。
        /// </summary>
        public static void Release(string key)
        {
            if (!_initialized || string.IsNullOrEmpty(key))
                return;

            if (!_refCounter.Release(key))
                Debug.LogWarning($"[Asset] Release unknown or over-released key: {key}");
        }

        // ─────────────────────────────────────────────
        // 预加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 预加载资源并标记为常驻。
        /// 调用方：Loading 阶段 / 启动阶段。
        /// 边界：
        ///   - 走独立轻量路径，不创建 AsyncHandle（没有调用方在 await）
        ///   - 完成后 isPreloaded = true，永不淘汰；不计入引用计数
        ///   - 用 typeof(UnityEngine.Object) 做类型，不约束具体类型
        ///     已知待验证项：Resources.LoadAsync 传基类时类型过滤是否生效（见设计文档 §10.1-1）
        /// </summary>
        public static void Preload(string key)
        {
            if (!_initialized || string.IsNullOrEmpty(key))
                return;

            // 已在缓存：直接升格为预加载
            if (_cache.TryGetEntry(key, out var existing))
            {
                existing.isPreloaded = true;
                existing.canEvict = false;
                existing.cooldownUntil = 0f;
                return;
            }

            // 在途：挂一个升格回调，不重复入队
            if (_pendingLoads.TryGetValue(key, out var waiters))
            {
                waiters.Add(asset =>
                {
                    if (_cache.TryGetEntry(key, out var e))
                    {
                        e.isPreloaded = true;
                        e.canEvict = false;
                    }
                });
                return;
            }

            var list = new List<Action<UnityEngine.Object>>
            {
                asset => _cache.Put(key, asset, isPreloaded: true)
            };
            _pendingLoads[key] = list;

            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(UnityEngine.Object),
                onDone = asset => DispatchPending(key, asset),
                onFail = reason =>
                {
                    _failure.RecordFailure(key, reason);
                    _pendingLoads.Remove(key);   // 预加载失败：丢弃合并列表
                },
            });
        }

        // ─────────────────────────────────────────────
        // 降级资源注册（转发给 FailureHandler）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注册降级资源。调用方：业务代码启动时。
        /// 边界：Data 层不创建资源，由业务传入（URP 下占位 Sprite 需 Sprite.Create，属于业务细节）。
        /// </summary>
        public static void RegisterFallback<T>(T fallback) where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] RegisterFallback before Init");

            _failure.RegisterFallback(fallback);
        }

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        /// <summary>把结果分发给同一 Key 的所有等待者，然后清掉合并列表。</summary>
        private static void DispatchPending(string key, UnityEngine.Object asset)
        {
            if (!_pendingLoads.TryGetValue(key, out var waiters))
                return;

            _pendingLoads.Remove(key);

            for (int i = 0; i < waiters.Count; i++)
                waiters[i]?.Invoke(asset);
        }
    }
}
```

> **注意**：`DispatchPending` 在 `onFail` 分支里**不做** `Retain`——失败时调用方拿到的是降级资源或 null，没有进入缓存的条目可持引用。因此拿到 fallback 的调用方**不应**对同一 Key 调 `Release`（会得到一条 "unknown or over-released key" 警告）。这是有意的：警告本身就是"你对一个未加载成功的 Key 做了释放"的信号。

### 2.10 `ConfigModule.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 数值配置模块。Data 层的数值查询入口。
    /// 调用时机：Init 由 GameRoot.Awake 调用一次；查询随时。
    /// 边界：
    ///   - 所有查询同步返回
    ///   - 运行时不接受任何写操作
    ///   - 不感知资源（资源引用是字符串 Key，交给 AssetModule）
    ///   - Init 失败抛异常阻止游戏启动：带病数据不进运行时
    /// </summary>
    public static class ConfigModule
    {
        private static TablesHolder _holder;
        private static bool _ready;

        public static bool IsReady => _ready;

        /// <summary>
        /// 初始化。调用方：GameRoot.Awake，**必须早于 AssetModule.Init**。
        /// 边界：重复调用抛异常；任何失败都包成 ConfigLoadException 抛出。
        /// </summary>
        public static void Init(string jsonRoot)
        {
            if (_ready)
                throw new InvalidOperationException("[Config] ConfigModule.Init called twice");

            if (string.IsNullOrEmpty(jsonRoot))
                throw new ConfigLoadException("[Config] jsonRoot is null or empty");

            if (!System.IO.Directory.Exists(jsonRoot))
                throw new ConfigLoadException($"[Config] jsonRoot not found: {jsonRoot}");

            try
            {
                _holder = new TablesHolder(jsonRoot);
            }
            catch (Exception e)
            {
                // Luban 生成代码在 JSON 结构不符时抛 SerializationException；
                // 文件缺失 / 为空在 TablesHolder 内抛 IOException 系。
                // 统一包成 ConfigLoadException，让 GameRoot 能区分「配置问题」与「代码问题」。
                throw new ConfigLoadException($"[Config] load failed: {e.Message}", e);
            }

            if (!StartupValidator.Validate(_holder))
            {
                _holder = null;
                throw new ConfigLoadException("[Config] startup validation failed");
            }

            _ready = true;
            Debug.Log($"[Config] initialized, tables loaded from: {jsonRoot}");
        }

        /// <summary>默认初始化：从 StreamingAssets/Luban 读取。调用方：GameRoot.Awake。</summary>
        public static void InitFromStreamingAssets()
        {
            Init(System.IO.Path.Combine(Application.streamingAssetsPath, "Luban"));
        }

        // ─────────────────────────────────────────────
        // 查询接口（薄转发给 Luban 的 TbXxx，不做二次封装）
        // 边界：id 不存在时 Luban 的 Get 会抛异常；ref 校验已在导表期由 --strict 保证
        // ─────────────────────────────────────────────

        public static cfg.demo.Weapon GetWeapon(int id)
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.Get(id);
        }

        public static cfg.demo.Item GetItem(int id)
        {
            EnsureReady();
            return _holder.Tables.TbItem.Get(id);
        }

        public static cfg.demo.Fish GetFish(int id)
        {
            EnsureReady();
            return _holder.Tables.TbFish.Get(id);
        }

        public static IReadOnlyList<cfg.demo.Weapon> GetAllWeapons()
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.DataList;
        }

        // ─────────────────────────────────────────────
        // 逃生舱：特殊情况直接访问原始 Tables
        // 边界：只读；调用方不得跨帧持有该引用
        // ─────────────────────────────────────────────

        public static cfg.Tables Tables
        {
            get
            {
                EnsureReady();
                return _holder.Tables;
            }
        }

        private static void EnsureReady()
        {
            if (!_ready)
                throw new InvalidOperationException("[Config] accessed before Init");
        }
    }

    /// <summary>
    /// 配置加载异常。独立定义，让 GameRoot 能区分「配置问题（重新导表）」与「代码问题」。
    /// </summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
```

### 2.11 `TablesHolder.cs`

```csharp
using System;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 持有 Luban 生成的 cfg.Tables 实例。
    /// 调用时机：ConfigModule.Init 时构造一次。
    /// 边界：
    ///   - 构造时同步加载全部 JSON（启动时一次性完成）
    ///   - Loader 严格校验：文件不存在 / 为空 / 解析失败都抛异常
    ///   - 平台：File.ReadAllText 只对桌面端（Windows / macOS / Linux）有效。
    ///     Android / WebGL 的 StreamingAssets 在 APK 包内，必须改用 UnityWebRequest
    ///     —— 那会让 Init 变异步，牵动整条启动链。Jam 期不支持，见 Data 层设计.md §11 A2。
    /// </summary>
    internal sealed class TablesHolder
    {
        public cfg.Tables Tables { get; }

        public TablesHolder(string jsonRoot)
        {
            // Luban 生成的 Tables 构造函数接收一个 Loader：表名 → JSONNode
            // （生成目标是 cs-simple-json，所以是 Func<string, JSONNode>，不是 Newtonsoft 的 JObject）
            Tables = new cfg.Tables(file => LoadJson(jsonRoot, file));
        }

        /// <summary>
        /// Luban 调用的 Loader。输入：文件名（不含扩展名，如 "demo_tbweapon"）；输出：解析后的 JSON。
        /// </summary>
        private static JSONNode LoadJson(string root, string file)
        {
            string path = Path.Combine(root, file + ".json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"[Config] JSON not found: {path}");

            string text = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"[Config] JSON is empty: {path}");

            return JSON.Parse(text);   // Luban.SimpleJSON.JSON
        }
    }
}
```

### 2.12 `StartupValidator.cs`

```csharp
using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 启动时抽样校验。
    /// 调用时机：ConfigModule.Init 中，TablesHolder 构造完成后。
    /// 边界：
    ///   - 只确认「数据能被加载进内存」，不重复 Luban 的 ref / path / range 校验（那些在导表期由 --strict 完成）
    ///   - 抽样而非全遍历：全表遍历会拖慢启动
    ///   - 不抛异常，失败通过返回值告知调用方
    /// </summary>
    internal static class StartupValidator
    {
        public static bool Validate(TablesHolder holder)
        {
            if (holder == null || holder.Tables == null)
            {
                Debug.LogError("[Config] Validate failed: Tables is null");
                return false;
            }

            var tables = holder.Tables;

            // 抽样访问每张已登记的表：触发其构造与索引建立
            // 关键表清单来自手写 TablesMeta（加表时同步维护，见 Data 层设计.md §3.6）
            if (DeepseaOil.Config.TablesMeta.Names.Length == 0)
            {
                Debug.LogWarning("[Config] TablesMeta.Names is empty: 跳过抽样校验");
                return true;
            }

            foreach (var name in DeepseaOil.Config.TablesMeta.Names)
            {
                var prop = tables.GetType().GetProperty(name);
                if (prop == null)
                {
                    Debug.LogError($"[Config] TablesMeta 里登记了不存在的表：{name}（生成物里没有这个属性）");
                    return false;
                }

                try
                {
                    _ = prop.GetValue(tables);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Config] Validate failed on table {name}: {e.Message}");
                    return false;
                }
            }

            return true;
        }
    }
}
```

### 2.13 `TablesMeta.cs`

```csharp
namespace DeepseaOil.Config
{
    /// <summary>
    /// 手写的表元信息清单。**非生成物**，放在 Assets/Scripts/Game/ 下
    /// （Assets/Scripts/Config/ 是生成物专用目录，导表时整目录镜像覆盖）。
    ///
    /// 为什么需要它：cfg.Tables 没有「表数量」之类的属性，生成物又不允许手改。
    /// 用它换来两件事：
    ///   1. DataMetrics.TableCount 有数可报
    ///   2. StartupValidator 的「关键表抽样」成为显式决策，而不是反射遍历全部
    /// 代价：加表后要同步加一行（见 ConfigWorkspace/AGENTS.md：新表必须登记进 __tables__.xlsx）。
    /// </summary>
    public static class TablesMeta
    {
        /// <summary>cfg.Tables 的属性名清单。加表时同步。</summary>
        public static readonly string[] Names =
        {
            "TbWeapon",
            "TbItem",
            "TbFish",
        };

        public static int Count => Names.Length;
    }
}
```

### 2.14 `DataSnapshot.cs`

```csharp
namespace DeepseaOil.Data
{
    /// <summary>
    /// Data 层只读快照。
    /// 调用时机：DebugOverlay 每 N 帧拉一次。
    /// 边界：
    ///   - struct 值语义；调用方拿到副本，改不了内部状态
    ///   - 所有字段为瞬时值，不保证跨帧一致
    ///   - **字段只增不删**（DebugOverlay 可能引用旧字段，删字段会静默断链）
    /// </summary>
    public struct DataSnapshot
    {
        // ─── ConfigModule ───
        /// <summary>ConfigModule 是否已 Init 成功</summary>
        public bool ConfigReady;
        /// <summary>已登记的表数（来自 TablesMeta.Count）</summary>
        public int TableCount;

        // ─── AssetModule：瞬时状态 ───
        /// <summary>缓存中条目总数（含 refCount 为 0 与 isPreloaded 的）</summary>
        public int CachedAssetCount;
        /// <summary>当前正在加载的数量</summary>
        public int LoadingCount;
        /// <summary>排队等待加载的数量</summary>
        public int QueuedCount;

        // ─── AssetModule：累计计数 ───
        /// <summary>累计加载完成数（进程启动至今）</summary>
        public int CompletedCount;
        /// <summary>累计最终失败数（重试已用尽，含降级）</summary>
        public int FailedCount;
        /// <summary>累计淘汰数</summary>
        public int EvictedCount;
        /// <summary>缓存命中次数</summary>
        public int CacheHits;
        /// <summary>缓存未命中次数</summary>
        public int CacheMisses;
        /// <summary>缓存命中率 [0, 1]；无访问时为 0</summary>
        public float CacheHitRate;
    }
}
```

### 2.15 `DataMetrics.cs`

```csharp
namespace DeepseaOil.Data
{
    /// <summary>
    /// Data 层可观测性表面。**拉模型**：不推送事件，不走 EventBus。
    /// 调用时机：DebugOverlay 每 N 帧拉一次。
    /// 边界：
    ///   - 只读，不修改任何 Module 状态
    ///   - 不持有资源引用
    ///   - 允许在未 Init 时调用（返回零值快照）
    ///   - 不分配（struct + 无 List），每帧调用开销可忽略
    /// </summary>
    public static class DataMetrics
    {
        public static DataSnapshot GetSnapshot()
        {
            var snap = new DataSnapshot();

            // ── ConfigModule ──
            snap.ConfigReady = ConfigModule.IsReady;
            snap.TableCount = ConfigModule.IsReady ? DeepseaOil.Config.TablesMeta.Count : 0;

            // ── AssetModule ──
            if (!AssetModule.IsInitialized)
                return snap;

            snap.CachedAssetCount = AssetModule.Cache.Count;
            snap.LoadingCount = AssetModule.Scheduler.LoadingCount;
            snap.QueuedCount = AssetModule.Scheduler.QueuedCount;
            snap.CompletedCount = AssetModule.Scheduler.CompletedCount;
            snap.FailedCount = AssetModule.Failure.FailedCount;
            snap.EvictedCount = AssetModule.Lifecycle.EvictedCount;
            snap.CacheHits = AssetModule.CacheHits;
            snap.CacheMisses = AssetModule.CacheMisses;

            int total = snap.CacheHits + snap.CacheMisses;
            snap.CacheHitRate = total > 0 ? (float)snap.CacheHits / total : 0f;

            return snap;
        }
    }
}
```

---

## 3. 与被否决草案的差异表（逐条可回溯）

被否决草案原样保存在提交 `b1692c2`，可用 `git show b1692c2:"Docs/框架设计/草案/<文件名>"` 取回。

| # | 文件 | 草案写法 | 本实现 | 原因 |
| :-- | :-- | :-- | :-- | :-- |
| 1 | 全部 | `namespace Jam.Data` | `namespace DeepseaOil.Data` | F2：与工程既有 `DeepseaOil.*` 前缀一致 |
| 2 | `TablesHolder` | `using Newtonsoft.Json.Linq;` + `JObject.Parse` | `using Luban.SimpleJSON;` + `JSON.Parse` | F1：生成目标是 `cs-simple-json`，构造签名是 `Func<string, JSONNode>` |
| 3 | `AssetRegistry.ResolvePath` | `return key;`（注释称 Key 已是 Resources 相对路径） | 去 `Assets/Resources/` 前缀 + 去扩展名 | F3：表里存的是相对 `Assets/` 带扩展名的路径 |
| 4 | `AssetRegistry` | 无 `ResourcesRoot` 常量，无大小写兼容 | 新增前缀处理与 `OrdinalIgnoreCase` | F3 + 容忍策划手写大小写差异 |
| 5 | `AsyncHandle` | `new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)` + `SetResult` | 默认构造 + `TrySetResult` | C3/C4：避免续体走线程池；重复完成不抛异常 |
| 6 | `LoadScheduler` | `SemaphoreSlim` + `semaphore.Wait()` | `int _loadingCount` + 上限判断 | C2：`Wait()` 阻塞主线程，且主线程独占时信号量多余 |
| 7 | `LoadScheduler` | 重试描述在详细设计与伪代码之间不一致 | 明确「立即重新入队，上限 2」 | C1 |
| 8 | `AssetModule.Tick` | 只驱动 Scheduler + Lifecycle（与详细设计文档矛盾） | 同上，并在注释与设计文档里**明确** FailureHandler 不需每帧推进 | C1 |
| 9 | `AssetModule` | 无 D1 重复入队保护 | 新增 `_pendingLoads` + `DispatchPending` | D1 |
| 10 | `AssetModule.Preload` | 未处理「已在途」的情况 | 挂升格回调，不重复入队 | D1 的一致性 |
| 11 | `LifecycleMgr.EvictLRU` | 无资源回收 | 淘汰后触发一次 `Resources.UnloadUnusedAssets()` | D2 |
| 12 | `LifecycleMgr.EvictLRU` | 无条件分配 `new List(...)(count)` | 有候选才分配（`candidates ??=`） | 每帧调用，避免无谓 GC |
| 13 | `ConfigModule` | `QueryAccessor` 单列一个 internal class | 合并进 `ConfigModule` | KISS：几行无状态转发，单独成类只增加跳转层级 |
| 14 | `ConfigModule.Init` | 直接 `new TablesHolder`，异常未包装 | `try/catch` 包成 `ConfigLoadException` | 让 GameRoot 能区分「配置问题」与「代码问题」 |
| 15 | `ConfigModule` | 查询 `cfg.weapon.Weapon` / `cfg.item.Item`（类名为占位） | `cfg.demo.Weapon` / `cfg.demo.Item` / `cfg.demo.Fish` | 按实际生成物（`Assets/Scripts/Config/demo/`） |
| 16 | `StartupValidator` | 硬编码 `holder.Tables.TbWeapon.DataList` | 遍历 `TablesMeta.Names` + 反射取属性 | F4：加表只需改一处清单 |
| 17 | `DataMetrics.CountTables` | 占位 `return 0` | `TablesMeta.Count` | F4 |
| 18 | `DataMetrics` | `GetSnapshot` 先取 Asset 再取 Config | 先 Config（未 Init 也能报），Asset 未 Init 时提前返回 | 与「未 Init 也安全」的声明一致 |
| 19 | 文件组织 | 按批次 1~6 切分（`核心代码_1~6.md`） | 按文件组织，单份文档 | 批次是交付节奏，不是代码结构 |
| 20 | 目录 | `Assets/Scripts/Data/` | `Assets/Scripts/Framework/Data/` | `Assets/Scripts/Config/` 是生成物专用目录。**校正**：并轨 FY 后 `Scripts/Framework/` 已存在且**不是空目录**（35 个 `.cs`，见蓝图 §15.1），所以这条理由要改读作"不靠近生成物目录"——落在 `Framework/` 下仍然正确，只是当初的旁注过时了 |

---

## 4. 落地步骤与验收

### 4.1 落地步骤与实际结果

| # | 步骤 | 验收标准 | 实际结果 |
| :-- | :-- | :-- | :-- |
| 1 | 建目录 `Assets/Scripts/Framework/Data/{ConfigModule,AssetModule,DataMetrics}/` ＋ 4 个目录 `.meta` | Unity 无编译错误 | ✅ 已建 |
| 2 | 按 §1 依赖顺序录入 14 个文件，另加 `Assets/Scripts/Game/TablesMeta.cs`，各带 `.meta` | `Assembly-CSharp` 编译通过，0 error | ✅ 15 个 `.cs` ＋ 19 个 `.meta` 已落盘（GUID 全仓库唯一） |
| 3 | 接进启动链：`GameRoot.Awake` 装 Data 层、`Update` 调 `AssetModule.Tick`、`OnDestroy` 调 `Dispose` | 编译通过 | ✅ 本次新增改动，见 §5.3 |
| 4 | 接第 ④ 项复位：`SceneService.Load` 在 `LoadScene` 前调 `AssetModule.OnSceneSwitch()` | 编译通过 | ✅ 注意 `SceneService.Load` 目前**没有调用点** |
| 5 | `ConfigLoader` 改为消费 `ConfigModule`，消除第二个 `cfg.Tables` | Console 打印武器 / 外键 / 表条数 / `DataMetrics` | ✅ 靠 `Awake` 先于 `Start` 保证 `IsReady` |
| 6 | **外部 dotnet harness**：真实 Luban 运行库 ＋ 真实 `cfg` 生成代码 ＋ 15 个新文件（`UnityEngine` 最小替身，`LangVersion 9.0`） | `dotnet build` 0 error 0 warning；`dotnet run` 全绿 | ✅ **54 项断言全过，0 失败** |
| 7 | 在 Unity 里跑 `Assets/Tests/Editor/Data层Tests.cs` 与 `Assets/Tests/EditMode/框架文档一致性Tests.cs` | 全绿 | ⏳ **未执行**——本次环境没有 Unity 编辑器。D5（EditMode 下 `Resources.LoadAsync` 回调）是唯一有实质不确定性的一条 |

**第 6 步覆盖到的语义**（都在真实 JSON / 真实生成类上跑）：

- 配置载入：`ConfigModule.IsReady`、`GetWeapon(1).Name == "木剑"`、`Pow == 10`、外键 `icon_item → Item` 已解析、
  `GetFish(1002)` 非空、`GetAllWeapons().Count == 3`、`TablesMeta.Count == 3`、逃生舱 `Tables` 可访问、重复 `Init` 抛异常。
- `AssetRegistry.ResolvePath` 七例：带前缀带扩展名 / 只带扩展名 / 无扩展名 / 前缀大小写变体 / 只写 `Resources/` /
  反斜杠归一化 / 非 Resources 路径（后者的失败链见 §7）。
- 命中路径：`LoadAsync<GameObject>("Assets/Resources/ui/Panel/BeginPanel.prefab")` → 传给 `Resources.LoadAsync` 的是
  `ui/Panel/BeginPanel` → 句柄 resolve → `TryGet` 命中。
- D1 重复入队保护：三次并发请求只产生 **1 次** `Resources.LoadAsync`，三个句柄拿到同一资源。
- 失败路径：重试共 3 次（首次 + 2 次重试）后句柄以 `null` 完成，不抛异常；注册 fallback 后返回 fallback。
- `AsyncHandle`：二次 `Complete` 被忽略且不抛；`Completed(null).IsDone == true`。
- `RefCounter` / `LifecycleMgr`：`Retain`/`Release` 计数、归零设 `now + 60s` 冷却期、重复 `Release` 返回 false、
  冷却期内不淘汰、到期后淘汰、超阈值淘汰最久未访问（LRU）、`isPreloaded` 永不淘汰。
- `DataMetrics`：`ConfigReady`/`TableCount == 3`/`CompletedCount == 2`（D 段 3 个句柄因合并只算 1 次完成）/`FailedCount`；
  `OnSceneSwitch` 不删条目；`Dispose` 后全清且可重复调用；未 Init 时 `Tick` 是 no-op。

> ⚠️ **诚实标注**：第 6 步是"真实 Luban ＋ 真实生成代码 ＋ 最小 `UnityEngine` 替身"在 Unity **之外**编译并运行的。
> 它证明了**编译**与**Data 层逻辑**；它**没有**证明 Unity 编辑器内的行为——`Resources.LoadAsync` 的真实回调时机、
> `Time.realtimeSinceStartup` 的真实值、`Task` 续体是否真的回到主线程、`Resources.UnloadUnusedAssets` 的实际开销。
> 这些必须靠第 7 步在 Unity 里补。

### 4.2 未完成项（不阻塞落地，已在设计文档登记）

| 项 | 归属 | 状态 |
| :-- | :-- | :-- |
| `Resources.LoadAsync` 传 `typeof(UnityEngine.Object)` 的类型过滤 | `Preload` | 待实测（设计文档 §10.1-1）；harness 只能证明路径转换，证明不了类型过滤 |
| EditMode 下 `Resources.LoadAsync` 的完成回调是否触发 | `Data层Tests.D5` | 待实测；不触发则降级为 PlayMode 测试 |
| 占位 Sprite 在 URP 下的构造方式 | 业务侧 `RegisterFallback` | 待实测（§10.1-4） |
| `UnloadUnusedAssets` 的帧尖峰量级 | `LifecycleMgr` | 待 Profiler（§10.1-5） |
| Android / WebGL 的 StreamingAssets 异步读取 | `TablesHolder` | 不支持，影响面见 §11 A2 |
| 延时重试 | `LoadScheduler` | 未采纳（§10.2 O7） |
| 取消未完成加载 | `LoadScheduler` | 未采纳（§7 D3） |
| `ConfigModule` 的重置入口 | `ConfigModule` | 缺失（§10.2 O10）——测试只能靠 Domain Reload |

---

## 5. 并轨上游 FY 之后的落地记录

### 5.1 实际写入的文件

**代码（15 个 `.cs`，每个都带同名 `.meta`）**

| 路径 | 说明 |
| :-- | :-- |
| `Assets/Scripts/Framework/Data/AssetModule/AsyncHandle.cs` | 句柄（`await` 支持） |
| `Assets/Scripts/Framework/Data/AssetModule/CacheEntry.cs` | 缓存条目 |
| `Assets/Scripts/Framework/Data/AssetModule/CacheStore.cs` | 缓存表 |
| `Assets/Scripts/Framework/Data/AssetModule/RefCounter.cs` | 引用计数 + 冷却期设置 |
| `Assets/Scripts/Framework/Data/AssetModule/AssetRegistry.cs` | **Key 唯一转换点** + 类型匹配 |
| `Assets/Scripts/Framework/Data/AssetModule/LoadScheduler.cs` | 并发/队列/重试 |
| `Assets/Scripts/Framework/Data/AssetModule/LifecycleMgr.cs` | 冷却期标记 + LRU + D2 回收 |
| `Assets/Scripts/Framework/Data/AssetModule/FailureHandler.cs` | 降级注册 + 失败记录 |
| `Assets/Scripts/Framework/Data/AssetModule/AssetModule.cs` | 对外入口（含 D1 合并） |
| `Assets/Scripts/Framework/Data/ConfigModule/ConfigModule.cs` | 数值入口 + `ConfigLoadException` |
| `Assets/Scripts/Framework/Data/ConfigModule/TablesHolder.cs` | 持有 `cfg.Tables` + JSON Loader |
| `Assets/Scripts/Framework/Data/ConfigModule/StartupValidator.cs` | 启动抽样校验 |
| `Assets/Scripts/Framework/Data/DataMetrics/DataSnapshot.cs` | 只读快照（struct） |
| `Assets/Scripts/Framework/Data/DataMetrics/DataMetrics.cs` | 拉模型入口 |
| `Assets/Scripts/Game/TablesMeta.cs` | 手写表清单（命名空间 `DeepseaOil.Config`） |

**`.meta`（19 个）**：4 个目录（`Data/` 与其三个子目录）＋ 15 个脚本。全部 LF、无 BOM、`folderAsset`/`MonoImporter` 格式与仓库既有 `.meta` 逐字节同构；
GUID 生成后与全仓库既有 168 个比对过唯一性。

### 5.2 与本文档 §2 代码的偏差

**零偏差**：§2 的 15 段代码逐字落地，没有任何"编译必须"的修改。
`dotnet build` 在 `LangVersion 9.0`（与 Unity 的 `Assembly-CSharp.csproj` 一致）下报 **0 error 0 warning**。

### 5.3 对上游逻辑层的最小接入改动（4 处）

| 文件 | 改动 | 理由 |
| :-- | :-- | :-- |
| `Assets/Scripts/Framework/GameRoot.cs` | `using DeepseaOil.Data;`；新增 `Awake()` → `ConfigModule.InitFromStreamingAssets(); AssetModule.Init();`；`Update()` 在 services 循环之后加 `AssetModule.Tick(Time.deltaTime)`；新增 `OnDestroy()` → `AssetModule.Dispose()` | 兑现 §3.1「Config 先于 Asset」与契约表的 step ② / 退出清理。**放 `Awake` 不放 `Start`**：Unity 只保证"所有 `Awake` 先于任何 `Start`"，`ConfigLoader.Start()` 才能确定性地拿到已就绪的配置 |
| `Assets/Scripts/Framework/Logic/Services/SceneService.cs` | 顶部 `using DeepseaOil.Data;`；`LoadScene` 之前插入第 ④ 步 `AssetModule.OnSceneSwitch();`，原"④ 换场景"顺移为 ⑤ | 兑现契约表「复位四项」。⚠️ 该方法目前无调用点 |
| `Assets/Scripts/Game/ConfigLoader.cs` | `Start()` 改为**消费方**：`if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();`，再经 `ConfigModule.GetWeapon`/`GetFish`/`Tables` 打印同样的日志，并补一行 `DataMetrics` 输出 | 消除第二个 `cfg.Tables`（原脚本自己 `new` 了一份，与 `ConfigModule` 各读一遍 JSON）；同时让它在 `TestConfig.unity`（没有 `GameRoot`）里也能独立跑通 |
| `Assets/Scripts/Framework/Logic/IMovementMotor.cs` | 注释里的 `Dasuus.Presentation` → `DeepseaOil.Presentation` | 上游注释写错了命名空间（同问题的 `EventBusDebugPanel.cs:10` 未改，登记在蓝图 §16 D22） |

**未改动**：`ResMgr` / `MonoMgr` / `UIMgr` / `BasePanel` / `SaveService` / `BeginPanel` / `PauseService` /
`Singleton.cs` / `BaseManager.cs` / `ConfigController.cs` / 任何场景与预设体。上游现存缺陷只登记不修，清单见蓝图 §16。
