# Data 层核心代码 · 批次 4

## 批次 4：AssetModule 对外入口

本批次把前三个批次的微块整合为 `AssetModule`。开头先对批次 3 结尾提出的三个待拍板项给出定稿与理由，如果你有异议，review 时指出，我回退。

| 待拍板项                               | 定稿                               | 理由                                                         |
| -------------------------------------- | ---------------------------------- | ------------------------------------------------------------ |
| 命中缓存时 `Touch` 时机                | 取出后**立即** Touch               | 访问时间应反映"最后一次被访问"，无论 LoadAsync 还是 TryGet 都算访问 |
| `Preload` 实现方式                     | **独立轻量路径**，不经 AsyncHandle | 预加载没有调用方在 await，走完整句柄路径是浪费               |
| `OnSceneSwitch` 是否清 `LoadScheduler` | **不清**                           | 挂起请求可能是下一场景的预加载；若属于上一场景，加载完成后会被 OnSceneSwitch 的标记逻辑覆盖 |

---

### 4.1 AssetModule

```csharp
using System;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 资源模块。Data 层的资源查询与生命周期管理入口。
    /// 调用时机：
    ///   - Init：GameRoot.Awake 一次
    ///   - Tick：GameRoot 顺序表 step ③，每帧
    ///   - OnSceneSwitch：SceneService 切场景时
    ///   - Dispose：GameRoot.OnDestroy
    ///   - LoadAsync / TryGet / Release / Preload：调用方任意时机
    /// 边界：
    ///   - 所有方法仅主线程调用（Unity 资源 API 限制）
    ///   - 不在 EventBus 上发布任何事件（观测走 DataMetrics 拉模型）
    ///   - 不感知调用方身份、不感知业务语义
    /// </summary>
    public static class AssetModule
    {
        // ─────────────────────────────────────────────
        // 常量（详细设计阶段可调，改动集中在此）
        // ─────────────────────────────────────────────
        private const float COOLDOWN_SECONDS     = 60f;
        private const int   MAX_CACHE_ENTRIES    = 100;
        private const int   MAX_CONCURRENT_LOAD  = 4;
        private const int   MAX_EVICT_PER_TICK   = 8;

        // ─────────────────────────────────────────────
        // 微块实例
        // ─────────────────────────────────────────────
        private static AssetRegistry  _registry;
        private static CacheStore     _cache;
        private static RefCounter     _refCounter;
        private static LoadScheduler  _scheduler;
        private static LifecycleMgr   _lifecycle;
        private static FailureHandler _failure;

        private static bool _initialized;

        // 供 DataMetrics 只读访问
        internal static CacheStore    Cache     => _cache;
        internal static LoadScheduler Scheduler => _scheduler;
        internal static LifecycleMgr  Lifecycle => _lifecycle;
        internal static FailureHandler Failure  => _failure;
        internal static bool IsInitialized => _initialized;

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化。
        /// 调用方：GameRoot.Awake。
        /// 边界：
        ///   - 重复调用抛异常
        ///   - 不创建任何 Unity 资源（降级资源由业务代码注册）
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

            _initialized = true;
        }

        /// <summary>
        /// 每帧推进。
        /// 调用方：GameRoot 顺序表 step ③。
        /// 边界：在未 Init 时 no-op，不抛异常（防止 GameRoot 装配顺序出错时炸）。
        /// </summary>
        public static void Tick(float dt)
        {
            if (!_initialized) return;

            _scheduler.Tick(dt);
            _lifecycle.Tick(dt);
        }

        /// <summary>
        /// 切场景时调用。
        /// 调用方：SceneService.PrepareForSceneSwitch。
        /// 边界：
        ///   - 不清空挂起请求（可能是下一场景的预加载）
        ///   - 不强制释放 refCount > 0 的资源
        ///   - 保留 isPreloaded=true 的条目
        /// </summary>
        public static void OnSceneSwitch()
        {
            if (!_initialized) return;
            _lifecycle.OnSceneSwitch();
        }

        /// <summary>
        /// 进程退出时调用。
        /// 调用方：GameRoot.OnDestroy。
        /// </summary>
        public static void Dispose()
        {
            if (!_initialized) return;

            _scheduler.Clear();
            _cache.Clear();
            _initialized = false;
        }

        // ─────────────────────────────────────────────
        // 主路径：异步加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 异步加载资源。
        /// 调用方：任何层（Logic 或 Presentation）。
        /// 边界：
        ///   - 命中缓存：refCount++，返回已完成句柄（同步返回）
        ///   - 未命中：入队，返回未完成句柄；由 Tick 推进加载
        ///   - 加载完成后：refCount++（自动 Retain）
        ///   - 调用方必须成对调用 Release
        /// </summary>
        public static AsyncHandle<T> LoadAsync<T>(string key)
            where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] LoadAsync before Init");

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[Asset] LoadAsync with empty key");
                var empty = AsyncHandle<T>.Completed(null);
                return empty;
            }

            // ── 命中缓存 ──
            if (_cache.TryGet<T>(key, out var entry))
            {
                _cache.Touch(key);
                _refCounter.Retain(key);
                return AsyncHandle<T>.Completed(entry.asset as T);
            }

            // ── 未命中：入队 ──
            var handle = AsyncHandle<T>.Create();

            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(T),
                onDone = asset =>
                {
                    // 加载成功
                    // 边界：如果 handle 已经被 Dispose 或调用方已放弃，此处仍需写入缓存
                    //       否则会反复触发 IO；调用方应通过 Release 管理生命周期
                    _cache.Put(key, asset, isPreloaded: false);
                    _refCounter.Retain(key);
                    handle.Complete(asset as T);
                },
                onFail = reason =>
                {
                    // 重试已用尽，走降级
                    _failure.RecordFailure(key, reason);
                    var fallback = _failure.GetFallback<T>();
                    // 边界：fallback 可能为 null，调用方需处理
                    handle.Complete(fallback);
                },
            });

            return handle;
        }

        // ─────────────────────────────────────────────
        // 观测路径：查缓存（不触发加载）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 尝试从缓存取资源。
        /// 调用方：任何层，用于避免不必要的异步开销。
        /// 边界：
        ///   - 不触发加载
        ///   - 不改变引用计数
        ///   - 命中时更新访问时间
        /// </summary>
        public static bool TryGet<T>(string key, out T asset)
            where T : UnityEngine.Object
        {
            asset = null;
            if (!_initialized) return false;
            if (string.IsNullOrEmpty(key)) return false;

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
        /// 释放一次引用。
        /// 调用方：LoadAsync 的持有者。
        /// 边界：
        ///   - 未知 Key 记警告，不抛异常
        ///   - refCount 归零后进入冷却期，不立即卸载
        /// </summary>
        public static void Release(string key)
        {
            if (!_initialized) return;
            if (string.IsNullOrEmpty(key)) return;

            if (!_refCounter.Release(key))
                Debug.LogWarning($"[Asset] Release unknown or over-released key: {key}");
        }

        // ─────────────────────────────────────────────
        // 预加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 预加载资源，标记为常驻。
        /// 调用方：SceneService 的 Loading 阶段 / 游戏启动时。
        /// 边界：
        ///   - 走独立轻量路径，不创建 AsyncHandle（无调用方 await）
        ///   - 完成后 isPreloaded=true，永不淘汰
        ///   - 不计入引用计数
        /// </summary>
        public static void Preload(string key)
        {
            if (!_initialized) return;
            if (string.IsNullOrEmpty(key)) return;

            // 已在缓存：直接标记为预加载
            if (_cache.TryGetEntry(key, out var existing))
            {
                existing.isPreloaded = true;
                existing.canEvict = false;
                return;
            }

            // 未在缓存：入队加载
            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(UnityEngine.Object),
                onDone = asset =>
                {
                    _cache.Put(key, asset, isPreloaded: true);
                },
                onFail = reason =>
                {
                    _failure.RecordFailure(key, reason);
                },
            });
        }

        // ─────────────────────────────────────────────
        // 降级资源注册（转发给 FailureHandler）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注册降级资源。
        /// 调用方：业务代码启动时（如 GameRoot.Awake 之后）。
        /// 边界：Data 层不创建资源，由业务代码传入。
        /// </summary>
        public static void RegisterFallback<T>(T fallback)
            where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] RegisterFallback before Init");
            _failure.RegisterFallback(fallback);
        }
    }
}
```

---

## 四个关键设计点说明

### 设计点 1：`LoadAsync` 命中缓存时的返回

```csharp
if (_cache.TryGet<T>(key, out var entry))
{
    _cache.Touch(key);
    _refCounter.Retain(key);
    return AsyncHandle<T>.Completed(entry.asset as T);
}
```

**语义**：`AsyncHandle.Completed` 返回的句柄虽然 "已完成"，但仍然是 `AsyncHandle<T>` 类型。调用方写 `await handle` 时**不会挂起**，直接拿到值。这样调用方代码路径统一——无论命中还是未命中，都是 `var asset = await AssetModule.LoadAsync<T>(key);`，不需要分支。

**代价**：命中缓存时仍然创建了一次 `AsyncHandle` 对象（一次 GC）。但这是 Jam 阶段可接受的取舍，换来的是调用方代码简洁。

**未来优化**（记录，不实现）：如果 GC 是瓶颈，可以暴露一个 `TryLoad<T>` 同步路径，命中时直接返回资源本体，未命中时返回 false。但当前不建议——过早优化。

### 设计点 2：`onDone` 回调里 `Put` 在 `Complete` 之前

```csharp
onDone = asset =>
{
    _cache.Put(key, asset, isPreloaded: false);
    _refCounter.Retain(key);
    handle.Complete(asset as T);   // 最后才 Complete
}
```

**顺序严格**：先写缓存 → 再 Retain → 最后 Complete。原因：调用方 `await handle` 后立即 `TryGet` 或 `Release` 时，缓存和引用计数必须已经就绪。如果顺序反了，会出现"句柄已 resolve 但缓存还没写入"的窗口。

### 设计点 3：`onFail` 里 `Complete(null)` 而非抛异常

```csharp
onFail = reason =>
{
    _failure.RecordFailure(key, reason);
    var fallback = _failure.GetFallback<T>();
    handle.Complete(fallback);   // fallback 可能为 null
}
```

**语义**：加载失败不阻塞调用方。调用方 `await` 后拿到的是降级资源或 `null`，由调用方决定如何处理。**日志已由 `RecordFailure` 记录**，不需要调用方再处理错误信息。

**为什么用 `Complete` 而不是 `SetException`**：`await` 时抛异常会打断调用方的异步链，需要每个调用方 try-catch。而游戏内资源缺失通常不是"致命错误"，是"降级显示"——用 `Complete(null)` 更符合游戏内的容错语义。

### 设计点 4：`Preload` 的轻量路径

```csharp
_scheduler.Enqueue(new LoadRequest
{
    key = key,
    type = typeof(UnityEngine.Object),   // 不约束类型
    onDone = asset => _cache.Put(key, asset, isPreloaded: true),
    onFail = reason => _failure.RecordFailure(key, reason),
});
```

**三个要点**：

1. **不创建 AsyncHandle**：预加载没有调用方在 await，创建句柄是浪费。
2. **不 Retain**：预加载资源不计入引用计数，由 `isPreloaded=true` 保证不被淘汰。
3. **`typeof(UnityEngine.Object)` 作为类型**：不约束具体类型。但注意——`Resources.LoadAsync` 用基类作为类型时，返回的资源类型可能与实际不符（Unity 会按路径找到资源，但类型过滤可能不生效）。**这是一个已知风险**，Jam 阶段如果预加载的资源类型明确，建议调用方传入具体类型。

**待验证项**：`Resources.LoadAsync(path, typeof(UnityEngine.Object))` 在 Unity 2022.3 上是否返回正确的资源类型。如果不行，需要给 `Preload` 加泛型参数 `Preload<T>(string key)`。这个验证放到你实操阶段。

---

## 批次 4 完成

一个文件：`AssetModule.cs`。

**已完成的批次总览**：

| 批次 | 文件                                             | 状态 |
| ---- | ------------------------------------------------ | ---- |
| 1    | `AsyncHandle` / `CacheEntry` / `CacheStore`      | ✅    |
| 2    | `RefCounter` / `AssetRegistry` / `LoadScheduler` | ✅    |
| 3    | `LifecycleMgr` / `FailureHandler`                | ✅    |
| 4    | `AssetModule`                                    | ✅    |

**遗留事项**（不阻塞当前，记录在案）：

| 事项                                                       | 归属                           |
| ---------------------------------------------------------- | ------------------------------ |
| `Preload` 的 `typeof(UnityEngine.Object)` 是否需要改为泛型 | 待你实操验证                   |
| 命中缓存时创建 `AsyncHandle` 的 GC 开销                    | 待 Profiler 验证，非瓶颈则不做 |
| `LoadScheduler` 的重试是否改为延时                         | 待批次 5 完成后评估            |

请 review 批次 4。确认后我进批次 5（ConfigModule：`TablesHolder` / `StartupValidator` / `ConfigModule`）。