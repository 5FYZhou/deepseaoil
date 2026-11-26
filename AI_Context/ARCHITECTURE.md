# ARCHITECTURE — asmdef 分层与运行期边界

> 权威来源：根 `AGENTS.md`、`Docs/工程/架构约束.md`、实际 `.asmdef` 与 `Tools/compile-gate.ps1`。本文件用于任务规划时快速装载，不替代源文件。

## 程序集拓扑

```text
DeepseaOil.Presentation
        │
        ▼
DeepseaOil.Logic ───────────────▶ DeepseaOil.Foundation
        │
        ▼
DeepseaOil.Data ───────▶ cfg ───▶ Luban.Runtime
        │                  │
        └──────────────────┴──▶ DeepseaOil.Foundation
```

| 程序集 | 位置 | 允许的主要引用 |
| --- | --- | --- |
| `DeepseaOil.Foundation` | `Assets/Scripts/Foundation/` | 空 references；只认 UnityEngine 与自身 |
| `cfg` | `Assets/Scripts/Generated/cfg.asmdef` | `Luban.Runtime` |
| `DeepseaOil.Data` | `Assets/Scripts/Data/` | `cfg`、`Luban.Runtime` |
| `DeepseaOil.Logic` | `Assets/Scripts/Logic/` | Foundation、Data、cfg、Luban.Runtime |
| `DeepseaOil.Presentation` | `Assets/Scripts/Presentation/` | Foundation、Data、Logic、Generated.Input、cfg、Luban.Runtime、Input System、TMP、UGUI、Cinemachine |
| `DeepseaOil.Generated.Input` | `Assets/Scripts/Generated/Input/` | `Unity.InputSystem` |
| `DeepseaOil.EditorTools` | `Assets/Editor/` | 空 references，仅 Editor |
| `DeepseaOil.Tests.EditMode` | `Assets/Tests/Runtime/Editor/` | 四层、cfg、Luban.Runtime、Unity Test Runner |
| `DeepseaOil.EditorTools.Tests` | `Assets/Tests/Tools/` | `DeepseaOil.EditorTools` |
| `Luban.Runtime` | `Assets/Luban.Runtime/` | 本地运行库，空 references |

## 不可破坏的拓扑规则

1. 依赖只能朝 Foundation 方向回流；Foundation 绝不引用 Data / Logic / Presentation。
2. `cfg.asmdef` 必须放在 `Assets/Scripts/Generated/`，不能放进被 Luban 镜像覆盖的 `Generated/Config/`。
3. Unity asmdef 的 references 不传递。代码使用到的基类、接口和公开签名类型所在程序集必须逐条显式引用。
4. `Logic` 不得继承 `MonoBehaviour`，不得触碰 GameObject / Transform / Component / Tilemap，不得直接读 `Time.*`。
5. Editor 代码只在 Editor 程序集；不得混入运行时构建。
6. 不安装 Luban UPM 包，避免与本地 `Luban.Runtime` asmdef 撞名。

## 运行期模型

- `GameRoot` 是唯一真单例和唯一常驻组合根。
- 业务组件不得散落 `Update()` / `FixedUpdate()`；驱动由 `GameRoot` 统一调度。
- 场景系统实现 `ISceneRoot` 并声明推进顺序。
- UI、音频、Data 层与游戏状态由 `GameRoot` 装配并持有。
- 热路径禁止 LINQ、匿名 lambda 闭包、频繁容器分配和热路径 `GetComponent`。

## 数据与表现边界

- `ConfigWorkspace/Data/*.xlsx` 是策划数值的唯一人工真源，只由 Excel 修改。
- Luban 先写 `ConfigWorkspace/output/`，成功后再镜像发布到 `Assets/Scripts/Generated/Config/` 与 `Assets/StreamingAssets/Luban/`。
- `Data/Config/Spec/` 负责吸收表结构变化，`Logic` 与 `Presentation` 通过 Spec 读取，避免直接依赖生成的表行细节。
- 运动学、投掷、视觉频率等手感参数放在调参 SO；不得为单次需求硬编码成代码常量。
- 资产通过 `AssetModule` 按需懒加载，禁止启动时全量预加载。

## 编译期与人工门禁

- 机器门：`pwsh Tools/compile-gate.ps1 -Topology`。
- 注释门：`pwsh Tools/comment-lint.ps1`。
- 导表镜像：`pwsh Tools/luban-mirror.ps1`。
- 运行纪律（单例、唯一驱动入口、热路径 0 GC）没有完整静态门禁，必须在计划和 Diff 审查中显式核对。

最后核验日期：2026-11-24｜分支：main｜维护人：CEO
