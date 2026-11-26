# BUILD_AND_TEST — 真实验证入口

> 计划第 10 章只能引用本文件登记的方法。实际未运行必须写 `NOT RUN`；环境缺失写 `BLOCKED` 或 `NOT RUN`，不得口头宣称通过。

## 环境

- Unity：`2022.3.62f3c1`
- PowerShell：7+（命令前缀 `pwsh`）
- Python：3.10+（工作流脚本仅依赖标准库）
- URP：`14.0.12`
- 工程根：仓库根目录；不要在被忽略或生成的临时工程内施工。

## V0 — 计划与边界

检查 `PLAN_vNN.md` 的目标、Non-goals、File Scope、批准记录、Base Commit 和回滚是否自洽。范围、接口或验收变化必须升版重审。

## V1 — 静态门禁

```powershell
pwsh Tools/comment-lint.ps1
python -m py_compile scripts\stage_context.py scripts\validate_handover.py
```

- 注释门退出码必须为 0。
- Python 工作流脚本必须能完成语法编译。
- C# 改动若涉及代码风格、空值语义或公共契约，需同时做人工 Diff 审查。

### 2026-11-26 当前基线

`pwsh Tools/comment-lint.ps1` 当前退出码为 1，2 个既有文件超预算：

- `Assets/Scripts/Logic/Services/Timer/TimerWheel.cs`
- `Assets/Scripts/Presentation/UI/Panel/SaveSlot.cs`

这两个文件属于既有基线。`AudioManager.cs` 与 `Fountain.cs` 已在 GD-2026-0003 / GD-2026-0001 的授权改动中回到预算内。新代码任务在 CEO 决定清理或豁免策略前，不得声称注释门通过；默认应另开任务处理，不与功能改动混在一起。

## V2 — 编译与拓扑门

```powershell
pwsh Tools/compile-gate.ps1 -Topology
```

通过判据：

- `0 Error(s)`
- `0 Warning(s)`
- asmdef 目录、反向依赖、重名、镜像覆盖区四类守卫全部通过

这是本工程代码改动的硬门；不得用 Unity Console 单一口头描述替代命令输出。

## V3 — 自动测试

当前登记的自动测试入口：

1. Unity 打开工程，`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`。
2. 记录通过数、失败数、日志或截图。

项目目前没有登记统一的命令行 EditMode 测试脚本，因此 Worker 不得虚构批处理命令。若任务需要无头测试，必须在 PLAN 中专项提出并重新批准。

## 导表镜像专项

涉及 `ConfigWorkspace`、Luban 或生成物时：

```powershell
pwsh Tools/luban-mirror.ps1
```

通过判据：

- 待删除：0
- 待覆盖：0
- 孤儿 `.meta`：0

`ConfigWorkspace/Data/*.xlsx` 只能由 CEO 在 Excel 手工修改；Agent 不写源表，也不得运行覆盖源表的生成脚本。

## V4 — 编辑器与运行时

按任务选择：

- 完整玩法：打开 `Assets/Scenes/Range.unity` 并进入 Play Mode。
- 新游戏入口：`Level_0`。
- 视觉 / 光照：记录场景、观察点、截图或录屏。
- UI：记录打开、关闭、输入与 0 GC 观察点。

证据存入 `AI_Context/EVIDENCE/GD-YYYY-NNNN/`，验收表必须写相对路径。

## V5 — 性能与专项

按风险使用 Unity Profiler、Memory Profiler、GC Alloc、DrawCall 或专项脚本。R2+ 任务默认需要 V5；若降层，必须由 CEO 在批准记录中明确写出理由。

## 上下文与交接

```powershell
python scripts/stage_context.py AI_Context/TASKS/GD-YYYY-NNNN `
  --files Assets/Scripts/Logic/LogicContext.cs

python scripts/validate_handover.py AI_Context/TASKS/GD-YYYY-NNNN --for planner
python scripts/validate_handover.py AI_Context/TASKS/GD-YYYY-NNNN --for worker
python scripts/validate_handover.py AI_Context/TASKS/GD-YYYY-NNNN --for watchdog
python scripts/validate_handover.py AI_Context/TASKS/GD-YYYY-NNNN --for archive
```

## Preflight 必查

1. `git status --short --branch`
2. 当前 HEAD 是否等于批准记录的 Base Commit
3. 既有未提交文件是否会被本任务触碰
4. 拟修改路径是否全部在 File Scope
5. 所需 Unity 版本、Python、PowerShell 与工具是否可用

最后核验日期：2026-11-26｜分支：main@254b9cf｜维护人：CEO
