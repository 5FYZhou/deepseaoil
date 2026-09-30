# Data 层核心代码 · 批次 6

## 批次 6：DataMetrics

两个文件：`DataSnapshot` / `DataMetrics`。

**前置说明**：`DataMetrics` 要读取"缓存命中率"，但批次 4 的 `AssetModule` 没有跟踪命中/未命中计数器。本批次包含一份**对 AssetModule 的最小补丁**，只加两个静态计数器，不改任何已有逻辑。

---

### 6.0 对批次 4 的增量补丁：AssetModule 补计数器

在 `AssetModule.cs` 的字段区新增：

```csharp
// 供 DataMetrics 读取（仅统计，不参与业务逻辑）
private static int _cacheHits;
private static int _cacheMisses;

internal static int CacheHits   => _cacheHits;
internal static int CacheMisses => _cacheMisses;
```

在 `LoadAsync` 的两个分支各加一行：

```csharp
// 命中缓存分支
if (_cache.TryGet<T>(key, out var entry))
{
    _cacheHits++;                    // ← 新增
    _cache.Touch(key);
    _refCounter.Retain(key);
    return AsyncHandle<T>.Completed(entry.asset as T);
}

// 未命中分支
_cacheMisses++;                      // ← 新增
var handle = AsyncHandle<T>.Create();
```

在 `Dispose` 中重置：

```csharp
_cacheHits = 0;
_cacheMisses = 0;
```

**为什么放在 AssetModule 而非 DataMetrics**：计数器记录的是 AssetModule 内部事件，应该由事件发生地维护。DataMetrics 只读，不写。这是职责分离。

---

### 6.1 DataSnapshot

```csharp
namespace Jam.Data
{
    /// <summary>
    /// Data 层只读快照。
    /// 调用时机：DebugOverlay 每 N 帧拉取一次。
    /// 边界：
    ///   - struct，值语义；调用方拿到的是副本，改不了内部状态
    ///   - 所有字段为瞬时值，不保证跨帧一致
    ///   - 字段只增不删（DebugOverlay 可能引用旧字段）
    /// </summary>
    public struct DataSnapshot
    {
        // ─── AssetModule ───
        /// <summary>缓存中条目总数（含 refCount 为 0 与 isPreloaded 的）</summary>
        public int   CachedAssetCount;

        /// <summary>当前正在加载的数量</summary>
        public int   LoadingCount;

        /// <summary>排队等待加载的数量</summary>
        public int   QueuedCount;

        /// <summary>累计加载完成数（进程启动至今）</summary>
        public int   CompletedCount;

        /// <summary>累计加载失败数（含降级）</summary>
        public int   FailedCount;

        /// <summary>累计淘汰数</summary>
        public int   EvictedCount;

        /// <summary>缓存命中次数（进程启动至今）</summary>
        public int   CacheHits;

        /// <summary>缓存未命中次数（进程启动至今）</summary>
        public int   CacheMisses;

        /// <summary>缓存命中率 [0, 1]；无访问时为 0</summary>
        public float CacheHitRate;

        // ─── ConfigModule ───
        /// <summary>ConfigModule 是否已 Init 成功</summary>
        public bool  ConfigReady;

        /// <summary>已加载的表数</summary>
        public int   TableCount;
    }
}
```

**两个设计点说明**：

1. **字段只增不删**：注释里写明。未来 DebugOverlay 可能依赖某个字段，删字段会静默断链。加字段是安全的，删字段需要同步修改消费方。
2. **`CacheHitRate` 是冗余字段**：可由 `CacheHits / (CacheHits + CacheMisses)` 算出。冗余的理由——消费方（DebugOverlay）只想显示一个数，不想每次算。计算逻辑集中在 `DataMetrics` 内部，消费方不做除法。

---

### 6.2 DataMetrics

```csharp
namespace Jam.Data
{
    /// <summary>
    /// Data 层可观测性表面。
    /// 调用时机：DebugOverlay 每 N 帧拉取。
    /// 边界：
    ///   - 只读，不修改任何 Module 状态
    ///   - 拉模型，不推送事件，不走 EventBus
    ///   - 不持有任何资源引用
    ///   - 允许在未 Init 时调用（返回零值快照）
    /// </summary>
    public static class DataMetrics
    {
        /// <summary>
        /// 获取当前快照。
        /// 调用方：DebugOverlay。
        /// 边界：任意时刻可调用；未 Init 时返回零值快照。
        /// </summary>
        public static DataSnapshot GetSnapshot()
        {
            var snap = new DataSnapshot();

            // ── ConfigModule ──
            snap.ConfigReady = ConfigModule.IsReady;
            snap.TableCount  = ConfigModule.IsReady
                ? CountTables()
                : 0;

            // ── AssetModule ──
            if (!AssetModule.IsInitialized)
                return snap;

            snap.CachedAssetCount = AssetModule.Cache.Count;
            snap.LoadingCount     = AssetModule.Scheduler.LoadingCount;
            snap.QueuedCount      = AssetModule.Scheduler.QueuedCount;
            snap.CompletedCount   = AssetModule.Scheduler.CompletedCount;
            snap.FailedCount      = AssetModule.Failure.FailedCount;
            snap.EvictedCount     = AssetModule.Lifecycle.EvictedCount;
            snap.CacheHits        = AssetModule.CacheHits;
            snap.CacheMisses      = AssetModule.CacheMisses;

            int total = snap.CacheHits + snap.CacheMisses;
            snap.CacheHitRate = total > 0
                ? (float)snap.CacheHits / total
                : 0f;

            return snap;
        }

        /// <summary>
        /// 数 Tables 里有多少张表。
        /// 边界：依赖 cfg.Tables 暴露的字段；Luban 版本差异可能导致字段名不同。
        /// 待验证：cfg.Tables 是否有稳定的表数量属性。
        /// </summary>
        private static int CountTables()
        {
            // 占位实现：如果 Luban 生成的 Tables 有 TableCount 之类的属性，直接用
            // 否则按项目实际表硬编码，或改为反射遍历（不推荐）
            // 这里返回占位值，你实操时按实际生成的 Tables 结构替换
            return 0;
        }
    }
}
```

**四个设计点说明**：

1. **`GetSnapshot` 在未 Init 时也安全**：ConfigModule / AssetModule 都做了 Init 状态检查。DebugOverlay 在 GameRoot 装配完成前也能调用，拿到的是零值快照。
2. **`CacheHitRate` 在无访问时为 0**：不做特殊处理（比如 NaN）。消费方看到 0 就知道"还没加载过"。如果希望显示"无数据"，那是 DebugOverlay 的格式化职责，不是 DataMetrics 的。
3. **`CountTables` 是占位实现**：Luban 生成的 `Tables` 类是否有稳定的表数量属性，我不确定。**标记为待验证项**。Jam 阶段如果 DebugOverlay 不需要这个数，可以直接 `return 0` 不做。量产阶段再补。
4. **`AssetModule.IsInitialized` 需要 internal 可见**：批次 4 已经定义为 `internal static bool IsInitialized => _initialized;`，DataMetrics 在同一程序集，可以访问。

---

### 6.3 DebugOverlay 的消费方式（示意，不属于本批次代码）

这部分只是说明 DataMetrics 的消费方长什么样，**不需要你实现**。

```csharp
// Presentation/Debug/DebugOverlay.cs 中的示意
void DrawDataLayerStats()
{
    var snap = DataMetrics.GetSnapshot();

    GUILayout.Label($"Config: ready={snap.ConfigReady}, tables={snap.TableCount}");
    GUILayout.Label($"Assets: cached={snap.CachedAssetCount}, " +
                    $"loading={snap.LoadingCount}, queued={snap.QueuedCount}");
    GUILayout.Label($"Asset lifetime: completed={snap.CompletedCount}, " +
                    $"failed={snap.FailedCount}, evicted={snap.EvictedCount}");
    GUILayout.Label($"Cache: hit={snap.CacheHits}, miss={snap.CacheMisses}, " +
                    $"rate={snap.CacheHitRate:P0}");
}
```

**调用频率**：`DebugOverlay.Tick` 每帧调用 `GetSnapshot()` 一次。`GetSnapshot` 本身不做分配（struct + 无 List），开销可忽略。

---

## 批次 6 完成 · Data 层全部完成

两个文件：`DataSnapshot.cs` / `DataMetrics.cs`。外加对 `AssetModule.cs` 的最小补丁。

**Data 层完整文件清单**：

```
Assets/Scripts/Data/
├── ConfigModule/
│   ├── ConfigModule.cs           ✅ 批次 5
│   ├── TablesHolder.cs           ✅ 批次 5
│   └── StartupValidator.cs       ✅ 批次 5
├── AssetModule/
│   ├── AssetModule.cs            ✅ 批次 4（+ 批次 6 补丁）
│   ├── AssetRegistry.cs          ✅ 批次 2
│   ├── CacheStore.cs             ✅ 批次 1
│   ├── CacheEntry.cs             ✅ 批次 1
│   ├── RefCounter.cs             ✅ 批次 2
│   ├── LoadScheduler.cs          ✅ 批次 2
│   ├── LifecycleMgr.cs           ✅ 批次 3
│   ├── FailureHandler.cs         ✅ 批次 3
│   └── AsyncHandle.cs            ✅ 批次 1
└── DataMetrics/
    ├── DataMetrics.cs            ✅ 批次 6
    └── DataSnapshot.cs           ✅ 批次 6
```

共 **13 个文件**。

---

## 全局遗留事项（待你实操验证）

| #    | 事项                                                         | 影响                     | 归属   |
| ---- | ------------------------------------------------------------ | ------------------------ | ------ |
| 1    | Luban Loader 签名（JObject vs JSONNode）                     | 编译                     | 批次 5 |
| 2    | `cfg.Tables` 的表访问器实际类型名（`TbWeapon` 等）           | 编译                     | 批次 5 |
| 3    | `cfg.Tables` 是否有稳定的表数量属性                          | `CountTables` 实现       | 批次 6 |
| 4    | `Resources.LoadAsync(path, typeof(UnityEngine.Object))` 是否返回正确类型 | `Preload` 是否需要改泛型 | 批次 4 |
| 5    | Android StreamingAssets 读取方式                             | 平台兼容                 | 批次 5 |
| 6    | `AsyncHandle.Completed` 的 GC 开销是否可接受                 | 性能                     | 批次 4 |

---

## 整体回顾：Data 层设计的三条主线

**主线 1：职责分离**

- ConfigModule 只管数值，同步，无释放
- AssetModule 只管资源，异步，引用计数
- 两者零依赖，通过字符串 Key 衔接，调用方是衔接点

**主线 2：轻量智能**

- 智能体现在"调用方不需要思考的事变多"：透明缓存、自动生命周期、冷却期、并发控制、失败降级
- 轻量体现在"不引入任何额外概念"：零外部依赖、零反射、零事件订阅、零配置文件、零新程序集

**主线 3：与 Luban 的相性**

- Luban 只负责 Excel → 强类型代码 + JSON
- ConfigModule 只负责持有 Tables + 暴露查询 + 启动校验
- AssetModule 只接收字符串 Key，不知道 Luban 存在
- 边界清晰，各自演进互不影响

---

Data 层核心代码全部完成。请 review 批次 6。确认后，下一步可以选择：

1. **补 GameRoot 的装配代码**——把 ConfigModule / AssetModule 接入框架的初始化序
2. **补 EventBus 与 Singleton**——框架的基础设施
3. **先实操验证**——把这份代码丢进 Unity 工程跑一遍，暴露遗留事项

你定优先级。