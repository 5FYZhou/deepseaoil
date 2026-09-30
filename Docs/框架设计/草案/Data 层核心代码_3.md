# Data 层核心代码 · 批次 3

两个文件：`LifecycleMgr` / `FailureHandler`。设计原则不变：只写核心，边界通过注释标注。

---

### 3.1 LifecycleMgr

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 生命周期管理。冷却期 + LRU 淘汰。
    /// 调用时机：
    ///   - Tick：GameRoot 每帧驱动
    ///   - OnSceneSwitch：SceneService 切场景时调用
    /// 边界：
    ///   - 主线程独占
    ///   - isPreloaded=true 的条目永不淘汰
    ///   - 一次 Tick 最多淘汰 _maxEvictPerTick 个，避免卡帧
    /// </summary>
    internal sealed class LifecycleMgr
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;
        private readonly int _maxEntries;
        private readonly int _maxEvictPerTick;
        private int _evictedCount;

        public int EvictedCount => _evictedCount;

        public LifecycleMgr(
            CacheStore cache,
            float cooldownSeconds,
            int maxEntries,
            int maxEvictPerTick = 8)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
            _maxEntries = maxEntries;
            _maxEvictPerTick = maxEvictPerTick;
        }

        /// <summary>
        /// 每帧推进。两件事：冷却期到期标记 + LRU 淘汰。
        /// 边界：不修改 refCount；不删除 refCount > 0 的条目。
        /// </summary>
        public void Tick(float dt)
        {
            float now = Time.realtimeSinceStartup;

            // 阶段 1：冷却期到期 → 标记 canEvict
            // 边界：只标记，不删除。删除留给阶段 2 和 EvictLRU。
            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.refCount > 0) continue;
                if (entry.isPreloaded) continue;
                if (entry.canEvict) continue;              // 已标记，跳过
                if (now < entry.cooldownUntil) continue;   // 冷却期未到

                entry.canEvict = true;
            }

            // 阶段 2：超阈值 → 淘汰最久未访问的
            int overflow = _cache.Count - _maxEntries;
            if (overflow > 0)
            {
                EvictLRU(overflow);
            }
        }

        /// <summary>
        /// 按 LRU 淘汰。调用方：Tick 阶段 2。
        /// 边界：
        ///   - 只淘汰 canEvict=true && isPreloaded=false 的条目
        ///   - 单次最多淘汰 _maxEvictPerTick 个
        ///   - 淘汰顺序：lastAccessTime 升序（最久未访问先走）
        /// </summary>
        private void EvictLRU(int count)
        {
            // 收集候选（不在遍历 CacheStore 时直接删除，避免集合修改冲突）
            var candidates = new List<KeyValuePair<string, CacheEntry>>(count);
            foreach (var kv in _cache.AllEntries)
            {
                if (!kv.Value.canEvict) continue;
                if (kv.Value.isPreloaded) continue;
                candidates.Add(kv);
            }

            if (candidates.Count == 0) return;

            // 按 lastAccessTime 升序
            candidates.Sort((a, b) =>
                a.Value.lastAccessTime.CompareTo(b.Value.lastAccessTime));

            int toEvict = Math.Min(
                Math.Min(count, candidates.Count),
                _maxEvictPerTick);

            for (int i = 0; i < toEvict; i++)
            {
                _cache.Remove(candidates[i].Key);
                _evictedCount++;
            }
        }

        /// <summary>
        /// 切场景时调用。调用方：AssetModule.OnSceneSwitch。
        /// 边界：
        ///   - isPreloaded 条目不动
        ///   - refCount > 0 的条目不动（不强制释放）
        ///   - 其余条目：清冷却期 + 立即标记可淘汰
        /// 语义：上一场景的缓存不再"享受冷却期保护"，可被立即淘汰。
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

**四个设计点说明**：

1. **Tick 分两阶段**：先标记（不删除），再淘汰。分离原因——标记是每帧全遍历（O(N)），淘汰只在超阈值时发生。分开写避免在一个循环里同时做两件事，逻辑清晰。
2. **候选收集用 List**：不能在遍历 `_cache.AllEntries` 时直接 `Remove`。先收集再删除，是标准做法。
3. **`_maxEvictPerTick = 8`**：防止一帧内淘汰过多资源导致卡顿。如果实际中超过 8 个需要淘汰，会在多帧内完成。这个常量可以后续调整。
4. **`OnSceneSwitch` 不删任何东西**：只标记 `canEvict = true`。真正删除在下一帧 Tick 的 EvictLRU 里。这样做是为了让 `OnSceneSwitch` 成为纯状态操作，无副作用，调用方（SceneService）不需要担心它会触发 IO 或 GC。

**已知限制**（记录，不阻塞本批次）：

- `_maxEntries` 是条目数量阈值，不是内存阈值。Jam 阶段资源数量少，条目数够用。量产阶段若需要精确内存控制，需引入资产大小估算（`Profiler.GetRuntimeMemorySizeLong`）。

---

### 3.2 FailureHandler

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 失败处理。降级资源注册 + 失败统计。
    /// 调用时机：
    ///   - Init：AssetModule.Init 中调用，注册默认降级资源
    ///   - GetFallback：AssetModule 的 onFail 回调中调用
    ///   - RecordFailure：加载最终失败时调用
    /// 边界：
    ///   - 不参与重试（重试由 LoadScheduler 内部处理）
    ///   - 不抛异常，只记录
    ///   - 降级资源必须由业务代码注册（Data 层不创建 Unity 资源）
    /// </summary>
    internal sealed class FailureHandler
    {
        private readonly Dictionary<Type, UnityEngine.Object> _fallbacks = new();
        private int _failedCount;
        private readonly List<FailureRecord> _recentFailures = new();

        // 只保留最近 N 条，避免无限增长
        private const int MAX_RECENT_RECORDS = 32;

        public int FailedCount => _failedCount;
        public IReadOnlyList<FailureRecord> RecentFailures => _recentFailures;

        /// <summary>
        /// 注册降级资源。
        /// 调用方：AssetModule.Init 中，或业务代码启动时。
        /// 边界：同类型覆盖；不允许注册 null。
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
        /// 取降级资源。
        /// 调用方：AssetModule 的 onFail 回调。
        /// 边界：未注册返回 null；调用方需处理 null（例如完全放弃该资源的显示）。
        /// </summary>
        public T GetFallback<T>() where T : UnityEngine.Object
        {
            if (_fallbacks.TryGetValue(typeof(T), out var fb))
                return fb as T;
            return null;
        }

        /// <summary>
        /// 记录一次最终失败（重试已用尽）。
        /// 调用方：AssetModule 的 onFail 回调。
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

    /// <summary>
    /// 失败记录。供 DataMetrics 展示最近失败。
    /// </summary>
    internal struct FailureRecord
    {
        public string key;
        public string reason;
        public float time;
    }
}
```

**四个设计点说明**：

1. **不参与重试**：重试逻辑在 `LoadScheduler.HandleFailure` 里（批次 2），当 `retryCount > MAX_RETRY` 时才通过 `onFail` 回调通知 AssetModule。`FailureHandler` 只处理"最终失败"的降级与记录。职责边界清晰。
2. **降级资源由业务代码注册**：Data 层不创建 Unity 资源（那是业务层的决定，比如空 Sprite 长什么样、静音 AudioClip 用哪段）。`FailureHandler` 只提供注册与查询机制。
3. **`_recentFailures` 有上限**：只保留最近 32 条，避免长跑游戏中无限增长。若需要完整历史，用日志而非内存记录。
4. **`GetFallback` 可能返回 null**：未注册该类型的降级资源时返回 null，调用方需处理。这比强行返回默认值更诚实——业务代码应该明确知道"我还没注册降级资源"。

**AssetModule 的 onFail 回调（批次 4 会写入 AssetModule）** 大致长这样：

```csharp
onFail = (reason) =>
{
    failure.RecordFailure(key, reason);
    var fallback = failure.GetFallback<T>();
    // fallback 可能为 null
    handle.Complete(fallback);
}
```

---

## 批次 3 完成

两个文件：`LifecycleMgr.cs` / `FailureHandler.cs`。

**已完成的批次总览**：

| 批次 | 文件                                             | 状态 |
| ---- | ------------------------------------------------ | ---- |
| 1    | `AsyncHandle` / `CacheEntry` / `CacheStore`      | ✅    |
| 2    | `RefCounter` / `AssetRegistry` / `LoadScheduler` | ✅    |
| 3    | `LifecycleMgr` / `FailureHandler`                | ✅    |

**下一批次（批次 4）将整合 AssetModule 对外入口**，把前三个批次的微块连接起来。届时会揭示几个需要你拍板的细节：

| 待拍板项                                           | 选项                                            |
| -------------------------------------------------- | ----------------------------------------------- |
| `LoadAsync` 命中缓存时 `Touch` 的时机              | 取出后立即 Touch / 由调用方决定                 |
| `Preload` 的实现方式                               | 走 LoadAsync 路径但 isPreloaded=true / 独立路径 |
| `OnSceneSwitch` 是否也调用 `LoadScheduler.Clear()` | 是（清空挂起请求） / 否（保留挂起请求）         |

请 review 批次 3。确认后进批次 4。