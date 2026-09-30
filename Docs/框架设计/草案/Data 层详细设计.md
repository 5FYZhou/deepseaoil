# Data 层详细设计 · 轻量智能版

以下设计把 Data 层拆到微块粒度，明确每个微块的职责、对外接口、内部状态与协作关系。所有设计与 Luban 方案的相性在每一处都标注。

---

## 一、Data 层的定位

### 一句话定义

**Data 层是游戏内所有静态内容的唯一提供者，回答两类问题：“值是多少？”（同步）和“东西在哪？”（异步）。**

### 它不是什么

| 不是           | 原因                                             |
| -------------- | ------------------------------------------------ |
| 不是 Service   | 没有业务状态，不参与游戏逻辑                     |
| 不是缓存中心   | 缓存是 AssetModule 的内部实现，不是对外概念      |
| 不是事件中心   | 不走 EventBus，不发布订阅                        |
| 不是业务数据仓 | 不存运行时状态（存档、进度），那些归 SaveService |

### 它是什么

| 是                     | 表现                                          |
| ---------------------- | --------------------------------------------- |
| 静态内容的只读查询接口 | 输入 Key → 输出值或资源                       |
| 进程级常驻             | 启动装配，运行时不释放                        |
| 被所有层直连           | Logic 和 Presentation 都可以调，不走 EventBus |
| 零业务依赖             | 不知道谁在调、为什么调                        |

---

## 二、Data 层的微块总览

```
Data 层
│
├── ① ConfigModule           数值配置查询
│   ├── 1.1 TablesHolder     持有 Luban Tables 实例
│   ├── 1.2 QueryAccessor    类型化查询入口
│   └── 1.3 StartupValidator 启动时全量校验
│
├── ② AssetModule            资源查询与生命周期
│   ├── 2.1 AssetRegistry    Key → 资源元数据
│   ├── 2.2 CacheStore       Key → CacheEntry
│   ├── 2.3 RefCounter       引用计数
│   ├── 2.4 LoadScheduler    并发控制 + 加载队列
│   ├── 2.5 LifecycleMgr     冷却期 + LRU 淘汰
│   └── 2.6 FailureHandler   重试 + 降级
│
└── ③ DataMetrics            可观测性表面
    ├── 3.1 CounterSet       缓存数 / 加载中数 / 命中率
    └── 3.2 Snapshot         只读快照，给 DebugOverlay
```

**为什么是三个微块而不是两个**：ConfigModule 和 AssetModule 是服务提供者，DataMetrics 是**观测表面**。它不属于任何 Module，但属于 Data 层——因为只有 Data 层知道自己的内部状态。把它塞进 AssetModule 会让 AssetModule 承担“被观测”的职责，职责不纯。

---

## 三、微块 ① ConfigModule

### 职责边界

| 负责                            | 不负责                                           |
| ------------------------------- | ------------------------------------------------ |
| 持有 Luban 生成的 `Tables` 实例 | 不解析 Excel（那是 Luban 工具的事）              |
| 提供类型化查询入口              | 不做数值计算（那是 Logic 的事）                  |
| 启动时校验数据完整性            | 不缓存查询结果（Luban Tables 本身已是内存结构）  |
| 暴露表级访问                    | 不感知资源（资源引用是字符串 Key，不是资源本体） |

### 对外服务接口

```
// 类型化查询（薄封装，Luban 已生成）
Weapon GetWeapon(int id)
Item   GetItem(int id)
IReadOnlyList<Weapon> GetAllWeapons()

// 原始访问（逃生舱，特殊情况用）
cfg.Tables Tables { get; }

// 生命周期
void Init()      // 启动时调用一次
bool IsReady     // 供调用方判断
```

**设计要点**：

- **薄封装**：不重新发明 Luban 的查询 API，只在它之上加一层统一入口。`ConfigModule.GetWeapon(id)` 内部直接转发 `tables.TbWeapon.Get(id)`。
- **逃生舱**：`Tables` 属性暴露原始实例，特殊情况（如需要遍历所有表）可以直接访问，不强制走封装。
- **无状态**：ConfigModule 自己不存任何数据，所有数据在 Luban Tables 里。

### 内部微块

| 微块                     | 职责                                                    | 状态     |
| ------------------------ | ------------------------------------------------------- | -------- |
| **1.1 TablesHolder**     | 持有 `cfg.Tables` 实例，负责 Init 时构造                | 一个字段 |
| **1.2 QueryAccessor**    | 类型化查询方法集合（大部分由 Luban 生成，少量手写转发） | 无状态   |
| **1.3 StartupValidator** | Init 时调用 Luban 的校验；失败则抛异常，阻止游戏启动    | 无状态   |

**1.3 的边界**：Luban 的 `--strict` 模式让校验失败时进程退出。ConfigModule 的 Init 应该调用一次显式的校验（或在加载时依赖 Luban 已经校验过的产物），**确保带病数据不会进入运行时**。这是“启动时失败优于运行时失败”原则的体现。

### 与 Luban 的相性

| Luban 特性              | ConfigModule 如何用                                     |
| ----------------------- | ------------------------------------------------------- |
| 生成的 `Tables` 类      | TablesHolder 持有                                       |
| 生成的 `TbXxx` 表访问器 | QueryAccessor 薄封装                                    |
| ref 校验                | 启动时由 Luban 完成，ConfigModule 不重复                |
| path 校验               | 启动时由 Luban 完成；资源路径的运行时解析归 AssetModule |
| OOP 继承                | 生成的基类/子类直接可用，ConfigModule 不做特殊处理      |
| JSON 数据文件           | Init 时通过 Luban 的 Loader 读取                        |

**关键边界**：**ConfigModule 不负责加载 JSON 文件**。加载 JSON 是 Luban 的 `Tables` 构造函数的工作，ConfigModule 只负责在正确的时机调用它、持有它、暴露它。

---

## 四、微块 ② AssetModule

### 职责边界

| 负责           | 不负责                                              |
| -------------- | --------------------------------------------------- |
| 异步加载资源   | 不解析配置（Key 从调用方来）                        |
| 引用计数管理   | 不决定“何时该释放”（调用方决定）                    |
| 冷却期缓存     | 不管理 GameObject 生命周期（那是 PoolManager 的事） |
| 并发控制       | 不做资源分组（Jam 阶段单资源为原子单位）            |
| LRU 淘汰       | 不感知场景（SceneService 通过 OnSceneSwitch 通知）  |
| 失败重试与降级 | 不弹窗、不阻塞（失败写日志）                        |

### 对外服务接口

```
// 主路径：异步加载
AsyncHandle<T> LoadAsync<T>(string key) where T : UnityEngine.Object

// 观测路径：查缓存（不触发加载）
bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object

// 显式释放
void Release(string key)

// 预加载（标记为常驻，不增加调用方引用计数）
void Preload(string key)

// 生命周期
void Init()
void Tick(float dt)          // 由 GameRoot 每帧驱动
void OnSceneSwitch()         // 由 SceneService 切场景时调用
void Dispose()               // 进程退出时调用
```

### 内部微块详解

#### 2.1 AssetRegistry

| 维度     | 内容                                                         |
| -------- | ------------------------------------------------------------ |
| **职责** | 把字符串 Key 解析为“加载所需的信息”                          |
| **输入** | `string key`（如 `"weapon.wood.icon"` 或 `"Assets/Icons/wood.png"`） |
| **输出** | 资源元数据（路径 / 类型 / 是否预加载 / 失败回退 Key）        |
| **实现** | Jam 阶段：Key 直接是路径，Registry 是空壳；量产：可切换为 SO 映射表 |
| **状态** | 只读，Init 时建好，运行时不改                                |

**为什么需要一个空壳 Registry**：让“Key 到路径”的解析有一个明确的挂载点。Jam 阶段它什么都不做，但未来的 SO 映射表、Addressables 地址表可以直接挂进来，不需要改 AssetModule 的其他部分。

**置信度：中**。如果团队认为“空壳 Registry”是过度设计，可以砍掉，让 Key 直接等于路径。代价是未来切换映射表时改动面更大。

#### 2.2 CacheStore

| 维度                | 内容                                                         |
| ------------------- | ------------------------------------------------------------ |
| **职责**            | 持有所有已加载资源的条目                                     |
| **结构**            | `Dictionary<string, CacheEntry>`                             |
| **CacheEntry 字段** | `handle`（底层句柄）、`refCount`、`lastAccessTime`、`isPreloaded`、`cooldownUntil` |
| **访问**            | 主线程独占，无锁                                             |

**CacheEntry 的状态机**：

```
[未加载] --LoadAsync--> [加载中] --完成--> [已缓存, refCount >= 1]
                          |
                          +--失败--> [降级, 返回占位资源]

[已缓存, refCount >= 1] --Release--> [已缓存, refCount == 0]
                                        |
                                        +--冷却期--> [可淘汰]
                                        |
                                        +--再 LoadAsync--> refCount++（命中，无 IO）
```

**关键设计**：**refCount == 0 不等于立即卸载**。它进入冷却期，冷却期结束才标记为“可淘汰”，LRU 在此时才可能真正释放它。

#### 2.3 RefCounter

| 维度                     | 内容                                                       |
| ------------------------ | ---------------------------------------------------------- |
| **职责**                 | 管理每个 CacheEntry 的引用计数                             |
| **规则**                 | `LoadAsync` 成功 = +1；`Release` = -1；`TryGet` 不改变计数 |
| **边界**                 | refCount 不能为负；Release 不存在的 Key 记警告             |
| **与 CacheEntry 的关系** | RefCounter 是逻辑，CacheEntry.refCount 是数据              |

**为什么独立成微块**：引用计数是“智能”的核心——它决定了资源何时该被卸载。把它独立出来，是为了让它有明确的测试边界（可以单独测 +1/-1 的正确性），而不是散落在 CacheStore 各处。

#### 2.4 LoadScheduler

| 维度       | 内容                              |
| ---------- | --------------------------------- |
| **职责**   | 限制并发加载数，排队超额请求      |
| **机制**   | `SemaphoreSlim`（初始值 = 4）     |
| **队列**   | 超出的请求排队，不丢弃            |
| **优先级** | Jam 阶段 FIFO；量产可加优先级队列 |
| **状态**   | 加载中计数、队列长度              |

**与 AssetModule.Tick 的关系**：Scheduler 的队列推进由 Tick 驱动。Tick 每帧检查队列，若有空位则取出下一个请求。

**为什么需要并发控制**：同时发起 50 个 `Resources.LoadAsync` 会导致 IO 争抢、帧率抖动、GC 峰值。信号量把并发压到可控范围，其他排队。这是“智能”的第二层含义——**调用方不需要自己控制并发，AssetModule 替它控制**。

#### 2.5 LifecycleMgr

| 维度           | 内容                                                         |
| -------------- | ------------------------------------------------------------ |
| **职责**       | 管理冷却期与 LRU 淘汰                                        |
| **冷却期**     | refCount 归零后，条目保留 `COOLDOWN_SECONDS`（常量，默认 60s） |
| **冷却期结束** | 标记 `canEvict = true`                                       |
| **LRU 淘汰**   | 当缓存条目数 > `MAX_CACHE_ENTRIES`（默认 100）或估算内存 > `MAX_CACHE_MB`（默认 64MB）时，淘汰 `canEvict && !isPreloaded` 中最久未访问的条目 |
| **预加载保护** | `isPreloaded = true` 的条目永不被淘汰                        |

**冷却期的意义**：UI 面板反复开关、音效反复播放，是“高频加载”的典型场景。冷却期把“关面板时释放 → 开面板时重新加载”变成“关面板时保留 → 开面板时直接命中”，消除了重复 IO。

**为什么是 60s 而不是更长**：Jam 阶段调试时，60s 足够覆盖大部分“来回切换”的场景；太长会导致内存滞留。这个值是常量，改一行代码就能调。

#### 2.6 FailureHandler

| 维度       | 内容                                                    |
| ---------- | ------------------------------------------------------- |
| **职责**   | 加载失败时的重试与降级                                  |
| **重试**   | 最多 2 次，间隔 0.5s（由 Tick 驱动）                    |
| **降级**   | 重试仍失败 → 返回占位资源（空 Sprite / 静音 AudioClip） |
| **日志**   | 记录 Key、失败原因、重试次数                            |
| **不阻塞** | 不弹窗、不抛异常给调用方                                |

**降级资源的存在**：调用方拿到占位资源后，代码路径不变（`sprite` 仍是 Sprite），只是内容为空。这样 UI 不会因为资源缺失而崩溃。占位资源在 Init 时预加载一次，常驻内存。

### 与 Luban 的相性

| Luban 特性                   | AssetModule 如何用                                 |
| ---------------------------- | -------------------------------------------------- |
| 配置里存资源路径字符串       | 调用方从 ConfigModule 拿到字符串，传给 AssetModule |
| `string#path` 校验           | 启动时校验路径合法性；运行时 AssetModule 只管加载  |
| 生成的 `Weapon.IconKey` 字段 | 调用方直接读取，不需要 AssetModule 感知 Luban      |
| 表间引用                     | 不涉及；资源引用是字符串，不是表引用               |

**关键边界**：**AssetModule 不知道 Luban 存在**。它只接收字符串 Key，加载资源。Key 从哪来（Luban 配置、SO、硬编码）它不关心。

---

## 五、微块 ③ DataMetrics

### 职责边界

| 负责                 | 不负责                      |
| -------------------- | --------------------------- |
| 采集 Data 层内部状态 | 不做逻辑判断                |
| 提供只读快照         | 不写数据                    |
| 供 DebugOverlay 读取 | 不推送事件（不走 EventBus） |

### 对外服务接口

```
// 只读快照
DataSnapshot GetSnapshot()

struct DataSnapshot {
    // AssetModule 状态
    int CachedAssetCount;
    int LoadingCount;
    int QueuedCount;
    float CacheHitRate;        // 命中率
    int EvictedCount;          // 累计淘汰数
    int FailedCount;           // 累计失败数

    // ConfigModule 状态
    int TableCount;            // 已加载表数
    bool ConfigReady;
}
```

**为什么不做成事件**：DataMetrics 是“拉”模型，不是“推”模型。DebugOverlay 每帧或每 N 帧拉一次快照，不需要事件。这符合你“能用日志和版本库回答的事不加脚本”的约束——**观测用拉，不用推**。

**为什么放在 Data 层而不是 DebugOverlay 内**：只有 Data 层知道自己的内部状态。DebugOverlay 只是消费方，不应该反向依赖 Data 层的内部结构。DataMetrics 是 Data 层的“对外观测表面”，DebugOverlay 调 `DataMetrics.GetSnapshot()` 即可。

---

## 六、Data 层内部协作关系

```
┌──────────────────────────────────────────────────────────┐
│  调用方（Logic 或 Presentation）                          │
│                                                          │
│  ① var data = ConfigModule.GetWeapon(id)                 │
│  ② var key = data.IconKey                                │
│  ③ var handle = AssetModule.LoadAsync<Sprite>(key)       │
│  ④ var sprite = await handle                             │
│  ⑤ ...使用 sprite...                                      │
│  ⑥ AssetModule.Release(key)                              │
└──────────────────────────────────────────────────────────┘
                    │
                    │ 直连查询
                    ▼
┌──────────────────────────────────────────────────────────┐
│  Data 层                                                  │
│                                                          │
│  ConfigModule                    AssetModule             │
│  ┌─────────────┐                ┌──────────────────┐    │
│  │TablesHolder │                │ AssetRegistry    │    │
│  │QueryAccessor│                │ CacheStore       │    │
│  │StartupValid │                │ RefCounter       │    │
│  └─────────────┘                │ LoadScheduler    │    │
│         │                       │ LifecycleMgr     │    │
│         │                       │ FailureHandler   │    │
│         │                       └──────────────────┘    │
│         │                                │              │
│         └────────┬───────────────────────┘              │
│                  │                                       │
│                  ▼                                       │
│           DataMetrics                                    │
│           （只读快照，供 DebugOverlay）                   │
└──────────────────────────────────────────────────────────┘
```

**关键边界**：

- ConfigModule 与 AssetModule **零依赖**，不互相调用
- 两者的衔接由**调用方**完成（从 Config 拿 Key，传给 Asset）
- DataMetrics 只读两个 Module 的内部状态，不被任何 Module 调用

---

## 七、Data 层的生命周期

```
启动
  └─ GameRoot.Awake
       ├─ ConfigModule.Init()
       │    ├─ 1.1 TablesHolder 构造 cfg.Tables（加载 JSON）
       │    ├─ 1.3 StartupValidator 校验（Luban 已校验，此处确认）
       │    └─ 若失败 → 抛异常，阻止游戏启动
       │
       ├─ AssetModule.Init()
       │    ├─ 2.1 AssetRegistry 建表（Jam 阶段空壳）
       │    ├─ 2.2 CacheStore 初始化
       │    ├─ 2.4 LoadScheduler 建信号量
       │    ├─ 2.6 FailureHandler 预加载降级资源
       │    └─ 3.1 DataMetrics 初始化计数器
       │
       └─ 同序写入 tickables[]

每帧
  └─ AssetModule.Tick(dt)
       ├─ 2.4 LoadScheduler 推进队列
       ├─ 2.5 LifecycleMgr 检查冷却期 / LRU 淘汰
       ├─ 2.6 FailureHandler 重试计时
       └─ 3.1 DataMetrics 更新快照

切场景
  └─ AssetModule.OnSceneSwitch()
       ├─ 2.5 LifecycleMgr 清空冷却期（防误命中）
       ├─ 2.5 LifecycleMgr 保留 isPreloaded=true 的条目
       └─ 2.5 LifecycleMgr 不强制释放 refCount > 0 的资源

进程退出
  └─ AssetModule.Dispose()
       └─ 2.2 CacheStore 全部释放
```

---

## 八、“智能”体现在哪里

“智能”不是指算法复杂，而是指**调用方不需要思考的事情变多**。Data 层的智能点：

| 智能点           | 调用方不需要思考                   | 内部实现                  |
| ---------------- | ---------------------------------- | ------------------------- |
| **透明缓存**     | “这个资源之前加载过吗？”           | CacheStore 自动命中       |
| **自动生命周期** | “我什么时候该释放？”               | RefCounter 管理引用       |
| **冷却期保留**   | “关了面板再开，资源会重新加载吗？” | LifecycleMgr 冷却期       |
| **并发控制**     | “我一次加载 50 个会不会卡？”       | LoadScheduler 信号量      |
| **自动降级**     | “资源加载失败了我该怎么办？”       | FailureHandler 占位资源   |
| **LRU 淘汰**     | “内存会不会爆？”                   | LifecycleMgr 自动淘汰     |
| **启动时校验**   | “数据是不是对的？”                 | StartupValidator 前置失败 |
| **类型安全**     | “这个字段是什么类型？”             | Luban 生成的强类型        |

**“轻量”体现在哪里**：

| 轻量点     | 表现                                          |
| ---------- | --------------------------------------------- |
| 零外部依赖 | 只有 Luban.Runtime + UnityEngine              |
| 零反射     | Luban 生成代码无反射，AssetModule 也不用反射  |
| 零事件订阅 | 不走 EventBus，调用方直接 await               |
| 零配置文件 | 所有阈值是 const 常量                         |
| 零新程序集 | AssetModule 落 Assembly-CSharp，不新增 asmdef |
| 零业务耦合 | 不知道谁在调、为什么调                        |

---

## 九、与 Luban 方案的完整相性表

| Luban 能力       | Data 层如何衔接                     | 边界                                |
| ---------------- | ----------------------------------- | ----------------------------------- |
| 生成 Tables 类   | ConfigModule.TablesHolder 持有      | 不修改生成的代码                    |
| 生成强类型数据类 | ConfigModule.QueryAccessor 直接返回 | 不做二次封装                        |
| ref 校验         | 启动时由 Luban 完成                 | ConfigModule 不重复校验             |
| path 校验        | 启动时由 Luban 完成                 | 资源路径字符串的合法性由 Luban 保证 |
| OOP 继承         | 生成的基类/子类直接可用             | Data 层不感知继承关系               |
| JSON 数据文件    | Luban 的 Loader 读取                | AssetModule 不参与                  |
| 热更兼容         | 生成代码无反射，可放热更程序集      | AssetModule 无反射                  |
| 二进制/懒加载    | 量产阶段可切换                      | 接口形状不变                        |
| l10n 本地化      | 生成的本地化 Key 直接可用           | Data 层不做本地化                   |

**关键分工**：

- **Luban 负责**：Excel → 强类型代码 + JSON 数据
- **ConfigModule 负责**：持有生成的 Tables，暴露查询入口
- **AssetModule 负责**：把配置里的字符串 Key 解析为资源本体
- **衔接点**：调用方从 Config 拿 Key，传给 Asset

---

## 十、演进路径

| 阶段            | Data 层形态                                                  | 触发条件                     |
| --------------- | ------------------------------------------------------------ | ---------------------------- |
| **Jam（当前）** | ConfigModule 薄封装 + AssetModule 完整实现；AssetRegistry 空壳；Key = 路径字符串 | —                            |
| **首次上线**    | AssetRegistry 切换为 SO 映射表；增加预加载队列；DataMetrics 增加命中率统计 | 资源重命名频繁 或 需要预加载 |
| **量产**        | AssetModule 底层切 Addressables；AssetRegistry 切换为 Addressables 地址表；接口形状不变 | 资源总量 > 200MB 或需要热更  |
| **长线**        | AssetModule 引入分组引用计数；支持按场景释放；DataMetrics 增加内存估算 | 多场景资源复用率高           |

**ConfigModule 不参与演进**——Luban 生成的 Tables 从 Jam 到量产形态不变，只增量加表。

---

## 十一、开放项（供后续详细设计时拍板）

| #    | 开放项                               | 影响                                  |
| ---- | ------------------------------------ | ------------------------------------- |
| O1   | `COOLDOWN_SECONDS` 默认值            | 60s 是起点，实操后调整                |
| O2   | `MAX_CACHE_ENTRIES` / `MAX_CACHE_MB` | 100 / 64MB 是起点                     |
| O3   | `SemaphoreSlim` 初始值               | 4 是起点                              |
| O4   | 降级资源的具体内容                   | 空 Sprite / 静音 AudioClip / 粉色方块 |
| O5   | 是否引入 `AssetKey` 强类型           | 当前用 string，未来可评估             |
| O6   | DataMetrics 快照的刷新频率           | 每帧 / 每 N 帧 / 按需                 |

这些都是 **const 常量或局部实现**，不在当前阶段拍死。

# Data 层伪代码示意

以下伪代码展示每个微块的**职责、状态、对外接口与边界**。不写完整实现，只示意结构。所有 `//` 注释标注调用时机、边界、版本差异。

---

## 一、文件结构概览

```
Assets/Scripts/Data/
├── ConfigModule/
│   ├── ConfigModule.cs           // 对外入口
│   ├── TablesHolder.cs           // 1.1
│   ├── QueryAccessor.cs          // 1.2（部分由 Luban 生成）
│   └── StartupValidator.cs       // 1.3
├── AssetModule/
│   ├── AssetModule.cs            // 对外入口
│   ├── AssetRegistry.cs          // 2.1
│   ├── CacheStore.cs             // 2.2
│   ├── CacheEntry.cs             // 2.2 数据结构
│   ├── RefCounter.cs             // 2.3
│   ├── LoadScheduler.cs          // 2.4
│   ├── LifecycleMgr.cs           // 2.5
│   ├── FailureHandler.cs         // 2.6
│   └── AsyncHandle.cs            // 句柄
└── DataMetrics/
    ├── DataMetrics.cs            // 对外入口
    └── DataSnapshot.cs           // 只读快照结构
```

---

## 二、微块 ① ConfigModule

### 1.0 ConfigModule（对外入口）

```csharp
// 调用时机：GameRoot.Awake 中 Init 一次
// 边界：Init 后只读；运行时不接受任何写操作
public static class ConfigModule
{
    private static TablesHolder holder;
    private static QueryAccessor accessor;

    // 生命周期
    public static void Init(string jsonRoot)
    {
        // 1. 构造 Luban Tables（加载 JSON）
        holder = new TablesHolder(jsonRoot);
        // 2. 启动时校验（Luban 已校验过，此处只确认加载成功）
        if (!StartupValidator.Validate(holder)) {
            throw new ConfigLoadException("Config validation failed");
            // 边界：抛异常阻止游戏启动，不允许带病数据进入运行时
        }
        // 3. 构造查询访问器
        accessor = new QueryAccessor(holder.Tables);
    }

    public static bool IsReady => holder != null;

    // 查询接口（薄封装，转发给 accessor）
    public static Weapon GetWeapon(int id) => accessor.GetWeapon(id);
    public static Item   GetItem(int id)   => accessor.GetItem(id);
    public static IReadOnlyList<Weapon> GetAllWeapons() => accessor.GetAllWeapons();

    // 逃生舱：特殊情况直接访问原始 Tables
    public static cfg.Tables Tables => holder.Tables;
}
```

### 1.1 TablesHolder

```csharp
// 职责：持有 Luban 生成的 Tables 实例
// 状态：一个字段
// 边界：构造后不再变更
internal class TablesHolder
{
    public cfg.Tables Tables { get; }

    public TablesHolder(string jsonRoot)
    {
        // 调用 Luban 的 Loader 读 JSON
        // 版本差异：Luban 5.x 的 Loader 签名以实际版本为准
        this.Tables = new cfg.Tables(file => LoadJsonFromDisk(jsonRoot, file));
    }

    private static JSONNode LoadJsonFromDisk(string root, string file)
    {
        // 读 StreamingAssets/Luban/{file}.json
        // 边界：文件不存在时抛异常，由 Init 捕获
    }
}
```

### 1.2 QueryAccessor

```csharp
// 职责：类型化查询方法集合
// 状态：无状态
// 边界：大部分由 Luban 生成的 TbXxx 提供，这里只做薄转发
internal class QueryAccessor
{
    private readonly cfg.Tables tables;

    public QueryAccessor(cfg.Tables t) { this.tables = t; }

    public Weapon GetWeapon(int id)
    {
        // 转发：tables.TbWeapon.Get(id)
        // 边界：id 不存在时 Luban 会抛异常；调用方需保证 id 有效（ref 校验已保证）
        return tables.TbWeapon.Get(id);
    }

    public Item GetItem(int id) => tables.TbItem.Get(id);

    public IReadOnlyList<Weapon> GetAllWeapons() => tables.TbWeapon.DataList;
}
```

### 1.3 StartupValidator

```csharp
// 职责：启动时确认 Luban 产物完整
// 状态：无
// 边界：只确认"加载成功"，不重复 Luban 的 ref/path 校验
internal static class StartupValidator
{
    public static bool Validate(TablesHolder holder)
    {
        // 1. 确认 Tables 非空
        if (holder?.Tables == null) return false;
        // 2. 抽样确认关键表可访问
        //    （不遍历全部，避免启动耗时）
        try {
            _ = holder.Tables.TbWeapon.DataList;   // 触发访问
        } catch { return false; }
        return true;
    }
}
```

---

## 三、微块 ② AssetModule

### 2.0 AssetModule（对外入口）

```csharp
// 调用时机：
//   Init    — GameRoot.Awake
//   Tick    — GameRoot 顺序表 step ③
//   OnSceneSwitch — SceneService 切场景时
//   Dispose — 进程退出
// 边界：所有方法仅主线程调用（Unity 资源 API 限制）
public static class AssetModule
{
    private static AssetRegistry    registry;
    private static CacheStore       cache;
    private static RefCounter       refCounter;
    private static LoadScheduler    scheduler;
    private static LifecycleMgr     lifecycle;
    private static FailureHandler   failure;

    // 常量（详细设计阶段可调）
    private const float COOLDOWN_SECONDS    = 60f;
    private const int   MAX_CACHE_ENTRIES   = 100;
    private const int   MAX_CACHE_MB        = 64;
    private const int   MAX_CONCURRENT_LOAD = 4;

    public static void Init()
    {
        registry    = new AssetRegistry();
        cache       = new CacheStore();
        refCounter  = new RefCounter(cache);
        scheduler   = new LoadScheduler(MAX_CONCURRENT_LOAD);
        lifecycle   = new LifecycleMgr(cache, refCounter, COOLDOWN_SECONDS, MAX_CACHE_ENTRIES);
        failure     = new FailureHandler();
        failure.PreloadFallbacks();   // 预加载降级资源（空 Sprite 等）
    }

    // 主路径：异步加载
    public static AsyncHandle<T> LoadAsync<T>(string key)
        where T : UnityEngine.Object
    {
        // 命中缓存 → refCount++，立即返回已完成句柄
        if (cache.TryGet<T>(key, out var entry)) {
            refCounter.Retain(key);
            entry.lastAccessTime = Time.realtimeSinceStartup;
            return AsyncHandle<T>.Completed(entry.asset as T);
        }

        // 未命中 → 入队
        var handle = AsyncHandle<T>.Create();
        scheduler.Enqueue(new LoadRequest {
            key    = key,
            type   = typeof(T),
            handle = handle,
            onDone = (asset) => {
                cache.Put(key, asset, isPreloaded: false);
                refCounter.Retain(key);
                handle.Complete(asset as T);
            },
            onFail = (reason) => {
                var fallback = failure.GetFallback<T>();
                handle.Complete(fallback);
                Debug.LogError($"[Asset] {key} failed: {reason}");
            }
        });
        return handle;
    }

    // 观测路径：查缓存（不触发加载）
    public static bool TryGet<T>(string key, out T asset)
        where T : UnityEngine.Object
    {
        if (cache.TryGet<T>(key, out var entry)) {
            asset = entry.asset as T;
            entry.lastAccessTime = Time.realtimeSinceStartup;
            return true;
        }
        asset = null;
        return false;
    }

    // 显式释放
    public static void Release(string key)
    {
        if (!refCounter.Release(key)) {
            Debug.LogWarning($"[Asset] Release unknown key: {key}");
            // 边界：不抛异常，只记警告
        }
    }

    // 预加载：标记为常驻，不增加调用方引用计数
    public static void Preload(string key)
    {
        // 走 LoadAsync 路径，但完成后不 Retain 调用方引用
        // isPreloaded = true，LifecycleMgr 不淘汰它
        scheduler.Enqueue(new LoadRequest {
            key = key,
            // ...
            onDone = (asset) => cache.Put(key, asset, isPreloaded: true)
        });
    }

    // 每帧推进
    public static void Tick(float dt)
    {
        scheduler.Tick(dt);      // 推进队列，释放信号量
        lifecycle.Tick(dt);      // 冷却期检查 / LRU 淘汰
        failure.Tick(dt);        // 重试计时
    }

    // 切场景
    public static void OnSceneSwitch()
    {
        lifecycle.OnSceneSwitch();
        // 边界：不强制释放 refCount > 0 的资源
        //       保留 isPreloaded=true 的条目
        //       清空所有冷却期，防上一场景的缓存误命中新场景
    }

    public static void Dispose()
    {
        cache.ReleaseAll();
        scheduler.Clear();
    }
}
```

### 2.1 AssetRegistry

```csharp
// 职责：Key → 资源元数据
// 状态：只读映射表，Init 后不变
// 版本差异：
//   Jam 阶段：空壳，Key 直接是路径
//   量产阶段：可切换为 SO 映射表
internal class AssetRegistry
{
    // Jam 阶段：无需映射
    public string ResolvePath(string key)
    {
        // Jam: return key; // key 即路径
        // 量产: return lookupTable[key];
        return key;
    }
}
```

### 2.2 CacheStore + CacheEntry

```csharp
// 职责：持有所有已加载资源的条目
// 状态：Dictionary<string, CacheEntry>
// 访问：主线程独占，无锁
internal class CacheStore
{
    private readonly Dictionary<string, CacheEntry> entries = new();

    public bool TryGet<T>(string key, out CacheEntry entry)
    {
        if (entries.TryGetValue(key, out entry) && entry.asset is T) return true;
        entry = null;
        return false;
    }

    public void Put(string key, UnityEngine.Object asset, bool isPreloaded)
    {
        entries[key] = new CacheEntry {
            asset = asset,
            refCount = 0,                        // Retain 由调用方做
            lastAccessTime = Time.realtimeSinceStartup,
            isPreloaded = isPreloaded,
            cooldownUntil = 0f,
            canEvict = false,
        };
    }

    public void MarkCooldown(string key, float cooldownSeconds)
    {
        // refCount 归零时调用，设置冷却期
    }

    public void Evict(string key)
    {
        // LRU 淘汰时调用
        // 边界：isPreloaded=true 的条目不会被传进来
    }

    public void ReleaseAll()
    {
        // Dispose 时调用
    }

    public IEnumerable<CacheEntry> AllEntries => entries.Values;
}

internal class CacheEntry
{
    public UnityEngine.Object asset;
    public int   refCount;
    public float lastAccessTime;
    public bool  isPreloaded;
    public float cooldownUntil;
    public bool  canEvict;
}
```

### 2.3 RefCounter

```csharp
// 职责：管理每个 CacheEntry 的引用计数
// 状态：委托给 CacheStore
// 边界：refCount 不能为负
internal class RefCounter
{
    private readonly CacheStore cache;
    public RefCounter(CacheStore cache) { this.cache = cache; }

    public void Retain(string key)
    {
        if (cache.TryGetEntry(key, out var e)) e.refCount++;
    }

    public bool Release(string key)
    {
        if (!cache.TryGetEntry(key, out var e)) return false;
        e.refCount = Math.Max(0, e.refCount - 1);
        if (e.refCount == 0) {
            // 触发冷却期，不立即卸载
            e.cooldownUntil = Time.realtimeSinceStartup + COOLDOWN_SECONDS;
            // 边界：如果 isPreloaded=true，不设冷却期
        }
        return true;
    }
}
```

### 2.4 LoadScheduler

```csharp
// 职责：限制并发加载数，排队超额请求
// 状态：信号量 + 队列
// 边界：FIFO；量产可加优先级
internal class LoadScheduler
{
    private readonly SemaphoreSlim semaphore;
    private readonly Queue<LoadRequest> queue = new();

    public LoadScheduler(int maxConcurrent)
    {
        semaphore = new SemaphoreSlim(maxConcurrent);
    }

    public void Enqueue(LoadRequest req)
    {
        queue.Enqueue(req);
    }

    public void Tick(float dt)
    {
        while (queue.Count > 0 && semaphore.CurrentCount > 0) {
            var req = queue.Dequeue();
            semaphore.Wait();
            StartLoad(req);
        }
    }

    private void StartLoad(LoadRequest req)
    {
        // 发起异步加载
        // Resources.LoadAsync / Addressables.LoadAssetAsync
        // 完成后：semaphore.Release(); req.onDone(asset) 或 req.onFail(reason)
    }
}

internal class LoadRequest
{
    public string key;
    public Type type;
    public object handle;                // AsyncHandle<T>
    public Action<UnityEngine.Object> onDone;
    public Action<string> onFail;
}
```

### 2.5 LifecycleMgr

```csharp
// 职责：冷却期 + LRU 淘汰
// 状态：无独立状态，操作 CacheStore
// 边界：isPreloaded=true 的条目永不淘汰
internal class LifecycleMgr
{
    private readonly CacheStore cache;
    private readonly RefCounter refCounter;
    private readonly float cooldownSeconds;
    private readonly int   maxEntries;

    public void Tick(float dt)
    {
        float now = Time.realtimeSinceStartup;
        // 1. 冷却期到期 → 标记可淘汰
        foreach (var e in cache.AllEntries) {
            if (e.refCount == 0 && !e.isPreloaded
                && !e.canEvict && now >= e.cooldownUntil) {
                e.canEvict = true;
            }
        }
        // 2. LRU 淘汰（超过阈值时）
        if (cache.Count > maxEntries) {
            EvictLRU();
        }
    }

    private void EvictLRU()
    {
        // 按 lastAccessTime 排序
        // 淘汰 canEvict=true 且最久未访问的
        // 边界：一次 Tick 最多淘汰 N 个，避免卡帧
    }

    public void OnSceneSwitch()
    {
        foreach (var e in cache.AllEntries) {
            if (e.isPreloaded) continue;
            if (e.refCount > 0) continue;   // 不强制释放
            e.cooldownUntil = 0f;            // 清空冷却期
            e.canEvict = true;               // 允许立即淘汰
        }
    }
}
```

### 2.6 FailureHandler

```csharp
// 职责：重试 + 降级
// 状态：降级资源 + 重试队列
// 边界：不阻塞、不抛异常
internal class FailureHandler
{
    private readonly Dictionary<Type, UnityEngine.Object> fallbacks = new();
    private readonly List<RetryEntry> retryQueue = new();
    private const int   MAX_RETRY = 2;
    private const float RETRY_DELAY = 0.5f;

    public void PreloadFallbacks()
    {
        // 预加载空 Sprite / 静音 AudioClip / 粉色 Material
    }

    public T GetFallback<T>() where T : UnityEngine.Object
    {
        return fallbacks[typeof(T)] as T;
    }

    public void Tick(float dt)
    {
        // 推进重试队列，到期则重试
        // 超过 MAX_RETRY 则走降级
    }
}

internal class RetryEntry
{
    public string key;
    public int    attempt;
    public float  nextTryTime;
}
```

### AsyncHandle

```csharp
// 句柄语义
// 版本差异：必须是 class，不能是 struct（框架约定：跨帧持有）
// 边界：await 必须在主线程
public class AsyncHandle<T> where T : UnityEngine.Object
{
    private TaskCompletionSource<T> tcs = new();
    public bool IsDone { get; private set; }
    public T    Asset  { get; private set; }

    public TaskAwaiter<T> GetAwaiter() => tcs.Task.GetAwaiter();

    public void Complete(T asset)
    {
        Asset = asset;
        IsDone = true;
        tcs.SetResult(asset);
    }

    public static AsyncHandle<T> Create() => new();
    public static AsyncHandle<T> Completed(T asset)
    {
        var h = new AsyncHandle<T>();
        h.Complete(asset);
        return h;
    }
}
```

---

## 四、微块 ③ DataMetrics

### 3.0 DataMetrics（对外入口）

```csharp
// 调用时机：DebugOverlay 每 N 帧拉一次
// 边界：只读；不推送事件；不走 EventBus
public static class DataMetrics
{
    public static DataSnapshot GetSnapshot()
    {
        return new DataSnapshot {
            // AssetModule 状态
            CachedAssetCount = CacheStoreProxy.Count,
            LoadingCount     = LoadSchedulerProxy.LoadingCount,
            QueuedCount      = LoadSchedulerProxy.QueuedCount,
            CacheHitRate     = ComputeHitRate(),
            EvictedCount     = LifecycleMgrProxy.EvictedCount,
            FailedCount      = FailureHandlerProxy.FailedCount,

            // ConfigModule 状态
            TableCount = ConfigModule.IsReady ? ConfigModule.Tables.TableCount : 0,
            ConfigReady = ConfigModule.IsReady,
        };
    }
}
```

### DataSnapshot

```csharp
// 只读快照结构
// 边界：struct，值语义，调用方改不了内部
public struct DataSnapshot
{
    public int   CachedAssetCount;
    public int   LoadingCount;
    public int   QueuedCount;
    public float CacheHitRate;
    public int   EvictedCount;
    public int   FailedCount;
    public int   TableCount;
    public bool  ConfigReady;
}
```

---

## 五、调用方视角的完整流程

```csharp
// 场景：UI 面板显示武器图标
async void ShowWeaponIcon(int weaponId)
{
    // 1. ConfigModule：同步拿数值（含 Key）
    var weapon = ConfigModule.GetWeapon(weaponId);
    string iconKey = weapon.IconKey;

    // 2. AssetModule：异步拿资源
    var handle = AssetModule.LoadAsync<Sprite>(iconKey);
    var sprite = await handle;

    // 3. 使用
    iconImage.sprite = sprite;
}

// 场景：面板关闭
void OnPanelClose(int weaponId)
{
    var weapon = ConfigModule.GetWeapon(weaponId);
    AssetModule.Release(weapon.IconKey);
    // 边界：Release 后资源不立即卸载，进入冷却期
}
```

---

## 六、关键边界汇总

| 边界                       | 说明                                        |
| -------------------------- | ------------------------------------------- |
| **ConfigModule.Init 失败** | 抛异常阻止游戏启动，不允许带病数据          |
| **AssetModule 全部方法**   | 仅主线程调用                                |
| **AsyncHandle**            | 必须是 class，不能是 struct                 |
| **LoadAsync 未命中**       | 入队，不立即返回；由 Tick 推进              |
| **Release 未知 Key**       | 记警告，不抛异常                            |
| **refCount 归零**          | 进入冷却期，不立即卸载                      |
| **isPreloaded=true**       | 永不淘汰                                    |
| **OnSceneSwitch**          | 清冷却期，保留预加载，不强制释放 refCount>0 |
| **FailureHandler 降级**    | 返回占位资源，不阻塞调用方                  |
| **DataMetrics**            | 拉模型，不推送事件                          |

---

## 七、伪代码的演进留口

| 留口                      | Jam 阶段              | 量产阶段                      |
| ------------------------- | --------------------- | ----------------------------- |
| AssetRegistry.ResolvePath | `return key;`         | `return lookupTable[key];`    |
| LoadScheduler.StartLoad   | `Resources.LoadAsync` | `Addressables.LoadAssetAsync` |
| AssetModule 接口          | 已定稿，不变          | 不变                          |
| ConfigModule 接口         | 已定稿，不变          | 不变                          |
| DataMetrics 快照          | 字段可增，不可删      | 增加内存估算字段              |

接口形状从 Jam 到量产**不变**，变化全部在实现内部。
