# MODULE_MAP — 程序集、目录与职责索引

> 本表是任务装载索引，不是源码镜像。每个任务验收后核对漂移；漂移项标 `STALE` 并回写修正。

| 路径 | 程序集 | 职责 | 关键入口 |
| --- | --- | --- | --- |
| `Assets/Scripts/Foundation/` | `DeepseaOil.Foundation` | 与游戏无关的地基：状态机、对象池、运动参数、随机数、Singleton、YSort | `Singleton<T>`、`StateMachine`、`Pool`、`Rng` |
| `Assets/Scripts/Data/` | `DeepseaOil.Data` | 配置查询、Spec 防腐、资产加载、调参 SO、数据观测 | `ConfigModule`、`AssetModule`、`*Spec` |
| `Assets/Scripts/Logic/` | `DeepseaOil.Logic` | 纯业务逻辑：Actor、Combat、Grid、Element、Movement、Wave、Services | `LogicContext`、`Tickable` |
| `Assets/Scripts/Logic/Grid/` | `DeepseaOil.Logic` | 网格状态、流场导航、连锁反应、地块队列 | `GridLogic`、`TileChainReactor`、`NavField` |
| `Assets/Scripts/Logic/Element/` | `DeepseaOil.Logic` | 离散元素反应查表与结果结构 | `ReactionResolver`、`ReactionOutcome` |
| `Assets/Scripts/Logic/Movement/` | `DeepseaOil.Logic` | 运动账本、状态与端口 | `ActorLedger`、`IActorMotor`、`IMovementMotor` |
| `Assets/Scripts/Logic/World/` | `DeepseaOil.Logic` | 生命泉蓄力/衰减、九宫范围与种子播种合法性 | `LifeFountainState`、`NineGrid`、`SeedPlanter` |
| `Assets/Scripts/Presentation/` | `DeepseaOil.Presentation` | 组合根、Adapter、表现、UI、特效、输入、表现层诊断 | `GameRoot`、`ISceneRoot` |
| `Assets/Scripts/Presentation/Root/` | `DeepseaOil.Presentation` | 唯一装配与调度入口 | `GameRoot`、`CombatRoot`、`GameManager` |
| `Assets/Scripts/Presentation/Adapters/` | `DeepseaOil.Presentation` | 将 Logic 端口接到 Unity 运行时 | `ActorMotor`、`TilemapAdapter`、`GameTime` |
| `Assets/Scripts/Presentation/Effects/` | `DeepseaOil.Presentation` | 特效目录、对象池、驱动与调参 | `EffectModule`、`EffectCatalog`、`IEffectDriver` |
| `Assets/Scripts/Presentation/Lighting/` | `DeepseaOil.Presentation` | 环境、波次时段与全局光照表现控制 | `EnvironmentDirector`、`TileGridLightmap` |
| `Assets/Scripts/Presentation/Input/` | `DeepseaOil.Presentation` | 输入采样与动作表接线 | `InputProvider`、`InputSys.inputactions` |
| `Assets/Scripts/Presentation/UI/` | `DeepseaOil.Presentation` | 战斗 HUD、键位标、支援/残血浮窗、逐字对话框与弹窗 | `GamePlayPanel`、`DialoguePanel`、`HudHintBanner` |
| `Assets/Scripts/Presentation/World/` | `DeepseaOil.Presentation` | 泉眼交互、生命泉蓄力表现与进圈发光 | `Fountain` |
| `Assets/Scripts/Generated/Config/` | `cfg` | Luban 生成的表与行类型 | `Tables`、`Tb*`、`vector*` |
| `Assets/Scripts/Generated/Input/` | `DeepseaOil.Generated.Input` | Input System 生成代码 | `InputSys.cs` |
| `Assets/Editor/` | `DeepseaOil.EditorTools` | Luban 导入、菜单、校验与编辑器工具 | `LubanProject`、`LubanImport` |
| `Assets/Tests/Runtime/Editor/` | `DeepseaOil.Tests.EditMode` | 运行时逻辑的 EditMode 回归测试 | Test Runner |
| `Assets/Tests/Tools/` | `DeepseaOil.EditorTools.Tests` | 配表工具链测试 | `LubanWorkflowTests` |
| `ConfigWorkspace/Data/` | — | 人工维护的 Excel 真源 | `.xlsx` |
| `Docs/工程/架构约束.md` | — | asmdef 拓扑与实测边界的权威文档 | 人工阅读 |
| `Tools/compile-gate.ps1` | — | 逐 asmdef 编译与拓扑守卫 | CLI |
| `Tools/comment-lint.ps1` | — | 注释预算与 XML 标签门禁 | CLI |
| `Tools/luban-mirror.ps1` | — | 导表结果镜像与孤儿 `.meta` 检查 | CLI |
| `scripts/stage_context.py` | — | 生成 `FEED_DROP/` 与上下文 Manifest | CLI |
| `scripts/validate_handover.py` | — | planner / worker / watchdog / archive 结构门禁 | CLI |

## 禁止直接依赖的实现细节

- `Logic` / `Presentation` 不应绕过 Spec 直接绑定生成表行字段。
- `Presentation` 之外的层不得依赖表现组件、UI、Cinemachine 或场景对象。
- 手写代码不得放入 `Assets/Scripts/Generated/Config/`、`Assets/StreamingAssets/Luban/` 或覆盖 `InputSys.cs`。
- `.prefab / .unity / .asset / .meta / .xlsx` 默认不修改；需要时由 CEO 手工执行或在批准记录中专项授权。

## 漂移记录

| 日期 | 漂移内容 | 处理 |
| --- | --- | --- |
| 2026-11-24 | 初次布设，基于根 `AGENTS.md`、`Docs/工程/架构约束.md` 和 `Docs/工程/目录说明.md` | VERIFIED，待任务验收后漂移回写 |
| 2026-11-26 | 三批大更新合入后，移除“EnvironmentDirector 未提交”，补生命泉、输入与引导 UI 入口 | VERIFIED |

最后核验日期：2026-11-26｜分支：main@254b9cf｜维护人：CEO
