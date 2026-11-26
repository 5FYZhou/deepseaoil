# PROJECT BOARD — 荒原复苏指南

> Secretary 独家维护。Attention Queue 只列真正需要 CEO 拍板、手工操作或签收的阻断事项。

最后更新时间：2026-11-26 10:37（Asia/Shanghai）

## Attention Queue

| 事项 | Task ID | 当前状态 | 一句话决策提示 | 更新时间 |
| --- | --- | --- | --- | --- |
| 大更新安装包待完成一轮完整通关 | GD-2026-0001~0003 | PACKAGED / FULL_RUN_PENDING | 请完成安装包完整通关；在此之前仅按“内部预览包”处理 | 2026-11-25 19:05 |
| 注释门基线仍红：`TimerWheel.cs`、`SaveSlot.cs` 两个既有文件超预算 | - | BASELINE_FAIL | 需 CEO 决定另开清理任务、定点调整预算，或明确门禁豁免条件 | 2026-11-25 16:52 |
| README v02 成品待视觉验收 | GD-2026-0004 | AWAITING_CEO_REVIEW | 检查首屏新文案、媒体排版、Mermaid 与 details 折叠效果，然后决定签收或返工 | 2026-11-26 01:15 |

## Ticket Index

| Task ID | 级别 | 状态 | 分支 | 当前 PLAN 版本 | 变更模块摘要 | 一句话结论 |
| --- | --- | --- | --- | --- | --- | --- |
| GD-2026-0001 | L2 / R2 | MERGED | `VReason/Update` | v01 | 核心玩法底层修复与配表/手感调优（批 1/3） | 已签收合入；进入安装包完整通关阶段，独立 Play/V4 与 V5 保留 |
| GD-2026-0002 | L2 / R2 | MERGED | `VReason/Update` | v01 | 玩家生存、死亡反馈与波前种子配给（批 2/3） | 已签收合入；进入安装包完整通关阶段，独立 Play/V4 与 V5 保留 |
| GD-2026-0003 | L2 / R2 | MERGED | `VReason/Update` | v01 | 引导、UI、剧情与音频体验（批 3/3） | 已签收合入；进入安装包完整通关阶段，独立 Play/V4 与 V5 保留 |
| GD-2026-0004 | L1 / R1 | AWAITING_CEO_REVIEW | - | v02 | 根 README 对外主页、实机媒体与开发入口重构 | 已直接执行 FINAL v02 与指定文案，自动验证通过，等待 CEO 验收 |
| GD-2026-0005 | L1 / R1 | MERGED | `ai/GD-2026-0005` | v03 | 同元素反应、种子计数遮挡与剧情/弹窗语义配色 | 已按 CEO 指令提交并合入 `main`；业务提交 `5031dba`，编译/导表/Diff 门通过 |

## 当前基线

- Branch：`main`
- HEAD：`78a5583` (`merge: GD-2026-0005 同元素扩散与提示体验修复`)
- 工作区：任务登记前干净；本地 `main` 领先 `origin/main` 1 个提交
- 原三项修改：`Tables.cs`、`vector2.cs`、`EnvironmentDirector.cs` 已包含在 `df60015`，不再作为未提交改动
- 状态：`PACKAGED / FULL_RUN_PENDING`。三批合入后编译/拓扑、Luban 与 Diff 门通过；CEO 已完成一轮 Test Runner 和简短 Play 冒烟，安装包完整通关尚未完成
- 团队说明：`Docs/更新说明/v2.0更新说明.docx`

## Context Drift

| 文件 | STALE 内容 | 当前事实 | 处理 |
| --- | --- | --- | --- |
| `AI_Context/README_AI.md` | 基线仍写 `main` / `d1ec97d`，并称三个文件未提交 | 当前为 `main` / `254b9cf` | 待 CEO 授权后续上下文回写 |
| `AI_Context/KNOWN_ISSUES.md` | #1 仍登记三个文件为未提交且 `ACTIVE` | 已随 `df60015` 提交 | 待 CEO 授权后续上下文回写 |
| `AI_Context/MODULE_MAP.md` | `EnvironmentDirector` 标注“当前有未提交修改” | 当前工作区无未提交修改 | 待 CEO 授权后续上下文回写 |
| `AI_Context/PROJECT_OVERVIEW.md` | 最后核验分支仍写 `main` | 当前工作分支为 `VReason/Update@3c7b76e` | 待 CEO 授权后续上下文回写 |

## 看板规则

1. 每个任务只占 Ticket Index 一行，状态与任务目录最新凭证一致。
2. 同一阻断只保留一条 Attention 记录，解除后立即删除。
3. 普通进度、代码细节和未决策项不得污染 Attention Queue。
4. 看板与文件冲突时，以实际任务凭证和 Git 状态为准，并标 `STALE` 后修正。
