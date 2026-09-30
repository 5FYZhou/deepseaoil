# Data 层设计

> 本文件是 `框架蓝图.md` 第 9 节的展开落到设计粒度。**规范性来源仍是蓝图**：蓝图定"能不能这么做"，本文件定"怎么做"。
> 配套实现见 `Data 层实现.md`（13 个文件的完整可编译代码）。
>
> 本文件由草案 `Data 层详细设计.md`、`Data 层设计缺漏文档.md` 合并而成，并**修正了草案与仓库实际不符的 4 处假设**（见 §11）。

---

## 1. 定位

### 一句话定义

**Data 层是游戏内所有静态内容的唯一提供者，回答两类问题："值是多少？"（同步）和"东西在哪？"（异步）。**

### 它不是什么

| 不是 | 原因 |
| :-- | :-- |
| 不是 Service | 没有业务状态，不参与游戏逻辑 |
| 不是缓存中心 | 缓存是 AssetModule 的内部实现，不是对外概念 |
| 不是事件中心 | 不走 EventBus，不发布不订阅 |
| 不是业务数据仓 | 不存运行时状态（存档、进度），那些归 `SaveService` |

### 它是什么

| 是 | 表现 |
| :-- | :-- |
| 静态内容的只读查询接口 | 输入 Key → 输出值或资源 |
| 进程级常驻 | 启动装配，运行时不释放（单个资源条目按引用计数走，见 §4.7） |
| 被所有层直连 | Logic 与 Presentation 都能调，不走 EventBus |
| 零业务依赖 | 不知道谁在调、为什么调 |

### 三条硬边界（与蓝图契约表同步）

| # | 边界 | 理由 |
| :-- | :-- | :-- |
| 1 | Data 层**不继承 `Singleton<T>`**，用 `static class` + 显式 `Init` | 需要确定的初始化时序；懒加载单例保证不了"Config 必须先于 Asset" |
| 2 | Data 层**不订阅、不发布 EventBus 事件** | 查询式接口无事件语义；加载完成通过 `AsyncHandle` 传递 |
| 3 | Data 层**唯一的主动行为是 `AssetModule.Tick`**，由 GameRoot 顺序表驱动，且只处理自己的内部状态 | 队列推进 / 冷却期 / LRU 都需要每帧推进；这条例外不做通用模式推广 |

---

## 2. 微块总览

```
Data 层
│
├── ① ConfigModule           数值配置查询（同步）
│   ├── 1.1 TablesHolder     持有 Luban 生成的 cfg.Tables
│   ├── 1.2 QueryAccessor    类型化查询入口（薄转发）
│   └── 1.3 StartupValidator 启动时确认产物可用
│
├── ② AssetModule            资源查询与生命周期（异步）
│   ├── 2.1 AssetRegistry    Key → 加载路径（唯一的路径转换点）
│   ├── 2.2 CacheStore       Key → CacheEntry
│   ├── 2.3 RefCounter       引用计数
│   ├── 2.4 LoadScheduler    并发控制 + 队列 + 失败重试
│   ├── 2.5 LifecycleMgr     冷却期 + LRU 淘汰
│   └── 2.6 FailureHandler   降级资源注册 + 失败记录
│
└── ③ DataMetrics            可观测性表面（拉模型）
    ├── 3.1 CounterSet       缓存数 / 加载中数 / 命中率
    └── 3.2 Snapshot         只读快照，给 DebugOverlay
```

**为什么是三个微块而不是两个**：ConfigModule 与 AssetModule 是服务提供者，DataMetrics 是**观测表面**。它不属于任何 Module，但属于 Data 层——只有 Data 层知道自己的内部状态。塞进 AssetModule 会让 AssetModule 承担"被观测"职责。

---

## 3. 微块 ① ConfigModule

### 3.1 职责边界

| 负责 | 不负责 |
| :-- | :-- |
| 持有 Luban 生成的 `cfg.Tables` 实例 | 不解析 Excel（那是 Luban 工具的事） |
| 提供类型化查询入口 | 不做数值计算（那是 Logic 的事） |
| 启动时确认数据可用 | 不缓存查询结果（`Tables` 本身已是内存结构） |
| 暴露表级访问（逃生舱） | 不感知资源（资源引用是字符串 Key，不是资源本体） |

### 3.2 对外接口

```csharp
public static class ConfigModule
{
    public static bool IsReady { get; }

    public static void Init(string jsonRoot);   // 指定 JSON 目录
    public static void InitFromStreamingAssets(); // 默认 StreamingAssets/Luban

    // 类型化查询（薄转发给 cfg.Tables 的 TbXxx）
    public static cfg.demo.Weapon GetWeapon(int id);
    public static cfg.demo.Item   GetItem(int id);
    public static IReadOnlyList<cfg.demo.Weapon> GetAllWeapons();

    // 逃生舱：特殊情况直接访问原始 Tables
    public static cfg.Tables Tables { get; }
}
```

**设计要点**：

- **薄封装**：不重新发明 Luban 的查询 API，`ConfigModule.GetWeapon(id)` 内部直接转发 `Tables.TbWeapon.Get(id)`。ConfigModule 的价值在**统一入口 + 生命周期 + 启动确认**，不在改造数据形态。
- **逃生舱**：`Tables` 暴露原始实例。代价是调用方可能绕过封装；Jam 期灵活性优先，约束写进注释——**不得跨帧持有**。
- **无状态**：ConfigModule 自己不存数据，数据全在 `cfg.Tables` 里。

### 3.3 内部微块

| 微块 | 职责 | 状态 |
| :-- | :-- | :-- |
| **1.1 TablesHolder** | 持有 `cfg.Tables`，负责构造与 Loader | 一个字段 |
| **1.2 QueryAccessor** | 类型化查询转发（不单独建类，直接写在 ConfigModule 上） | 无状态 |
| **1.3 StartupValidator** | 抽样访问关键表，确认产物可用；失败返回 false | 无状态 |

> **与草案的差异**：草案为 QueryAccessor 单列一个 `internal class`。实际转发代码只有几行且无状态，单独成类只增加跳转层级，**合并进 ConfigModule**（KISS）。

### 3.4 与 Luban 的相性（按仓库实际生成目标核对）

| 项 | 实际值 | 出处 |
| :-- | :-- | :-- |
| 生成目标 | `cs-simple-json` | `ConfigWorkspace/AGENTS.md` 导表命令；`luban.conf` 的 target `client` |
| Loader 签名 | `System.Func<string, JSONNode>` | `Assets/Scripts/Config/Tables.cs` |
| 命名空间 | `cfg`（Tables）/ `cfg.demo`（记录类与表类） | 同上 |
| 表访问器 | `TbWeapon` / `TbItem` / `TbFish`，各有 `DataMap` / `DataList` / `Get` / `GetOrDefault` / `ResolveRef` | `Assets/Scripts/Config/demo/TbWeapon.cs` |
| 外键 | 生成 `Xxx_Ref` 字段，构造时由 `ResolveRef` 解析 | `Assets/Scripts/Config/demo/Weapon.cs` |
| 表数量 | **没有** `TableCount` 之类的属性 | 同 TbWeapon.cs 全文 |

**关键边界**：**ConfigModule 不负责解析 JSON 文件**。加载是 `cfg.Tables` 构造函数的工作（它调用我们传入的 Loader）；ConfigModule 只负责在正确时机调用它、持有它、暴露它。

### 3.5 启动失败策略

| 阶段 | 失败表现 | 处理 |
| :-- | :-- | :-- |
| 目录不存在 | `DirectoryNotFoundException` | 由 `ConfigModule.Init` 包成 `ConfigLoadException` 抛出 |
| JSON 文件缺失 / 为空 | `FileNotFoundException` / `InvalidDataException` | 同上 |
| JSON 结构不符（字段类型错） | Luban 生成代码抛 `SerializationException` | 同上 |
| 抽样表访问失败 | `StartupValidator` 返回 false | 抛 `ConfigLoadException` |

**原则：启动时失败优于运行时失败**。带病数据不进运行时——`GameRoot.Awake` 捕获 `ConfigLoadException` 后阻止游戏启动。异常类型独立定义，让 GameRoot 能区分"配置问题（重新导表）"与"代码问题（改代码）"。

Luban 的 `ref` / `path` / `range` 校验在**导表阶段**已由 `--strict` 执行，运行时**不重复校验**（`ConfigWorkspace/AGENTS.md`：`--strict` 决定退出码）。

### 3.6 表清单的显式维护点

`cfg.Tables` 没有表数量属性，也不允许手改生成物。需要"已加载几张表"时，用手写清单：

```csharp
// Assets/Scripts/Game/TablesMeta.cs（手写，非生成物）
namespace DeepseaOil.Config
{
    public static class TablesMeta
    {
        public static readonly string[] Names = { "TbWeapon", "TbItem", "TbFish" };
        public static int Count => Names.Length;
    }
}
```

**代价**：加表后要同步一行。**收益**：`DataMetrics.TableCount` 与 `StartupValidator` 的关键表抽样都变成显式决策，比反射遍历可控（生成物无反射，保持"零反射"轻量点）。

---

## 4. 微块 ② AssetModule

### 4.1 职责边界

| 负责 | 不负责 |
| :-- | :-- |
| 异步加载资源 | 不解析配置（Key 从调用方来） |
| 引用计数管理 | 不决定"何时该释放"（冷却期 + LRU 自动决定） |
| 冷却期缓存 | 不管理 GameObject 生命周期（那是对象池的事；工程里有 `Pool<T>`，但当前零调用点） |
| 并发控制 | 不做资源分组（Jam 期单资源为原子单位） |
| LRU 淘汰 | 不感知场景（`SceneService` 通过 `OnSceneSwitch` 通知） |
| 失败重试与降级 | 不弹窗、不阻塞（失败写日志 + 返回占位资源） |

### 4.2 对外接口

```csharp
public static class AssetModule
{
    public static void Init();                       // GameRoot.Awake
    public static void Tick(float dt);               // 顺序表 step ③
    public static void OnSceneSwitch();              // SceneService 切场景前
    public static void Dispose();                    // GameRoot.OnDestroy

    public static AsyncHandle<T> LoadAsync<T>(string key) where T : UnityEngine.Object;
    public static bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object;
    public static void Release(string key);
    public static void Preload(string key);
    public static void RegisterFallback<T>(T fallback) where T : UnityEngine.Object;
}
```

### 4.3 内部微块

#### 2.1 AssetRegistry —— 唯一的路径转换点

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 把调用方 Key 解析为 `Resources.LoadAsync` 可用的路径 |
| **输入** | Luban 表里的字符串，形如 `"Settings/Renderer2D.asset"`（相对 `Assets/`、带扩展名） |
| **输出** | `"Settings/Renderer2D"`（相对 `Assets/Resources/`、无扩展名） |
| **状态** | 无状态（Jam 期不做映射表） |
| **未来** | 量产切 Addressables 时返回 Address 地址；改动只在这一个方法里 |

完整 Key 契约见 §9。

#### 2.2 CacheStore + CacheEntry

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 持有所有已加载资源的条目 |
| **结构** | `Dictionary<string, CacheEntry>`，**主线程独占，无锁** |
| **CacheEntry 字段** | `asset` / `refCount` / `lastAccessTime` / `isPreloaded` / `cooldownUntil` / `canEvict` |
| **约束** | `AllEntries` 直接暴露内部集合（`LifecycleMgr` 每帧遍历，避免临时数组）；**遍历中不得增删**，靠代码评审保证 |

**CacheEntry 状态机**：

```
[未加载] --LoadAsync 未命中--> [排队中] --Tick 启动--> [加载中]
                                                        │
                                          ┌─────────────┴─────────────┐
                                       成功                         失败
                                          │                           │
                              [已缓存 refCount>=1]            重试 <=2 次
                                          │                           │
                                    Release│                    仍失败 ↓
                                          ↓                    [降级 + 记录]
                              [已缓存 refCount==0 冷却期 60s]
                                          │
                                  冷却期结束 ↓
                                   [canEvict=true]
                                          │
                                  LRU 超阈值 ↓
                                     [被淘汰移除]
```

**关键设计**：**`refCount == 0` 不等于立即卸载**。它进冷却期，冷却期结束才标记 `canEvict`，LRU 在此时才可能真正移除条目。

#### 2.3 RefCounter

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 维护每个 CacheEntry 的 `refCount` |
| **规则** | `LoadAsync` 成功 = +1；`Release` = −1；`TryGet` **不**改变计数；`isPreloaded` 条目 Retain/Release 均为 no-op |
| **边界** | `refCount` 不为负；多调 `Release` 返回 false（不抛异常）；从 0 变 1 时清除 `cooldownUntil` 与 `canEvict` |

**为什么独立成微块**：引用计数是"自动生命周期"的核心，独立出来才有明确的测试边界（可单独测 +1/−1）。

#### 2.4 LoadScheduler

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 限制并发数、排队超额请求、处理失败重试 |
| **机制** | `int _loadingCount` + `Queue<LoadRequest>`；`Tick` 中 `while (queue.Count > 0 && _loadingCount < maxConcurrent)` |
| **并发数** | `MAX_CONCURRENT_LOAD = 4`（常量） |
| **重试** | 最多 2 次，**立即重新入队**（不延时） |
| **类型检查** | 加载完成后由 `AssetRegistry.IsTypeMatch` 判定，不匹配按失败处理 |

> **与草案的差异（C2）**：草案 `Data 层详细设计.md` 写的是 `SemaphoreSlim`，并在 `Tick` 里调 `semaphore.Wait()`。`Wait()` 会**阻塞主线程**，且主线程独占时信号量本身多余。**统一用计数器**。

> **与草案的差异（C1）**：草案同时存在两种重试描述——详细设计说"0.5s 延时、由 Tick 驱动"，伪代码说"立即重试"。**裁定以立即重试为准**：实现简单、Jam 期队列短、延时重试的收益不可观测。`AssetModule.Tick` **不**驱动 `FailureHandler`，后者收窄为"降级注册 + 失败记录"。延时重试登记为未采纳项（§10 O7），需要时的改造点在 `HandleFailure`：加 `nextRetryTime` 字段 + `Tick` 中过滤。

#### 2.5 LifecycleMgr

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 冷却期标记 + LRU 淘汰 |
| **冷却期** | `COOLDOWN_SECONDS = 60f`；`refCount` 归零时设 `cooldownUntil = now + 60` |
| **标记** | `Tick` 中：`refCount == 0 && !isPreloaded && !canEvict && now >= cooldownUntil` → `canEvict = true` |
| **淘汰** | 条目数 > `MAX_CACHE_ENTRIES = 100` 时，按 `lastAccessTime` 升序淘汰 `canEvict && !isPreloaded` 的条目 |
| **限流** | 单帧最多淘汰 `MAX_EVICT_PER_TICK = 8` 条，防卡帧 |
| **预加载保护** | `isPreloaded = true` 的条目永不被淘汰 |
| **回收** | 本轮淘汰数 > 0 时调用一次 `Resources.UnloadUnusedAssets()`（见 §7 D2） |

**冷却期的意义**：UI 面板反复开关、音效反复播放是"高频加载"的典型场景。冷却期把"关面板时释放 → 开面板时重新加载"变成"关面板时保留 → 开面板时直接命中"，消除重复 IO。

**为什么是 60s**：Jam 期调试足够覆盖"来回切换"，太长会滞留内存。常量，改一行可调。

**已知限制**：阈值是**条目数**不是内存量。Jam 期资源少够用；量产需要精确控制时引入资产大小估算（`Profiler.GetRuntimeMemorySizeLong`）。

#### 2.6 FailureHandler

| 维度 | 内容 |
| :-- | :-- |
| **职责** | 降级资源注册与查询 + 失败记录与统计 |
| **不负责** | **不参与重试**（重试在 `LoadScheduler.HandleFailure`） |
| **降级资源** | 由业务代码注册（`RegisterFallback<T>`）；Data 层不创建 Unity 资源 |
| **未注册时** | `GetFallback<T>()` 返回 null；调用方需处理（比强行返回默认值更诚实） |
| **记录** | `_failedCount` 累计 + 最近 32 条 `FailureRecord`（防无限增长）+ `Debug.LogError` |

**为什么降级资源要业务注册**：占位图长什么样、静音 AudioClip 用哪段，是业务决策不是框架决策。举例：URP 下建一个 `Texture2D` 不能直接当 `Sprite` 用，占位 Sprite 得走 `Sprite.Create` —— 这类细节属于业务。

### 4.4 生命周期

```
装配（GameRoot.Awake，阶段 1）
  ├─ ConfigModule.InitFromStreamingAssets()   ① 同步，失败抛异常
  └─ AssetModule.Init()                        ② 建缓存表 / 调度器 / 生命周期

每帧（GameRoot.Update 顺序表第 ② 步）
  └─ AssetModule.Tick(dt)
       ├─ LoadScheduler.Tick(dt)   推进队列（受并发上限约束）
       └─ LifecycleMgr.Tick(dt)    冷却期标记 + LRU 淘汰（含 UnloadUnusedAssets）

切场景（SceneService.Load 第 ④ 步）
  └─ AssetModule.OnSceneSwitch()
       ├─ 清空所有非预加载条目的冷却期（canEvict = true）
       ├─ 保留 isPreloaded = true 的条目
       ├─ 不强制释放 refCount > 0 的条目
       └─ 不动挂起请求与合并列表（可能是新场景的预加载）

进程退出（GameRoot.OnDestroy）
  └─ AssetModule.Dispose()
       ├─ 清队列、清缓存、清合并列表、重置命中计数
       └─ 不调用 UnloadUnusedAssets（进程都要退了）
```

### 4.5 主路径语义

| 场景 | 行为 |
| :-- | :-- |
| `LoadAsync` 命中缓存 | `refCount++` → 返回**已完成**句柄（同步返回，`await` 不挂起） |
| `LoadAsync` 未命中 | 入队 → 返回未完成句柄；由 `Tick` 推进加载 |
| 加载成功 | `Put` 缓存 → `Retain` → **最后** `Complete`（顺序严格，见下） |
| 加载失败（重试耗尽） | `RecordFailure` → 取降级资源（可能 null） → `Complete(fallback)` |
| `TryGet` 命中 | 更新 `lastAccessTime`，**不改**引用计数 |
| `Release` 未知 Key / 多调 | 记 `LogWarning`，不抛异常（防守性释放是常见写法） |
| 空 Key | `LoadAsync` 返回 `Completed(null)` 并记 error；`Release` / `Preload` 直接忽略 |

**`onDone` 里三步顺序为什么严格**：调用方 `await` 后可能**立即** `TryGet` 或 `Release`。若顺序反了，会出现"句柄已 resolve 但缓存还没写入"的窗口。

### 4.6 AsyncHandle 契约

| 项 | 决定 | 理由 |
| :-- | :-- | :-- |
| 类型 | `sealed class`（**不能是 struct**） | 跨帧持有；struct 复制会破坏完成语义 |
| 完成方式 | `TrySetResult`，不抛异常 | 资源缺失不是致命错误，降级路径不该抛 |
| 线程 | `Complete` **必须**主线程调用；续体在主线程内联执行 | Unity 资源 API 仅主线程；Unity 2022.3 无 `Awaitable` |
| **不用** `RunContinuationsAsynchronously` | 该选项会让续体走线程池，`await` 后调用 Unity API 会崩 | 见下 |

> **与草案的差异（C3）**：草案 `Data 层核心代码_1.md` 用了 `new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)`。该重载在 .NET Standard 2.1 存在（[MSDN](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1.-ctor?view=netstandard-2.1)），所以**编译得过**；但它的语义是"续体不走同步上下文，改走线程池"，与 Unity 的续体回主线程机制（[Unity 手册](https://docs.unity3d.com/6000.0/Documentation/Manual/async-awaitable-continuations.html)：`Task` 续体默认 posted 到 `UnitySynchronizationContext`，下一帧 Update 主线程执行）冲突。**去掉该选项**，让 `await` 的后续代码在主线程内联执行——本项目所有加载都在主线程发起，这是最简单且正确的选择。

### 4.7 引用计数与预加载

| 概念 | 语义 |
| :-- | :-- |
| `refCount` | **调用方持有数**，只有 `LoadAsync` 成功会 +1，只有 `Release` 会 −1 |
| `isPreloaded` | 常驻标记。Retain/Release 对它是 no-op，LRU 永不淘汰它 |
| 冷却期 | `refCount` 归零后的保护窗口，窗口内即使超阈值也不淘汰 |
| `OnSceneSwitch` | 清冷却期（防上一场景缓存误命中新场景），但跳过预加载与仍在被持有的条目 |

---

## 5. 微块 ③ DataMetrics

### 5.1 职责边界

| 负责 | 不负责 |
| :-- | :-- |
| 采集 Data 层内部状态 | 不做逻辑判断 |
| 提供只读快照 | 不写任何 Module 状态 |
| 供 DebugOverlay 读取 | 不推送事件（不走 EventBus） |

### 5.2 快照字段

| 字段 | 含义 |
| :-- | :-- |
| `ConfigReady` | ConfigModule 是否 Init 成功 |
| `TableCount` | 已加载表数（`TablesMeta.Count`） |
| `CachedAssetCount` | 缓存条目总数（含 `refCount == 0` 与 `isPreloaded`） |
| `LoadingCount` | 正在加载的数量 |
| `QueuedCount` | 排队等待的数量 |
| `CompletedCount` | 累计加载完成数 |
| `FailedCount` | 累计最终失败数 |
| `EvictedCount` | 累计淘汰数 |
| `CacheHits` / `CacheMisses` | 累计命中 / 未命中次数 |
| `CacheHitRate` | `Hits / (Hits + Misses)`，无访问时为 0 |

**规则**：

- `struct` 值语义，调用方拿到副本改不了内部。
- **字段只增不删**（DebugOverlay 可能引用旧字段，删字段静默断链）。
- `CacheHitRate` 是冗余字段（可由两个计数算出），冗余的理由是消费方只想显示一个数；计算集中在 DataMetrics，消费方不做除法。
- **未 Init 时也安全**：返回零值快照，DebugOverlay 在装配完成前调用不会崩。

**为什么是"拉"不是"推"**：观测不需要事件语义。"能用日志和版本库回答的事不加脚本"——观测用拉，不用推。`GetSnapshot()` 本身不分配（struct，无 List），每帧调用开销可忽略。

---

## 6. 协作关系

```
┌──────────────────────────────────────────────────────────┐
│  调用方（Logic 或 Presentation）                          │
│  ① var weapon = ConfigModule.GetWeapon(id)               │
│  ② var key    = weapon.Icon                              │
│  ③ var handle = AssetModule.LoadAsync<Sprite>(key)       │
│  ④ var sprite = await handle                             │
│  ⑤ ...使用 sprite...                                      │
│  ⑥ AssetModule.Release(key)                              │
└──────────────────────────────────────────────────────────┘
                    │ 直连查询（不是跨层通信）
                    ▼
┌──────────────────────────────────────────────────────────┐
│  Data 层                                                  │
│  ConfigModule                    AssetModule             │
│  ┌─────────────┐                ┌──────────────────┐     │
│  │TablesHolder │                │ AssetRegistry    │     │
│  │ (查询转发)   │                │ CacheStore       │     │
│  │StartupValid │                │ RefCounter       │     │
│  └─────────────┘                │ LoadScheduler    │     │
│         │                       │ LifecycleMgr     │     │
│         │                       │ FailureHandler   │     │
│         │                       └──────────────────┘     │
│         └────────┬───────────────────────┘               │
│                  ▼                                        │
│           DataMetrics（只读快照，供 DebugOverlay）         │
└──────────────────────────────────────────────────────────┘
```

**关键边界**：

- ConfigModule 与 AssetModule **零类型依赖**，不互相调用（衔接由调用方完成）。`AssetModule` 只接收字符串 Key，**不知道 Luban 存在**。
- DataMetrics 只读两个 Module 的状态，**不被任何 Module 调用**。
- Data 层与任何层之间**没有 EventBus 边**。

---

## 7. 缺漏项决策（D1 / D2 / D3）

决策基于 Jam 场景：15 天有效开发、3 人团队、资源量小、内存压力低。

| # | 缺口 | 决策 | 理由 |
| :-- | :-- | :-- | :-- |
| **D1** | 重复入队保护 | **修补** | Jam 期会踩到（UI 快速开关、多 Actor 同 Key 请求），修补成本低 |
| **D2** | `Resources.UnloadUnusedAssets` 触发 | **修补**（淘汰后触发） | 成本极低；不补则"淘汰"名不副实，量产必踩 |
| **D3** | 取消未完成异步加载 | **不修补** | Jam 期队列短（并发 4），浪费可忽略；修补成本中等（要改 `LoadRequest` 结构） |

### D1：重复入队保护

**方案**：`AssetModule` 内 pending 字典 `Dictionary<string, List<Action<UnityEngine.Object>>>`。

**为什么不是"CacheEntry 加 loading 标志"**：会改动 `CacheEntry` 结构与 `CacheStore.Put` 签名，牵扯面更大。
**为什么不是"LoadScheduler 去重"**：Scheduler 不知道 handle 的存在，合并回调必须挂在 AssetModule 层。

**要点**：

| 时机 | 动作 |
| :-- | :-- |
| `LoadAsync` 未命中缓存 | 先查 pending：命中 → 把本次回调挂到列表并返回新句柄，**不重复入队**；未命中 → 建列表、写入、入队 |
| `onDone` | `Put` 缓存 → 遍历列表逐一 `Retain` + `Complete` → 从 pending 移除 |
| `onFail` | `RecordFailure` → 遍历列表逐一 `Complete(fallback)` → 从 pending 移除 |
| `Dispose` | 清空 pending |
| `OnSceneSwitch` | **不动** pending（与"不清挂起请求"一致，可能是新场景的预加载） |

**已知边界（记录，不处理）**：同 Key 不同类型（`LoadAsync<Sprite>("icon")` 与 `LoadAsync<Texture2D>("icon")`）会被合并为同一请求。Jam 期假设"同路径即同类型"；若将来需要，在 Key 里加类型后缀。

### D2：淘汰后触发 `UnloadUnusedAssets`

**方案**：`LifecycleMgr.EvictLRU` 的 `for` 循环结束后，若本轮 `toEvict > 0` 则调用一次。

**为什么不是"每次淘汰后触发"**：一次 `Tick` 最多淘汰 8 个，逐个触发会产生多次调用。
**为什么不是"切场景触发"**：`OnSceneSwitch` 只标记 `canEvict`，真正的淘汰在下一帧 `Tick`。在 `OnSceneSwitch` 里调会跑在淘汰之前，回收不到东西。
**为什么必须补**：`EvictLRU` 只做 `Remove(key)`，解除的是我们的引用；`CacheEntry.asset` 若无其他引用会成为"未引用资源"，等 Unity 主动 GC。Unity 的自动 GC 时机不可控。

**已知边界**：`UnloadUnusedAssets` **返回 `AsyncOperation`，是一次耗时的全量扫描，可能造成帧尖峰**；本项目不 `yield` 等它完成（协程会打破"GameRoot 唯一驱动"的约束），靠"单帧最多淘汰 8 条 + 只在真的淘汰后触发"限流。若 Profiler 显示尖峰超标，改造点：改为按 N 帧节流或改用 `Addressables` 的按引用释放。

### D3：不取消未完成加载

| 维度 | 说明 |
| :-- | :-- |
| 触发概率 | 低——队列短，面板开关间隔通常长于加载时长 |
| 不补的后果 | 浪费一次 IO 与反序列化；队列长时可能阻塞后续请求 |
| 修补成本 | 中——`LoadRequest` 加取消标志、`Tick` 启动前检查、要处理"已启动但未完成"的路径 |
| 权衡 | 成本 > Jam 期收益 |

**重评条件**（任一满足即重开）：`DataMetrics` 显示 `LoadingCount` 持续 > 10；Profiler 显示 `Resources.LoadAsync` 等待成为瓶颈；队列长度经常触及并发上限。

---

## 8. ADR：不吸收的旧模式（`ResMgr` / `MonoMgr` / `BaseManager`）

**前提修正（并轨 FY 之后）**：`ResMgr.cs` / `MonoMgr.cs` **就在本工程里，而且正在服务 UI 层**——
`UIMgr` 用 `ResMgr.Instance.Load<GameObject>("ui/UICamera")` 造 UI 三件套（`UIMgr.cs:80/85/98`），
`ResMgr` 又用 `MonoMgr.Instance.StartCoroutine` 跑协程（`ResMgr.cs:133/150/…`）。
所以下面这张表不是"要不要从旧工程搬进来"，而是"**Data 层要不要复用它现有的实现**"——答案是不复用。

| 模式 | 不吸收理由 |
| :-- | :-- |
| `MonoMgr` 全局 Update 分发 | 与"只有组合根能开每帧入口"冲突，诱导绕过时序约束 |
| `BaseManager<T>` 继承 | 反射私有构造 + 失败静默（见蓝图 §16 D20）；Data 层用 `static class` + 显式 Init |
| 协程做异步加载 | 加载状态散在 `IEnumerator` 里不可观测；与 `AsyncHandle` 设计冲突 |
| `UnityAction` 回调 API | 回调地狱；错误路径不清晰 |
| 同步加载 API | 已定只提供异步 |
| `UnloadAsset` 的 `isSub` / `isDel` 双开关 | 把"何时释放"的决策权交给调用方，违反"Data 层自动管理生命周期"的定位 |
| Key = `path + "_" + typeof(T).Name` | 把类型编进 Key，同路径不同类型无法共享条目；且 `ResMgr.LoadAsync` 会在末尾**无条件重复启一次协程**（`ResMgr.cs:150`，见蓝图 §16 D5） |

**与 UI 层的边界**：`ResMgr` 继续服务 `UIMgr`，`AssetModule` 不接管它。两者共用 `Assets/Resources/` 这个根，
但**缓存表与引用计数各自独立**——同一份资源经由两条路径加载会出现两份计数。
UI 何时切到 `AssetModule` 是独立决策（见蓝图 §15）。

**经对比确认正确的 5 点（记录为设计依据）**：

| # | 设计点 | 旧代码做法 | 当前设计 | 结论 |
| :-- | :-- | :-- | :-- | :-- |
| V1 | 引用归零后的卸载时机 | `isDel` 由外部传入 | 冷却期自动管理 | 当前更好——调用方不需要思考"什么时候该删" |
| V2 | 每帧驱动入口 | `MonoMgr.AddUpdateListener`，任何类都能挂 | 组合根统一驱动（`GameRoot` 的 `List<IService>` ＋ `PlayerController.FixedUpdate`） | 当前更好——时序可推理。但 `MonoMgr` 仍在为 UI 层服务，未删 |
| V3 | 异步加载返回形态 | `UnityAction<T>` 回调 | `AsyncHandle<T>` + await | 当前更好——无回调地狱，异常路径清晰 |
| V4 | Key 的构成 | `path + "_" + typeof(T).Name` | 纯字符串 Key，类型由泛型参数指定 | 当前更好——同路径不同 T 可共享条目 |
| V5 | 同步 / 异步混合 | `ResMgr.Load` 发现加载中则停协程改同步 | 只提供异步 | 当前更好——避免迁就同步 API 的补丁 |

---

## 9. 资源 Key 契约（🔴 本设计的接口核心）

> 草案这里有一处会导致**运行时加载不到资源**的假设错误，已修正（F3）。

### 9.1 两套路径语义必须分清

| 语境 | 基准目录 | 扩展名 | 谁来保证 |
| :-- | :-- | :-- | :-- |
| **配置表里写的** | `Assets/` | **有**（`.asset` / `.png`） | Luban `#path=unity` + `pathValidator.rootDir = Assets`，**导表期**校验资源真实存在 |
| **`Resources.LoadAsync` 要的** | `Assets/Resources/` | **无** | 运行时由 `AssetRegistry.ResolvePath` 转换 |

`LubanProject.PathValidatorRoot` 实测就是 `Application.dataPath`，所以表里的 `icon` 值形如 `"Settings/Renderer2D.asset"`（相对 `Assets/`，带扩展名）。

### 9.2 转换规则

```
"Assets/Resources/Icons/weapon_wood.png"  ┐ 去掉 "Assets/Resources/" 前缀
"Icons/weapon_wood.png"                   ├ 去掉扩展名
"Icons/weapon_wood"                       ┘ → "Icons/weapon_wood"
```

**为什么保留表里的完整路径而不是直接写 Resources 相对路径**：`#path=unity` 让**导表阶段**就能发现"图标文件不存在"，错误在策划改表时暴露，而不是运行时黑屏。运行时多做一次字符串处理是划算的。

### 9.2.1 本契约的盲区（已实测）

`#path=unity` 只校验「文件在 `Assets/` 下**存在**」，**不校验「在 `Assets/Resources/` 下」**——
而 `Resources.LoadAsync` 只能加载 `Assets/Resources/` 里的东西。两者不一致时：**导表期通过，运行时失败**。

现状正好就是不一致的：`demo_tbweapon.json` / `demo_tbfish.json` 的 `icon` 全部是 `"Settings/Renderer2D.asset"`，
文件确实存在（`Assets/Settings/Renderer2D.asset`），但既不在 `Resources/` 下，也不是 Sprite。

外部 harness 实测链：

```
ResolvePath("Settings/Renderer2D.asset")  →  "Settings/Renderer2D"
Resources.LoadAsync("Settings/Renderer2D")  →  null（不是 Resources 资源）
→ 重试 2 次 → 降级为 fallback / null
```

**结论**：表里的资源路径必须同时满足"存在"与"在 `Assets/Resources/` 下"两个条件，
第二个条件目前**没有任何自动校验**。补法二选一（都不在本次范围，登记在蓝图 §16 D4）：
① 导表期加一条 `Assets/Resources/` 前缀校验；② 把表值改成 Resources 形态。

### 9.3 配套要求

| # | 要求 |
| :-- | :-- |
| 1 | 可加载资源放 `Assets/Resources/` 下。**该目录现已存在**（`ui/{Canvas,EventSystem,UICamera}.prefab`、`ui/Panel/BeginPanel.prefab`，由上游 FY 带入）——旧版本这里写的"仓库还没有这个目录"已过时 |
| 2 | `Assets/Scripts/Config/` 与 `Assets/StreamingAssets/Luban/` 是**生成物专用目录**，禁止放手写文件 |
| 3 | `Assets/Scripts/Framework/Input/InputSys.cs` 是**第三处生成物**（由 `.inputactions` 生成），同样禁止手写 |
| 4 | 切换 Addressables 时只改 `AssetRegistry.ResolvePath` 一个方法 |

---

## 10. 待验证项与开放项

### 10.1 待验证项（每条给验证方法）

| # | 项 | 影响 | 验证方法 | 置信度 |
| :-- | :-- | :-- | :-- | :-- |
| 1 | `Resources.LoadAsync(path, typeof(UnityEngine.Object))` 是否返回正确类型（`Preload` 用基类做类型过滤） | `Preload` 是否要改成泛型 | EditMode 测试：造一个 `Assets/Resources/_probe/` 下的 Sprite，`Preload` 后 `TryGet<Sprite>` | 中——Unity 文档未明确基类类型过滤行为 |
| 2 | `AsyncHandle.Completed` 的 GC 开销 | 命中路径是否要加同步捷径 | Profiler ▸ GC Alloc，对比命中/未命中各 1000 次 | 高——一次对象分配必然产生，量级待测 |
| 3 | Android / WebGL 的 `StreamingAssets` 读取方式 | 平台兼容 | 见 A2 假设；真机 `File.ReadAllText` 读 `Application.streamingAssetsPath` | 高——Android 上 StreamingAssets 在 APK 内，`File` API 读不到 |
| 4 | URP 下占位 Sprite 的构造方式 | `RegisterFallback<Sprite>` 的样本代码 | `Sprite.Create` + `Texture2D` 在 URP 2D 下的显示 | 中 |
| 5 | `Resources.UnloadUnusedAssets` 的帧尖峰量级 | 是否要节流 | Profiler ▸ 淘汰一帧的耗时，缓存 100 条时 | 中 |

### 10.2 开放项（常量与局部实现，不阻塞当前阶段）

| # | 开放项 | 当前值 | 备注 |
| :-- | :-- | :-- | :-- |
| O1 | `COOLDOWN_SECONDS` | 60s | 实操后调整 |
| O2 | `MAX_CACHE_ENTRIES` | 100 | 条目数，不是内存量 |
| O3 | `MAX_CONCURRENT_LOAD` | 4 | — |
| O4 | `MAX_EVICT_PER_TICK` | 8 | 防卡帧 |
| O5 | 降级资源的具体内容 | 由业务注册 | Data 层不创建资源 |
| O6 | DataMetrics 快照刷新频率 | 每帧拉一次 | 消费方决定 |
| O7 | 延时重试（0.5s） | **未采纳** | 需要时改 `LoadScheduler.HandleFailure` |
| O8 | `AssetKey` 强类型 | 未引入 | 当前用 string，未来可评估 |
| O9 | 平台分支（Android/WebGL 异步启动） | 未支持 | 见 §11 A2 |
| O10 | `ConfigModule` 的重置入口 | **缺失** | `Init` 重复调用会抛，而它没有 `Shutdown`／`Reset`。测试只能靠 Domain Reload；"回主菜单重开"也需要它 |
| O11 | `LoadScheduler.Tick(float dt)` 的 `dt` | 未使用 | 将来做超时或延时重试才用得上 |
| O12 | `LifecycleMgr._cooldownSeconds` | 赋值未读 | 冷却期实际读的是 `CacheEntry.cooldownUntil`；该字段是冗余的 |
| O13 | `Preload` 的类型过滤语义 | 待澄清 | 用 `typeof(UnityEngine.Object)` 加载，`TryGet<Sprite>` 是否命中取决于条目里的真实类型，不取决于请求类型 |

---

## 11. 与草案的事实校正记录

| # | 草案原假设 | 仓库事实（出处） | 本文件的处理 |
| :-- | :-- | :-- | :-- |
| **F1** | `TablesHolder` 用 `Newtonsoft.Json.Linq.JObject`，`new cfg.Tables(file => LoadJson(...))` 返回 `JObject` | 生成目标是 `cs-simple-json`，构造签名是 `Func<string, JSONNode>`（`Assets/Scripts/Config/Tables.cs`） | 改 `Luban.SimpleJSON` + `JSON.Parse`；`-c cs-simple-json` 记为不可单方面变更的管线契约 |
| **F2** | 命名空间 `Jam.Data` / `Jam.Config`，程序集"两级：Jam.Config 独立" | 生成代码已在默认程序集 `Assembly-CSharp`；工程共 4 个 asmdef（`Assets/**/*.asmdef`） | 命名空间统一 `DeepseaOil.Data`；程序集一节改为事实描述（见蓝图 §8）；Jam 期不给生成物划 asmdef |
| **F3** | `AssetRegistry.ResolvePath(key) => key`，注释写 Key = `Assets/Resources/` 相对路径不带扩展名 | 表里填的是相对 `Assets/` 带扩展名的路径（`Assets/StreamingAssets/Luban/demo_tbweapon.json` 实测 `"Settings/Renderer2D.asset"`） | §9 定义双约定 + 转换规则；`ResolvePath` 成为唯一转换点 |
| **F4** | `DataMetrics.TableCount = ConfigModule.Tables.TableCount`；占位 `return 0` | `cfg.Tables` 无 `TableCount` 属性，生成物不可手改（`Assets/Scripts/Config/demo/TbWeapon.cs`） | 改用手写 `TablesMeta.Names` 清单（§3.6） |
| **A1** | 假设命名空间前缀可改为 `DeepseaOil.*` | 工程既有手写代码用 `DeepseaOil.Config` / `DeepseaOil.EditorTools` | 沿用；若团队坚持 `Jam.*`，全局替换即可，结构不变 |
| **A2** | 假设 Jam 期按桌面端（Windows/Mac）同步启动 | `ConfigModule.Init` 在 `Awake` 同步读 `StreamingAssets`，Android/WebGL 读不到 | 写进蓝图 §10.3 与契约表「平台」一行：移动端要异步化，会牵动整条启动链 |
| **A3** | 假设蓝图保持"契约 + 结构"密度，完整代码另存 | — | 见 `Data 层实现.md` |

### 11.1 并轨上游 FY 之后的补充校正

上面 F1~F4 是针对**草案**的校正。并轨 FY（合并后 `932bac5`）之后，三份文档里又有一批"当时写得像事实、其实不是"的表述，
逐条列在下面。**缺陷级**的登记在蓝图 §16，这里只列**文档表述**层面的校正。

| # | 原表述（位置） | 仓库事实（出处） | 本文件的处理 |
| :-- | :-- | :-- | :-- |
| **F5** | 蓝图把 `GameRoot` + `FrameDriver` 画在**逻辑层** | `GameRoot` 的命名空间是 `DeepseaOil.Presentation`（`Framework/GameRoot.cs`）；且没有 `FrameDriver` 这个类型 | 蓝图 §1 改为"按命名空间判层"，`GameRoot` 归 Presentation 并标为组合根 |
| **F6** | 蓝图 §10.1「`Framework/` …（当前为空）」，实现文档 §3 写「已存在且为空」 | `Assets/Scripts/Framework/` 有 **35 个 `.cs`**（`Event/`、`Logic/`、`Singleton/`、`Context/`、`Input/`、`Character/`…） | 蓝图 §10.1 目录树按事实重写 |
| **F7** | 本文档 §9.3「当前仓库还没有 `Assets/Resources/` 目录」 | 该目录已存在：`ui/{Canvas,EventSystem,UICamera}.prefab`、`ui/Panel/BeginPanel.prefab` | §9.3 要求 1 改事实措辞 |
| **F8** | 本文档 §8「`ResMgr.cs` / `MonoMgr.cs` 来自旧工程，**不进本工程**」 | 两个文件在本工程且在服务 UI 层（`UIMgr.cs:80/85/98` → `ResMgr`；`ResMgr.cs:133/150` → `MonoMgr`） | §8 前提改写为"不复用其实现"，并明确与 UI 层的边界 |
| **F9** | 蓝图 §9 图 9 用 `sortingOrder 100/200/300/400/500`，契约表写"`sortingOrder` 是 `UIManager` 的 const" | 真实层级是 `E_UILayer { Bottom, Middle, Top, System }`（`UIMgr.cs:14-32`），靠 `Canvas.prefab` 四子节点的**兄弟顺序**分层；全类**没有** `sortingOrder`；管理器名是 `UIMgr` | 蓝图 §9 与图源 `07-panel-order.mmd` 整个重画；契约表改口 |
| **F10** | 蓝图 §3/§3.1 用 `tickables[]` 六步、`PoolManager.Init()`、`UIManager.Init()`、`DebugOverlay.Init()`、`Actors.Tick` | `GameRoot` 实际是 `List<IService>`（`GameRoot.cs:15`），`Start()` 里 `Init` 三个 Service；`Pool<T>` 是**实例级**泛型池、`UIMgr` 无 `Init()`、没有 `DebugOverlay` | 蓝图 §3.1 改为"两阶段装配"，§3.2 调用链按真实代码重写 |
| **F11** | 蓝图隐含"单一每帧入口 + 单一 Tick 抽象" | 两套并存：`IService.Tick(float)`（真被调用）与 `ITickable`/`IFixedTickable`（带默认空实现；`ITickable` 零调用，`IFixedTickable` 从不经接口分发） | 蓝图 §15.2 单列一节说明 |
| **F12** | 蓝图 §5 图 3：`SwitchState<T>` 只标记、下帧 `PerformTransition`、同帧上限 3、`CheckGlobalTransitions`、`OnUpdate` | `StateMachine<TStateTag>.ChangeState` **立即** `Exit → Enter` 并返回 `bool`；转移由 `MoveGroup.CheckTransitions` 两段仲裁（纯查询 → 消费余额）；`IState.Tick` 不叫 `OnUpdate`；没有上限 3，也没有全局转移 | 蓝图 §5 与图源 `03-state-machine.mmd` 整个重画 |
| **F13** | 蓝图 §3 把 `InputProvider.Update` + `InputBuffer.Push` 放进 `GameRoot.Update` 列表，硬序写"必须先于 `Actors.Tick`" | `Push` 发生在 `PlayerController.FixedUpdate` 内（`PlayerController.cs:83`）；真实硬序是**同一物理帧内** `Push` 先于 `PlayerLogic.FixedTick`（`:88`） | 蓝图 §3 图源拆成 Update / FixedUpdate 两条通道 |
| **F14** | 蓝图按**目录**判层（`Framework/Logic/`＝逻辑层） | 目录与命名空间不是一一对应：`Framework/Character/Player/` 里也有 `MovementDebugPanel`（Presentation）；`SceneService` 在**全局命名空间** | 蓝图 §15.1 给出命名空间归属表，并写明"判层看命名空间" |
| **F15** | 蓝图 §1.1「Presentation → Logic ❌ 唯一例外：通过 EventBus」 | `PlayerController`（表现层）直连 `PlayerLogic.FixedTick`；`MovementMotor`/`GameTime` 实现逻辑层端口后注入 | §1.1 增加"组合根直连"与"端口反转"两条允许项（ADR 层面见蓝图 §15.5） |
| **F16** | 蓝图 §1 只提一条 `Singleton<T>`，并称 Services 用它 | 两条基类并存：`Singleton<T> : MonoBehaviour`（`ResMgr`/`MonoMgr`）与 `BaseManager<T>`（反射非公开私有构造，`UIMgr`/`ConfigController`）；三个 Service **不是**单例 | 蓝图 §1 改为两条基类的事实表 |
| **F17** | 各处引用 `Docs/M1微规划.md`、`Docs/架构约束.md`、`Docs/速度设计.md` | 这三份文件**从未在任何提交里存在过**（`git log --all` 无记录）；逻辑层的几条关键约束只在代码注释里 | 蓝图 §15.7 登记引用清单，约束的事实版本收进 §15.2/§15.4 |
| **F18** | `IMovementMotor.cs:8` 注释写"实现位于 `Dasuus.Presentation`" | 实际命名空间是 `DeepseaOil.Presentation`（`MovementMotor.cs:4/15`） | 本次直接修了 `IMovementMotor.cs`；`EventBusDebugPanel.cs:10` 同样问题登记在蓝图 §16 D22 |
| **F19** | 蓝图铁律 3 只列两个生成物目录 | `Assets/Scripts/Framework/Input/InputSys.cs`（1957 行）由 `InputSys.inputactions` 生成，文件头写明手改会被丢弃 | 铁律 3 扩为三处；§9.3 要求 3 同步 |
| **F20** | 蓝图契约表「配置源」隐含"配置只有 Luban 一条路" | 还有一条遗留 CSV 路径 `ConfigController`/`ConfigData`（`Framework/ConfigController.cs`，全局命名空间 + 反射单例 + `Resources.Load<TextAsset>("config/…")`），**全仓库零调用**，且 `Assets/Resources/config/` 不存在 → 一旦调用必 NRE | 蓝图契约表加注；缺陷登记为 §16 D11 |
| **F21** | （未写过）测试怎么放 | asmdef 程序集**无法**引用预定义程序集 `Assembly-CSharp`；`DeepseaOil.EditorTools.Tests` 只引 `DeepseaOil.EditorTools`，所以 `Assets/Tests/EditMode/` 下的测试**看不到 `DeepseaOil.Data`** | 蓝图 §10.2 增加"测试可达性"一节；Data 层测试放 `Assets/Tests/Editor/`（落 `Assembly-CSharp-Editor`） |

**冲突裁定记录**：C1 重试机制（立即重试）见 §4.3-2.4；C2 `SemaphoreSlim` → 计数器见 §4.3-2.4；C3 `RunContinuationsAsynchronously` 去掉见 §4.6；C4 `Complete` 改 `TrySetResult` 见 §4.6；C5 pending 列表在切场景时的语义见 §7-D1；C6 复位三项 → 四项见蓝图 §7。
C7：蓝图"每帧只由 `GameRoot` 统一顺序表驱动"→ 改为"两个组合根入口"（`GameRoot.Update` + `PlayerController.FixedUpdate`），理由见 F11/F13。
