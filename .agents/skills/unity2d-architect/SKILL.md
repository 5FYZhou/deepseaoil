---
name: unity2d-architect
description: Use when planning Unity 2D tasks in the deepseaoil repository, especially asmdef topology, lifecycle, 0 GC, input, rendering, asset loading, or verification design. Read-only and planning-only.
---

# Unity 2D 架构顾问（deepseaoil）

## 定位

仅在 Planner 规划期叠加。读取真实 `.asmdef`、源码和 `AI_Context/`，给出设计、风险和验证建议；不写 C#、不碰 Unity 资产、不执行修复。Worker 会话不得挂载本技能。

## 本工程真实拓扑

```text
DeepseaOil.Presentation
        ↓
DeepseaOil.Logic
        ↓
DeepseaOil.Data
        ↓
DeepseaOil.Foundation
```

- `DeepseaOil.Foundation` references 必须为空。
- `cfg` 只引 `Luban.Runtime`；`Data`、`Logic`、`Presentation` 按真实 asmdef 逐条引用。
- `Presentation` 才能依赖 UI、Input、TMP、Cinemachine 和场景组件。
- Editor 工具位于 `Assets/Editor/`（`DeepseaOil.EditorTools`），测试位于 `Assets/Tests/`。
- Unity asmdef references 不传递；基类、接口和公开签名类型所在程序集必须显式引用。

权威来源：根 `AGENTS.md`、`Docs/工程/架构约束.md`、实际 `.asmdef`、`Tools/compile-gate.ps1`。

## 规划时必须覆盖

1. **运行期所有权**：`GameRoot` 是唯一真单例与组合根；业务组件不新增散落 `Update()` / `FixedUpdate()`。
2. **Logic 纯净度**：不继承 `MonoBehaviour`，不碰场景对象，不直接读 `Time.*`；时间和 Δt 由端口注入。
3. **生命周期与资源**：Awake / Tick / Disable / Destroy、事件订阅、对象池、AssetModule 加载与释放必须成对。
4. **Hot Path 0 GC**：禁 LINQ、匿名 lambda 闭包、频繁集合分配和热路径 `GetComponent`。
5. **2D 与渲染**：Sorting Layer + `RenderOrder`，物理检测优先 NonAlloc，刚体配置与相机方案必须可验证。
6. **数据边界**：Excel 只由 CEO 手工维护；Spec 吸收生成表结构变化；手感调参放 SO。
7. **生成物禁令**：`Generated/Config/`、`StreamingAssets/Luban/`、`InputSys.cs` 不手写。
8. **资产边界**：`.prefab / .unity / .asset / .meta / .xlsx` 不由 Agent 修改。

## 验证矩阵

只引用 `AI_Context/BUILD_AND_TEST.md` 已登记入口：

| 层 | 计划要求 |
| --- | --- |
| V1 | `pwsh Tools/comment-lint.ps1`；涉及导表时 `pwsh Tools/luban-mirror.ps1` |
| V2 | `pwsh Tools/compile-gate.ps1 -Topology`，0 error / 0 warning |
| V3 | Unity Test Runner EditMode Run All；未运行写 NOT RUN |
| V4 | 真实场景与观察点，例如 `Assets/Scenes/Range.unity` 或 `Level_0` |
| V5 | R2+ 任务按风险给 Profiler、GC、DrawCall 或内存预算 |

不得要求不存在的 `ProjectAuditWindow`、`RunBatchAudit` 或批处理测试命令。

## 输出

向 Planner 返回：

1. VERIFIED / OBSERVED / INFERRED / UNKNOWN 事实与来源。
2. 最小设计方案和替代方案取舍。
3. 精确 File Scope 建议与资产手工清单。
4. 生命周期、GC、序列化、资源和依赖风险。
5. 由真实门禁组成的 AC 与 Test Plan。
6. 仍需补充的 `NEED_FILES`。

## 停止条件

遇到新增程序集、公共接口、序列化字段、依赖、ProjectSettings、渲染管线、Excel 资产或跨层回归风险时，标记重审，不在规划中自行批准。
