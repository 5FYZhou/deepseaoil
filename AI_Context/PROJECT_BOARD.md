# PROJECT BOARD — 荒原复苏指南

> Secretary 独家维护。Attention Queue 只列真正需要 CEO 拍板、手工操作或签收的阻断事项。

最后更新时间：2026-11-25 16:36（Asia/Shanghai）

## Attention Queue

| 事项 | Task ID | 当前状态 | 一句话决策提示 | 更新时间 |
| --- | --- | --- | --- | --- |
| 注释门基线为红：4 个既有文件超注释预算，本次未修改这些文件 | - | BASELINE_FAIL | 需 CEO 决定另开清理任务、定点调整预算，或明确门禁豁免条件 | 2026-11-24 18:20 |
| 批 1 合入后的全量导表与 L2 正式运行仍待执行 | GD-2026-0003 | WAITING_CEO_DECISION | 先合入并验证批 1，再从最新主线导表，随后完成 EditMode/Play/V5 | 2026-11-25 15:27 |

## Ticket Index

| Task ID | 级别 | 状态 | 分支 | 当前 PLAN 版本 | 变更模块摘要 | 一句话结论 |
| --- | --- | --- | --- | --- | --- | --- |
| GD-2026-0001 | L2 / R2 | MERGED | `VReason/Update` | v01 | 核心玩法底层修复与配表/手感调优（批 1/3） | 已以 `ACCEPT WITH FOLLOW-UP` 签收并合入；独立 Play/V4 与 V5 保留 |
| GD-2026-0002 | L2 / R2 | ACCEPTED_WITH_FOLLOW_UP | `ai/GD-2026-0002` | v01 | 玩家受击/死亡与波前种子配给（批 2/3） | CEO 已完成手测并签收；独立 Play/V4 与 V5 保留为 follow-up |
| GD-2026-0003 | L2 / R2 | WAITING_CEO_DECISION | `ai/GD-2026-0003` | v01 | 引导、UI、剧情与音频体验（批 3/3） | 第二轮静态门通过；等待批 1 后导表与正式运行验收 |

## 当前基线

- Branch：`VReason/Update`
- HEAD：`c1028c4` (`merge: GD-2026-0001 玩法基座与配表调优`)
- 工作区：仅剩未提交的 `AI_Context/TASKS/GD-2026-0001/NEED_FILES/` 本地补料副本；其余任务文档与 0001 实现已提交
- 原三项修改：`Tables.cs`、`vector2.cs`、`EnvironmentDirector.cs` 已包含在 `df60015`，不再作为未提交改动
- 状态：`VERIFIED`，读取自 2026-11-25 16:32 当前 Git 状态；0001 主分支编译/拓扑门 `0 error / 0 warning`

## Context Drift

| 文件 | STALE 内容 | 当前事实 | 处理 |
| --- | --- | --- | --- |
| `AI_Context/README_AI.md` | 基线仍写 `main` / `d1ec97d`，并称三个文件未提交 | 当前为 `VReason/Update` / `df60015`；三文件已提交，当前仅新增任务文档未提交 | 待 CEO 授权后续上下文回写 |
| `AI_Context/KNOWN_ISSUES.md` | #1 仍登记三个文件为未提交且 `ACTIVE` | 已随 `df60015` 提交 | 待 CEO 授权后续上下文回写 |
| `AI_Context/MODULE_MAP.md` | `EnvironmentDirector` 标注“当前有未提交修改” | 当前工作区无未提交修改 | 待 CEO 授权后续上下文回写 |
| `AI_Context/PROJECT_OVERVIEW.md` | 最后核验分支仍写 `main` | 当前工作分支为 `VReason/Update` | 待 CEO 授权后续上下文回写 |

## 看板规则

1. 每个任务只占 Ticket Index 一行，状态与任务目录最新凭证一致。
2. 同一阻断只保留一条 Attention 记录，解除后立即删除。
3. 普通进度、代码细节和未决策项不得污染 Attention Queue。
4. 看板与文件冲突时，以实际任务凭证和 Git 状态为准，并标 `STALE` 后修正。
