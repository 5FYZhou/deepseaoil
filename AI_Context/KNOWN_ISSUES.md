# KNOWN_ISSUES — 已知问题、风险与人工边界

> 发现即登记。与代码冲突时以实际工程为准，并把相关文档标 `STALE`。

| # | 日期 | 问题 / 风险 | 影响 | 状态 | 关联任务 |
| --- | --- | --- | --- | --- | --- |
| 1 | 2026-11-24 | 部署前已有 3 个未提交修改：`Tables.cs`、`vector2.cs`、`EnvironmentDirector.cs` | 新任务可能覆盖用户工作或误把既有改动算作本任务 Diff | ACTIVE | - |
| 2 | 2026-11-24 | EditMode 测试目前没有统一命令行入口 | 无法把测试结果接入单一自动门禁；必须人工运行或专项补脚本 | OPEN | - |
| 3 | 2026-11-24 | `ConfigWorkspace/Data/*.xlsx` 只能人工在 Excel 维护 | Agent 写入会静默破坏别名、下拉框和人工数据 | GUARDED | - |
| 4 | 2026-11-24 | openpyxl 重建工作表会破坏嵌套子表头 | 可能产生“缺失列 alias”或源表结构损坏 | GUARDED | - |
| 5 | 2026-11-24 | Tile Palette 拖入多 Sprite 图集可能触发 Unity 原生对话框并崩溃编辑器 | 可留下残缺 Tile 资产与丢失引用 | GUARDED | - |
| 6 | 2026-11-24 | 安装 Luban UPM 包会与本地 `Luban.Runtime` asmdef 撞名 | 整个 Unity 工程程序集编译失败 | GUARDED | - |
| 7 | 2026-11-24 | `Generated/Config/`、`StreamingAssets/Luban/` 与 `InputSys.cs` 会被生成流程覆盖 | 手写文件可能被静默删除 | GUARDED | - |
| 8 | 2026-11-24 | `comment-lint.ps1` 基线退出码 1：`TimerWheel.cs`、`AudioManager.cs`、`SaveSlot.cs`、`Fountain.cs` 超注释预算 | V1 注释门当前不能全绿；功能任务不得混入无关清理 | OPEN | - |

## 登记规则

1. Worker 发现既有缺陷时只报告，不在批准范围外顺手修复。
2. `CLOSED` 必须附证据位置，例如 `AI_Context/EVIDENCE/GD-YYYY-NNNN/`。
3. 资产、GUID、配表、公共接口、生命周期或渲染管线风险应按 R2/R3 升级。
4. 当前脏工作区在首个施工任务前必须由 CEO 决定保留、提交或隔离；任何任务不得覆盖这些文件。

最后核验日期：2026-11-24｜分支：main｜维护人：CEO
