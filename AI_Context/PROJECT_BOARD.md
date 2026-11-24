# PROJECT BOARD — 荒原复苏指南

> Secretary 独家维护。Attention Queue 只列真正需要 CEO 拍板、手工操作或签收的阻断事项。

最后更新时间：2026-11-24 18:20（Asia/Shanghai）

## Attention Queue

| 事项 | Task ID | 当前状态 | 一句话决策提示 | 更新时间 |
| --- | --- | --- | --- | --- |
| 部署前已有 3 个未提交修改，首个施工任务前需确定保留、提交或隔离方式 | - | WORKTREE_DIRTY | 不得覆盖 `Tables.cs`、`vector2.cs`、`EnvironmentDirector.cs` | 2026-11-24 18:20 |
| 注释门基线为红：4 个既有文件超注释预算，本次未修改这些文件 | - | BASELINE_FAIL | 需 CEO 决定另开清理任务、定点调整预算，或明确门禁豁免条件 | 2026-11-24 18:20 |

## Ticket Index

| Task ID | 级别 | 状态 | 分支 | 当前 PLAN 版本 | 变更模块摘要 | 一句话结论 |
| --- | --- | --- | --- | --- | --- | --- |
| - | - | IDLE | main | - | - | v3 底座已布设；compile-gate 通过，comment-lint 有 4 项既有阻断 |

## 当前基线

- Branch：`main`
- HEAD：`d1ec97d30140aed8c39fd22adc00ccdf4765816e`
- 既有修改：`Assets/Scripts/Generated/Config/Tables.cs`、`Assets/Scripts/Generated/Config/vector2.cs`、`Assets/Scripts/Presentation/Lighting/EnvironmentDirector.cs`
- 状态：`OBSERVED`，读取自 2026-11-24 部署前 `git status`

## 看板规则

1. 每个任务只占 Ticket Index 一行，状态与任务目录最新凭证一致。
2. 同一阻断只保留一条 Attention 记录，解除后立即删除。
3. 普通进度、代码细节和未决策项不得污染 Attention Queue。
4. 看板与文件冲突时，以实际任务凭证和 Git 状态为准，并标 `STALE` 后修正。
