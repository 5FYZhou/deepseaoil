# AI ENGINEERING TASK SPECIFICATION — 荒原复苏指南

## 0. Metadata

Task ID: GD-2026-0005
Plan Version: v03
Status: APPROVED
Task Level: L1
Risk Level: R1
Project: 荒原复苏指南 / Unity 2022.3.62f3c1
Platform: Windows PC
Branch / Base Commit: `ai/GD-2026-0005` / `03d8913e6ef755ca675bea5bd71b0526097d4faf`
Planner: CEO 确认的 v03 行为修正
Worker: 独立 Worker 会话，加载 `$worker-engine`
Watchdog: CEO 验收；如要求独立复核另行启动
Context Manifest: `00_CONTEXT_MANIFEST.md`
Execution Attempt Cap: A01-A03
Created At: 2026-11-26 10:28 Asia/Shanghai
Approved By: CEO
Approved At: 2026-11-26 10:28 Asia/Shanghai

## 1. Objective

恢复规则 17/18 的半径 1，并修正同元素命中因 `NextTile == current` 被短路的问题，使水球砸基础水、土块砸基础土时，中心与十字四邻各受 1 点伤害并生成对应水/土元素格。已验收的剧情配色、动态种子配色和种子次数布局保持不变。

## 2. Non-goals

- 不修改反应算法、其他元素规则或二级反应。
- 不修改已验收的剧情表、剧情 JSON、VisualPalette、HudHintBanner 或 GamePlayPanel。
- 不新增 schema、依赖、公共接口或资源格式。
- 不提交、不合并、不推送。

## 3. Current State

- VERIFIED：`element_rule.xlsx!Sheet1.I21:I22` 当前被误改为 0；伤害仍为 1。
- VERIFIED：Luban JSON 规则 17/18 当前为 `impact_damage=1`、`impact_radius=0`。
- VERIFIED：`GridLogic.OnBallHit` 仅在 `outcome.NextTile != current` 时调用 `ApplyTileChange`。
- VERIFIED：规则 17/18 的 source/result 地貌相同，因此当前即使恢复半径 1，仍不会进入 `SpreadTileInRadius`。
- VERIFIED：剧情配色、动态种子色与 `TxtSeed` 布局已由 CEO 手工验收。

## 4. Constraints

- 保持四层依赖与热路径 0 GC。
- `element_rule.xlsx` 只改 `Sheet1!I21:I22`，保留工作表结构、验证、样式与非目标单元格。
- Luban JSON 只能由正常导表生成，禁止手改。
- 仅对同元素规则修复“同值短路”；不同元素、半径 0 和二级反应优先行为保持不变。
- 工作区中的 Secretary 文档、已验收 UI/配色修改和其他任务内容必须原样保留。

## 5. Proposed Design

- 配表恢复：规则 17/18 的 `impact_radius` 从 0 恢复为 1，`impact_damage` 保持 1。
- 逻辑修复：`OnBallHit` 在二级反应未触发时，只要 `NextTile != current || ImpactRadius > 0` 就调用 `ApplyTileChange`。
- 同元素时中心强制切换为同一状态是 no-op；`ApplyTileChange` 随后按既有半径调用 `SpreadTileInRadius`，将十字四邻空地铺为结果地貌。
- 冲击仍走既有 `ApplyImpact`：半径 1 使中心与十字四邻各结算一次伤害，对角格不纳入。
- 不拆分“伤害半径”和“地貌半径”，继续遵守现有 radius=1 的同构契约。

## 6. File Scope

- READ: `AGENTS.md`、`AI_Context/TASKS/GD-2026-0005/**`、`AI_Context/BUILD_AND_TEST.md`
- MODIFY: `AI_Context/TASKS/GD-2026-0005/TASK_BRIEF.md`
- MODIFY: `AI_Context/TASKS/GD-2026-0005/APPROVAL_RECORD.md`
- CREATE: `AI_Context/TASKS/GD-2026-0005/PLAN_v03.md`
- CREATE: `AI_Context/TASKS/GD-2026-0005/EXECUTION_REPORT_A02.md`
- MODIFY: `ConfigWorkspace/Data/element_rule.xlsx`
- MODIFY: `Assets/StreamingAssets/Luban/dso_tbelementrule.json`（仅 Luban 生成）
- MODIFY: `Assets/Scripts/Logic/Grid/GridLogic.cs`
- MODIFY: `Assets/Tests/Runtime/Editor/地块范围Tests.cs`
- MODIFY: `Assets/Tests/Runtime/Editor/地块效果Tests.cs`
- FORBIDDEN: 所有未列出的 `Assets/**`、其他 `ConfigWorkspace/Data/*.xlsx`、`ProjectSettings/**`、`Packages/**`、`Tools/**`、`scripts/**`、`README.md`、`AI_Context/PROJECT_BOARD.md`、`AI_Context/TASKS/GD-2026-0004/**`

## 7. Execution Steps

| # | 前置条件 | 路径 / 位置 | 操作 | 预期结果 | 检查方式 |
| --- | --- | --- | --- | --- | --- |
| 1 | v03 批准 | 本目录 | 写入 v03、批准记录与修正后的 Task Brief | 凭证一致 | worker validator |
| 2 | 当前错误行为已复现 | `地块范围Tests.cs` | 先把同元素用例改为半径 1、十字铺五格、五格伤害 | 测试表达目标行为 | Diff 审查 |
| 3 | 测试先红 | Unity EditMode | 当前逻辑下运行目标用例 | 同元素四邻仍为空地，用例失败 | TestRunner |
| 4 | 数据恢复 | `element_rule.xlsx` | `I21/I22` 从 0 改回 1 | 真表半径 1、伤害 1 | XLSX 回读 |
| 5 | 正常导表 | `ConfigWorkspace` | 运行 strict Luban 导出并镜像 | JSON 半径 1 | Luban 日志 + mirror |
| 6 | 最小逻辑修复 | `GridLogic.cs` | 允许同值但半径大于 0 的规则进入 `ApplyTileChange` | 四邻铺水/土格 | Diff + TestRunner |
| 7 | 回归 | 两份地块测试 | 更新规则 17/18 真值与同元素行为断言 | 其他规则断言不变 | Unity TestRunner |
| 8 | 收口 | 本目录 | 写 A02 报告并复跑全部门禁 | 11 节报告完整 | validator + commands |

## 8. Engineering Rules

- 修复点只放在 `OnBallHit` 的同值短路条件，不重构 `SpreadTileInRadius` 或 `ReactionResolver`。
- 半径 1 的邻格伤害沿用现有引擎契约：中心与十字四邻各 1 点。
- 非空邻格覆盖继续由 `SwitchTileState` 的既有优先级裁决。
- 两份 XLSX 修改必须保留 ZIP 部件、工作表结构、子表头和全部非目标单元格。
- 测试不得删除既有断言；规则 17/18 的半径期望恢复为 1。
- `GridLogic.cs` 注释预算只降不升；本次不改注释行。

## 9. Acceptance Criteria

- AC-01：规则 17/18 真表与 JSON 均为 `impact_damage=1`、`impact_radius=1`。
- AC-02：水球砸基础水、土块砸基础土后，中心与十字四邻均为对应水/土格，各命中 1 次。
- AC-03：同两例的对角四格保持 Normal 且命中 0 次。
- AC-04：不同元素规则、半径 0 行为和二级反应优先级不变。
- AC-05：已验收的剧情配色、动态种子配色与 `TxtSeed` 布局不被修改。
- AC-06：worker validator、compile gate、Luban mirror、`git diff --check` 有真实证据；Unity EditMode 若未执行必须写 NOT RUN。
- AC-07：实际修改 100% 位于 v03 File Scope，越权为 0。

## 10. Test Plan

- V0：`python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0005 --for worker`
- V1：`pwsh Tools/comment-lint.ps1`；记录两项既有基线红灯。
- V2：`pwsh Tools/compile-gate.ps1 -Topology`，要求 0 error / 0 warning。
- V3：Unity Test Runner → EditMode → Run All；重点运行 `地块范围Tests` 与 `地块效果Tests`。
- 配表专项：Luban strict 导出后运行 `pwsh Tools/luban-mirror.ps1`，要求 0/0/0。
- Diff：`git diff --check` 与白名单逐路径核对。
- V4：`Level_0` 人工确认水+水、土+土中心及四邻生成对应元素格，同时复核已验收 UI。
- V5：NOT APPLICABLE；逻辑为常数条件修正，不新增持续热路径扫描。

## 11. Risks

| 风险 | 影响 | 概率 | 缓解 |
| --- | --- | --- | --- |
| 同值短路未彻底修复 | 半径恢复但依旧不铺格 | 中 | 断言十字四邻地貌 |
| 同元素扩散误伤对角格 | 范围超出四邻 | 低 | 对角格状态与命中双断言 |
| 误改其他半径规则 | 战斗范围回归 | 中 | 仅改 `OnBallHit` 条件，保留既有测试 |
| XLSX 二次保存损坏结构 | 策划表不可维护 | 低 | OOXML 定向改单元格并回读全部结构 |

## 12. Rollback

- 基线：`main@03d8913e6ef755ca675bea5bd71b0526097d4faf`。
- 恢复 `element_rule.xlsx` 与对应 JSON、`GridLogic.cs`、两份地块测试到 v03 施工前状态。
- 不恢复已验收的剧情、Hud、Palette、Prefab 文件。
- 使用 `git restore` 仅恢复 v03 路径；不得重置整仓或覆盖 Secretary 修改。

## 13. Open Questions

- 阻塞：none。
- CEO 已锁定：十字四邻保留 1 点范围伤害。
- 非阻塞假设：非空邻格按既有优先级覆盖。

## 14. Approval

- Decision: APPROVED
- Plan Version: v03
- Approved Scope: 见第 6 节。
- Conditions: 只修复同元素地貌扩散并恢复半径 1；已验收 UI/配色保持不变。
- Explicitly Forbidden: 手改 JSON、改其他规则、改无关文件、提交/推送。
- Base Commit: `03d8913e6ef755ca675bea5bd71b0526097d4faf`
- Approver: CEO
- Approved At: 2026-11-26 10:28 Asia/Shanghai
