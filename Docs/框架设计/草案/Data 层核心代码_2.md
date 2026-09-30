# Data 层核心代码 · 批次 2

## 批次 2：计数与调度

三个文件：`RefCounter` / `AssetRegistry` / `LoadScheduler`。

---

### 2.1 RefCounter

```csharp
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 引用计数管理。操作 CacheStore 中条目的 refCount 字段。
    /// 调用时机：AssetModule 内部调用。
    /// 边界：
    ///   - 主线程独占
    ///   - refCount 不为负；Release 未知 Key 返回 false，由调用方决定如何记录
    ///   - isPreloaded=true 的条目不受引用计数影响（常驻）
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
        /// 引用计数 +1。
        /// 调用方：AssetModule.LoadAsync 命中缓存时 / 加载完成时。
        /// 边界：Key 不存在返回 false；isPreloaded 条目 no-op 返回 true。
        /// </summary>
        public bool Retain(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            // isPreloaded 条目：不参与引用计数，永远常驻
            if (entry.isPreloaded)
                return true;

            entry.refCount++;

            // 从 0 变 1：清除冷却期与可淘汰标记（资源又被持有了）
            if (entry.refCount == 1)
            {
                entry.cooldownUntil = 0f;
                entry.canEvict = false;
            }

            return true;
        }

        /// <summary>
        /// 引用计数 -1。
        /// 调用方：AssetModule.Release。
        /// 边界：Key 不存在返回 false；归零时设置冷却期，不立即卸载。
        /// </summary>
        public bool Release(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            if (entry.refCount == 0)
            {
                // 调用方多调了一次 Release，或从没 Retain 过
                return false;
            }

            entry.refCount--;

            if (entry.refCount == 0)
            {
                // 归零 → 进入冷却期（不立即卸载）
                entry.cooldownUntil = Time.realtimeSinceStartup + _cooldownSeconds;
                // 注意：canEvict 仍为 false，等 LifecycleMgr 在冷却期结束后置 true
            }

            return true;
        }
    }
}
```

**三个设计点说明**：

1. **`isPreloaded` 条目的 Retain/Release 是 no-op**：预加载资源的生命周期与调用方无关，永远常驻。调用方对它调 Retain/Release 不会改变任何状态，返回 true 避免误报警告。
2. **refCount 从 0 变 1 时清除 `cooldownUntil` 和 `canEvict`**：这是冷却期机制的关键——资源又被持有时，冷却期失效，防止在有人持有时被淘汰。
3. **多调 Release 返回 false 而非抛异常**：调用方可能是防守性释放（不确定是否已释放），抛异常会破坏这类代码路径。记日志交给 AssetModule 处理。

---

### 2.2 AssetRegistry

```csharp
namespace Jam.Data
{
    /// <summary>
    /// 资源 Key 解析。把调用方传入的 Key 解析为底层加载系统所需的路径。
    /// 调用时机：LoadScheduler.StartLoad 前。
    /// 边界：
    ///   - Jam 阶段：Key 即 Resources 相对路径，无需转换
    ///   - 量产阶段：可切换为 SO 映射表 / Addressables 地址表
    ///   - 空壳但保留，作为未来切换的挂载点
    /// </summary>
    internal sealed class AssetRegistry
    {
        /// <summary>
        /// 把 Key 解析为 Resources.LoadAsync 可用的路径。
        /// Jam 阶段：直接返回 Key。
        /// 约定：Key 形如 "Icons/weapon_wood"，不带扩展名，相对于 Assets/Resources/。
        /// 版本差异：切 Addressables 后，返回 Addressables 地址。
        /// </summary>
        public string ResolvePath(string key)
        {
            return key;
        }

        /// <summary>
        /// 类型匹配检查。
        /// 调用方：LoadScheduler 加载完成后。
        /// 边界：不做隐式转换，类型不匹配直接失败。
        /// </summary>
        public bool IsTypeMatch(System.Type expected, UnityEngine.Object asset)
        {
            if (asset == null) return false;
            return expected.IsAssignableFrom(asset.GetType());
        }
    }
}
```

**设计点说明**：

1. **`ResolvePath` 是空壳**：Jam 阶段无转换。它的存在是为了让未来切换映射表时，改动点集中在这一个方法里。
2. **`IsTypeMatch` 放这里而非 LoadScheduler 里**：类型匹配策略可能随实现变化（比如未来允许接口类型），集中在一处便于调整。
3. **Key 的约定写在注释里**：`"Icons/weapon_wood"` 是 Resources 相对路径，不带扩展名。这个约定必须与 Luban 配置里的字段值、`pathValidator.rootDir` 保持一致。

---

### 2.3 LoadScheduler

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 加载调度器。控制并发数、管理等待队列、处理失败重试。
    /// 调用时机：
    ///   - Enqueue：AssetModule.LoadAsync 未命中缓存时
    ///   - Tick：GameRoot 每帧调用
    /// 边界：
    ///   - 主线程独占
    ///   - 使用 Interlocked 维护 loadingCount（虽然主线程独占，但为将来扩展留余地）
    /// </summary>
    internal sealed class LoadScheduler
    {
        private const int   MAX_RETRY    = 2;
        private const float RETRY_DELAY  = 0.5f;

        private readonly AssetRegistry _registry;
        private readonly int _maxConcurrent;
        private readonly Queue<LoadRequest> _queue = new();

        private int _loadingCount;
        private int _completedCount;
        private int _failedCount;

        // 供 DataMetrics 读取
        public int LoadingCount   => _loadingCount;
        public int QueuedCount    => _queue.Count;
        public int CompletedCount => _completedCount;
        public int FailedCount    => _failedCount;

        public LoadScheduler(AssetRegistry registry, int maxConcurrent)
        {
            _registry = registry;
            _maxConcurrent = maxConcurrent;
        }

        /// <summary>
        /// 入队。调用方：AssetModule.LoadAsync 未命中缓存时。
        /// </summary>
        public void Enqueue(LoadRequest req)
        {
            _queue.Enqueue(req);
        }

        /// <summary>
        /// 每帧推进。调用方：GameRoot 顺序表。
        /// 边界：单帧最多启动 N 个加载，避免一帧内爆发。
        /// </summary>
        public void Tick(float dt)
        {
            // 先处理等待重试的请求（nextRetryTime 已到）
            // 简化：暂用同一个队列，重试请求由 Request 内部的 nextRetryTime 字段控制
            // 这一步留待批次 3 与 FailureHandler 对接时统一处理

            while (_queue.Count > 0 && _loadingCount < _maxConcurrent)
            {
                var req = _queue.Dequeue();
                StartLoad(req);
            }
        }

        private void StartLoad(LoadRequest req)
        {
            _loadingCount++;

            string path = _registry.ResolvePath(req.key);

            // 版本差异：量产切 Addressables 时，此处改为 Addressables.LoadAssetAsync
            var request = Resources.LoadAsync(path, req.type);
            request.completed += _ => OnLoadCompleted(req, request);
        }

        private void OnLoadCompleted(LoadRequest req, ResourceRequest request)
        {
            _loadingCount--;

            var asset = request.asset;

            if (asset == null)
            {
                HandleFailure(req, "asset not found");
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
                // 简化：立即重试（不延时）
                // 如果需要延时，可在 LoadRequest 上加 nextRetryTime 字段，Tick 中过滤
                _queue.Enqueue(req);
                return;
            }

            _failedCount++;
            req.onFail?.Invoke(reason);
        }

        /// <summary>清空队列。调用方：AssetModule.Dispose。</summary>
        public void Clear()
        {
            _queue.Clear();
        }
    }

    /// <summary>
    /// 加载请求。LoadScheduler 内部数据结构。
    /// </summary>
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

**四个设计点说明**：

1. **`Resources.LoadAsync` 是 Jam 阶段的底层**：注释里标注了切换点。`ResolvePath` 和 `IsTypeMatch` 让路径转换和类型检查与加载本身解耦。
2. **失败重试立即执行，不延时**：简化实现。如果实际观察到重试仍然失败（资源确实不存在），立即重试只是浪费两次 IO。延时重试留到需要时再加（`LoadRequest.nextRetryTime` 字段 + Tick 中过滤）。
3. **`_loadingCount` 用普通 int 而非 Interlocked**：主线程独占，不需要原子操作。若未来引入多线程加载，再改。
4. **`onFail` 在超过重试次数后调用**：由 AssetModule 决定如何降级（返回 fallback 资源）。LoadScheduler 不感知降级策略。

**关于重试的一个已知限制**（记录在此，不阻塞本批次）：

当前重试是「立即重新入队」。如果队列长度 > 并发数，重试请求会排在队尾，实际上延迟了若干帧。这在 Jam 阶段够用。如果未来需要精确的 0.5s 延时重试，改造点在 `HandleFailure`——加一个 `nextRetryTime` 字段，Tick 中检查。

---

## 批次 2 完成

三个文件：`RefCounter.cs` / `AssetRegistry.cs` / `LoadScheduler.cs`。

**遗留到后续批次的事项**：

| 事项                                                         | 归属批次 |
| ------------------------------------------------------------ | -------- |
| `FailureHandler` 与 `LoadScheduler` 的对接（`onFail` 后如何降级） | 批次 3   |
| `LifecycleMgr` 与 `CacheStore` 的对接（冷却期与 LRU）        | 批次 3   |
| `AssetModule.LoadAsync` 中如何组装 `LoadRequest` 并捕获 `AsyncHandle` | 批次 4   |
| `AssetModule.Tick` 如何调用 `LoadScheduler.Tick`             | 批次 4   |

请 review。确认后我进批次 3（`LifecycleMgr` / `FailureHandler`）。