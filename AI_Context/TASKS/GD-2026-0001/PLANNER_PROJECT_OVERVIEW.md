# PLANNER PROJECT OVERVIEW — GD-2026-0001

> Planner 快速入口，生成于 2026-11-24 19:38（Asia/Shanghai）。  
> 本文件只汇总当前事实与源码导航；真实源码、场景、配表与门禁输出仍是最终真源。

## 1. 当前快照

| 项目 | 当前事实 |
| --- | --- |
| 项目 | 荒原复苏指南 / deepseaoil |
| 引擎 | Unity 2022.3.62f3c1，URP 14.0.12，2D Renderer |
| Branch | `VReason/Update` |
| HEAD | `df60015` (`feat: 项目接入AI工作流`) |
| 任务 | `GD-2026-0001`：玩家反馈驱动的 GameJam 大更新 |
| 当前阶段 | 反馈原文已归档，尚未聚类、过滤、定优先级或批准方案 |
| 工作区 | 含本任务新增的 Secretary 文档，未提交 |
| 完整玩法调试场景 | `Assets/Scenes/Range.unity` |
| 新游戏入口 | `Assets/Scenes/Levels/Level_0.unity`，教学关 |

已知基线风险：

- `pwsh Tools/comment-lint.ps1` 当前为红：`TimerWheel.cs`、`AudioManager.cs`、`SaveSlot.cs`、`Fountain.cs` 四个既有文件超注释预算。
- `README_AI.md`、`KNOWN_ISSUES.md`、`MODULE_MAP.md`、`PROJECT_OVERVIEW.md` 仍有旧分支或“未提交修改”描述，状态为 `STALE`；当前 Git 事实以本文件与看板为准。
- 自动测试存在，但任务目前没有统一命令行 EditMode 入口；未运行不得写成通过。

## 2. 项目定位与核心体验

这是一款俯视角 2D 生存追逐战。玩家没有传统直接攻击技能，而是向网格地块投掷水球与土块，利用水、土、火、电、冰、植物等离散地貌反应制造减速、阻挡、伤害与连锁，在怪潮压力下规划路线并求生。

三条设计铁律：

1. `所见即所得`：画面上显示的地貌状态就是规则读取的状态。
2. `离散查表`：核心反应是“原格地貌 + 球种 → 结果地貌、伤害、击退、麻痹、连锁、范围”。
3. `首跳当帧`：带伤害的地貌在落地瞬间结算第一次效果，不等下一次地块 Tick。

项目不追求连续温湿度伪拟真，也不允许把运动学、投掷手感、颜色频率等参数重新塞进 Excel 或代码常量。

## 3. 程序集与运行入口

### 3.1 程序集拓扑

```text
Presentation → Logic → Data → Foundation
                    ↘ cfg → Luban.Runtime
```

实际 `references` 必须逐项显式声明；Unity asmdef references 不传递。

- `DeepseaOil.Foundation`：纯基础件，无程序集引用。
- `DeepseaOil.Data`：配置读取、Spec、SO、AssetModule。
- `DeepseaOil.Logic`：纯业务逻辑与状态机，不碰场景对象。
- `DeepseaOil.Presentation`：MonoBehaviour、场景组合根、输入、UI、表现。
- `cfg`：Luban 生成数据程序集。
- `DeepseaOil.Generated.Input`：Input System 生成程序集。

### 3.2 唯一常驻入口

`Assets/Scripts/Presentation/Root/GameRoot.cs`

- 唯一真单例，`DontDestroyOnLoad`。
- 装配顺序：`ConfigModule` → `AssetModule` → `ConfigModule.BindAssets` → `EffectModule` → UI/Game/Story/Audio → 进程服务。
- `GameRoot.Update` 阶段顺序：场景根装配 → 输入采样 → UI 输入 → 进程服务 → AssetModule → 场景根 RenderTick → EffectModule。
- `GameRoot.FixedUpdate` 只统一驱动已注册场景根的物理段。
- 场景根通过 `ISceneRoot.Order` 排序：入口 `int.MinValue`、玩家 `-100`、世界 `0`；剧情根为世界序后一格。

## 4. 四条主运行链

### 4.1 输入 → 玩家 → 投掷

```text
InputProvider.Sample
  → PlayerController.FixedTick（移动/冲刺快照）
  → PlayerController.RenderTick（瞄准/水球/土球意图）
  → CombatGroup.RequestThrow
  → IThrowSink.RequestThrow
  → BallDirector
  → BallDriver / ProjectileTrajectory
```

关键事实：

- `InputProvider` 是全工程唯一采样点，读取 `Player/Attack`、`AltAttack`、`Plant`、`Discard`、`Aim`、移动与冲刺。
- 瞄准在世界侧由 `CombatGroup.UpdateAim` 转成格子；主攻击为水球，副攻击为土球。
- `CombatGroup.RequestThrow` 先完成空中、冷却、资源、裁决口校验，再由世界侧接受；被拒绝时不扣弹药、不进冷却。
- `ProjectileTrajectory` 用贴地直线作为逻辑真值，抛物线高度只用于视觉；`t >= 1` 即视为落地，不依赖物理碰撞。

入口文件：

- `Assets/Scripts/Presentation/Input/InputProvider.cs`
- `Assets/Scripts/Presentation/Root/PlayerController.cs`
- `Assets/Scripts/Logic/Player/CombatGroup.cs`
- `Assets/Scripts/Logic/Projectile/ProjectileTrajectory.cs`
- `Assets/Scripts/Presentation/Ball/BallDirector.cs`

### 4.2 落地 → 元素反应 → 地貌与伤害

```text
GridLogic.OnBallHit
  → TileChainReactor.TriggerDuo（先判二级）
  → ReactionResolver.Resolve（一级查表/兜底）
  → 强制切换落点地貌
  → ApplyImpact / 范围波及
  → ScheduleTick
  → TileChainReactor.Trigger（按结果地貌连锁）
```

关键事实：

- `ReactionResolver.Resolve` 是纯函数查表；表未命中时，只有空地加水/土才给基础地貌兜底。
- 二级反应必须先于一级反应判断，并使用原格状态；否则一级会先改格，破坏二级配对。
- 落点写入不受覆盖秩限制；间接波及才遵守 `tile_state.priority`。
- 伤害与击退统一由格子层提交给 `IEffectTarget`，击退格数由受击者按自身阻尼反解初速度。
- 连锁方向由结果地貌决定；可燃气貌触发燎原，导电地貌触发导电链。

入口文件：

- `Assets/Scripts/Logic/Grid/GridLogic.cs`
- `Assets/Scripts/Logic/Grid/TileChainReactor.cs`
- `Assets/Scripts/Logic/Element/ReactionResolver.cs`
- `Assets/Scripts/Presentation/Adapters/TilemapAdapter.cs`

### 4.3 敌人 → 寻路 → 接触伤害

```text
CombatRoot.FixedTick
  → 刷新玩家所在格导航流场
  → EnemyActor.FixedTick
  → EnemyLogic.Tick（Brain → MoveGroup → EnemyNavigator）
  → CombatDirector.FixedTick
  → UpdatePlayerContact
```

关键事实：

- `EnemyBrainFactory.Create` 当前对所有敌种都返回 `ChaseBrain`；精英怪 AI 分流尚未实现。
- 敌人决策只产生 `EnemyIntent`，转向、绕障、刹车与分离由 `EnemyNavigator` 落地。
- 物理顺序是敌人先移动、格子再按新位置结算、最后玩家接触检查；调换顺序会读上一帧位置。
- 玩家受击方向与格上伤害共用 `Damage.At` 的方向数学。

入口文件：

- `Assets/Scripts/Presentation/Root/CombatRoot.cs`
- `Assets/Scripts/Presentation/Actor/EnemyActor.cs`
- `Assets/Scripts/Logic/Actor/EnemyLogic.cs`
- `Assets/Scripts/Logic/Actor/EnemyNavigator.cs`
- `Assets/Scripts/Logic/Actor/EnemyBrain.cs`

### 4.4 关卡 → 波次 → 剧情 / UI / 恢复

- `LevelLogic` 管理关卡开始、战斗、胜利/失败与结算状态。
- `WaveLogic` 管理等待、战斗、结算、刷怪与种子授予。
- `StoryFlow` 驱动开场、教学、关卡间剧情和结局，并可临时锁输入。
- `GamePlayPanel` 展示生命、水/土弹药、种子、波次和倒计时。
- `DialoguePanel` 负责剧情与提示框；`HudPanel` 负责受击反馈和运行 HUD。
- 生命泉 `Fountain` 需要玩家在范围内保持静止；`LifeFountainState` 累积静止时间并按间隔恢复。

入口文件：

- `Assets/Scripts/Logic/Level/LevelLogic.cs`
- `Assets/Scripts/Logic/Wave/WaveLogic.cs`
- `Assets/Scripts/Presentation/Story/StoryFlow.cs`
- `Assets/Scripts/Presentation/UI/Panel/GamePlayPanel.cs`
- `Assets/Scripts/Presentation/UI/Panel/DialoguePanel.cs`
- `Assets/Scripts/Presentation/World/Fountain.cs`

## 5. 数据与调参边界

```text
Excel 源表
  → Luban 生成行
  → Data/Config/Spec 包装
  → Logic / Presentation
```

- `ConfigWorkspace/Data/*.xlsx` 只由策划人工维护，Agent 默认只读。
- `ConfigModule` 是 Data 层唯一取值入口；启动时校验核心表、反应、种子、关卡、波次和敌种引用。
- `*Spec` 吸收生成行字段变化，上层不直接绑定 Luban 行。
- `ThrowTuning`、`EnemyTuning`、`PlayerConfig` 承接手感与运动学参数。
- `AssetModule` 按需加载并缓存资源；启动不预载全部 Tile / Sprite / Prefab。

## 6. 当前反馈资料

反馈原文位于 `Docs/玩家反馈/`，目前包括 10 张截图转写的 6 份来源记录，以及 TapTap 长截图中可见的 26 条评价。

- 评论整理方法：`Docs/设计/玩家反馈处理指导思想.md`
- 目录与转写规则：`Docs/玩家反馈/README.md`
- 当前状态：只完成原文归档，没有合并诉求、没有认定优先级、没有批准设计或实现范围。

Planner 应先把反馈当作用户体验证据，再对照源码验证现实约束。玩家提出的具体方案不能直接进入任务书。

## 7. 已知工程约束与风险

1. `GameRoot` 是唯一真单例与唯一常驻组合根；业务组件不得散落 `Update/FixedUpdate`。
2. `Logic` 不继承 `MonoBehaviour`，不碰场景对象，不直接读 `Time.*`。
3. 高频链禁止 LINQ、匿名闭包、频繁容器分配和热路径 `GetComponent`。
4. 生成物目录禁止手写：`Generated/Config/`、`StreamingAssets/Luban/`、`InputSys.cs`、`ConfigWorkspace/output/`。
5. 场景、Prefab、SO、`.meta`、ProjectSettings、Packages 与 xlsx 默认不修改。
6. asmdef references 不传递；跨层使用公开类型时需同时核对基类和接口程序集。
7. 注释门有既有红基线；功能任务不得顺手清理无关注释。
8. 精英怪 AI 当前仍与普通怪共用追逐大脑。
9. 无统一命令行 EditMode 测试入口时，测试必须明确写 `NOT RUN` 或人工记录。

## 8. 推荐阅读顺序

1. `00_CONTEXT_MANIFEST.md`：确认 Base Commit、文件清单、Token 和警告。
2. 本文件：建立项目定位、运行链和风险边界。
3. `AI_Context/TASKS/GD-2026-0001/TASK_BRIEF.md`：确认本任务目标与边界。
4. `Docs/设计/策划案_荒原复苏指南_v4.0.md`：确认设计意图和配置口径。
5. `Docs/玩家反馈/README.md` 与全部反馈文件：读取原始体验证据。
6. `GameRoot` → `PlayerController` / `CombatGroup` → `GridLogic` / `ReactionResolver` → `EnemyLogic` / `WaveLogic`：按实际运行链核对。
7. 需要扩证时再通过 `NEED_FILES` 请求光照、特效、完整 UI、测试实现或其他模块。

## 9. Planner 输出要求

- 任何任务书都必须写明真实入口、下游影响、文件白名单、Non-goals、风险等级和可判定验收。
- 反馈之间的冲突、人数声音大小与玩家具体方案不能替代设计判断。
- 触及公共接口、asmdef、数据表、场景、Prefab、SO、输入动作或验收标准时，必须停下并提交 CEO 重新批准范围。
- 未阅读对应源码、未运行门禁的内容只能标为 `INFERRED` / `UNKNOWN`，不得写成 `VERIFIED`。
