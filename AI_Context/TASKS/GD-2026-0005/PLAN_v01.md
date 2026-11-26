# AI ENGINEERING TASK SPECIFICATION — 荒原复苏指南

## 0. Metadata

Task ID: GD-2026-0005
Plan Version: v01
Status: APPROVED
Task Level: L1
Risk Level: R1
Project: 荒原复苏指南 / Unity 2022.3.62f3c1
Platform: Windows PC
Branch / Base Commit: `ai/GD-2026-0005` / `03d8913e6ef755ca675bea5bd71b0526097d4faf`
Planner: CEO 直接批准的 Secretary 快速响应方案
Worker: 独立 Worker 会话，加载 `$worker-engine`
Watchdog: CEO 验收；如要求独立复核另行启动
Context Manifest: `00_CONTEXT_MANIFEST.md`
Execution Attempt Cap: A01-A03
Created At: 2026-11-26 09:52 Asia/Shanghai
Approved By: CEO
Approved At: 2026-11-26 09:52 Asia/Shanghai

## 1. Objective

修复三项已观察问题：水球砸基础水、土块砸基础土仍向外铺格；种子次数被 `Q 丢弃` 遮挡；剧情/弹窗强调色全部为红。修复后保留同元素落点伤害、保留全部原有文案与布局语义，并用美术色板区分操作、元素、生命和危险信息。

## 2. Non-goals

- 不修改反应算法、元素优先级、其他 `element_rule` 行或二级反应。
- 不修改 README、GD-2026-0004、输入系统、波次、生命泉、关卡、CG、面板层级或无关 UI。
- 不借机重构 `DialoguePanel`、`GamePlayPanel` 或 `HudHintBanner`。
- 不提交、不合并、不推送。

## 3. Current State

- VERIFIED：`element_rule.xlsx` 的规则 17/18 当前 `impact_damage=1`、`impact_radius=1`。
- VERIFIED：`slot_dialogue.xlsx` 的强调标签当前为 `#FF0000`；动态种子提示当前统一为 `#85c6c8`。
- VERIFIED：`GamePlayPanel.prefab` 的 `TxtSeed` 锚在种子槽右下角，代码生成的 `Q 丢弃` 也位于右下，两者重叠。
- VERIFIED：`VisualPalette` 已有水球、土球与火/电/冰伤害色；植物/生命泉尚无统一 UI 强调字段。

## 4. Constraints

- 保持四层依赖与热路径 0 GC；富文本处理只允许在装配、表加载或非热路径提示生成时执行。
- CEO 临时授权修改两份 XLSX，但必须读入原工作簿后定向改单元格，保留工作表结构、子表头与数据验证。
- 生成 JSON 只能由 Luban 导入产生；不得手改 `Assets/StreamingAssets/Luban/*.json`。
- `.prefab`、`.asset`、`VisualPalette` 序列化字段和测试修改均在本计划明确授权。
- 如需要新增本计划未列出的文件、schema、依赖或验收标准，停止并升版。

## 5. Proposed Design

- 配表行为：把 `element_rule` 规则 17/18 的 `impact_radius` 改为 0。`ApplyTileChange` 因半径为 0 只强制落点保持原地貌，`ApplyImpact` 走单格伤害分支，因此保留 1 点伤害并停止相邻铺格。
- HUD 布局：在 Prefab 中把 `TxtSeed` 改为右上锚点，保持尺寸与字体；左下 E、右下 Q 不变。
- 静态剧情配色：对 `slot_dialogue` 的指定红色标签原位换色，不改变正文、粗体、方括号或句子结构。
- 动态种子配色：`VisualPalette` 增加 `SeedHighlightColor(SeedType)`；火/电/冰映射现有元素色，植物与生命语义使用 `#7EB88A`。`HudHintBanner` 接收 `SeedType` 并把返回色转为富文本颜色。
- 不新增通用富文本引擎；本次仍是逐句显式标注，动态种子名为唯一运行时例外。

## 6. File Scope

- READ: `AGENTS.md`、`AI_Context/TASKS/GD-2026-0005/**`、`AI_Context/BUILD_AND_TEST.md`、`Docs/任务/《美术视觉工程化执行宪章》.md`
- MODIFY: `ConfigWorkspace/Data/element_rule.xlsx`
- MODIFY: `ConfigWorkspace/Data/slot_dialogue.xlsx`
- MODIFY: `Assets/StreamingAssets/Luban/dso_tbelementrule.json`（仅 Luban 生成）
- MODIFY: `Assets/StreamingAssets/Luban/dso_tbslotdialogue.json`（仅 Luban 生成）
- MODIFY: `Assets/Resources/ui/Panel/GamePlayPanel.prefab`
- MODIFY: `Assets/Scripts/Data/Settings/VisualPalette.cs`
- MODIFY: `Assets/Resources/tuning/VisualPalette.asset`
- MODIFY: `Assets/Scripts/Presentation/UI/Component/HudHintBanner.cs`
- MODIFY: `Assets/Tests/Runtime/Editor/地块范围Tests.cs`
- MODIFY: `Assets/Tests/Runtime/Editor/引导与UI反馈Tests.cs`
- CREATE: `AI_Context/TASKS/GD-2026-0005/EXECUTION_REPORT_A01.md`
- FORBIDDEN: 所有未列出的 `Assets/**`、`ConfigWorkspace/Data/*.xlsx`、`ProjectSettings/**`、`Packages/**`、`Tools/**`、`scripts/**`，以及 `README.md`、`AI_Context/TASKS/GD-2026-0004/**`
- 用户手工清单：none；本任务由 CEO 临时授权 Worker 执行两份 XLSX 的定向单元格修改。
- 需重审条件：新增 schema、修改其他规则、增加新序列化资产、修改无关 prefab、改变文案内容或测试门禁。

## 7. Execution Steps

| # | 前置条件 | 路径 / 位置 | 操作 | 预期结果 | 检查方式 |
| --- | --- | --- | --- | --- | --- |
| 1 | 工作区干净、基线匹配 | 仓库根 | 创建并切到 `ai/GD-2026-0005` | 仅任务分支施工 | `git status` / `git rev-parse` |
| 2 | 读取批准范围 | 本目录 | Preflight 核对 | 无范围冲突 | `validate_handover.py --for worker` |
| 3 | 保留工作簿 | `element_rule.xlsx` | 将 `Sheet1!I21`、`I22` 从 1 改为 0 | 规则 17/18 只伤害不扩散 | openpyxl 只读复核 + Luban 导入 |
| 4 | 读取原工作簿 | `slot_dialogue.xlsx` | 仅替换 `context` 列已有红色标签 | 文案结构与标点不变 | 字符串 Diff / EditMode 文本测试 |
| 5 | 正常导表 | `ConfigWorkspace` | 执行项目既有 Luban 导入 | 两份 JSON 镜像更新 | `pwsh Tools/luban-mirror.ps1` |
| 6 | UI 修复 | `GamePlayPanel.prefab` | `TxtSeed` 改右上锚点 | 次数不被 Q 遮挡 | Prefab Diff + Play 视觉观察 |
| 7 | 动态色映射 | `VisualPalette.cs/.asset`、`HudHintBanner.cs` | 增加种子语义色与调用 | 种子名按类型分色 | 单元测试 + Play |
| 8 | 回归 | 两份测试文件 | 新增/修正断言 | 证明伤害保留、扩散停止、文本分色 | EditMode Test Runner |
| 9 | 收口 | 本目录 | 写 `EXECUTION_REPORT_A01.md` | 11 节报告完整、命令证据真实 | 人工审阅 + validator |

## 8. Engineering Rules

- `VisualPalette.SeedHighlightColor` 只做常数映射；不得在每次 Tick 扫描文本。
- `HudHintBanner` 只在波前支援提示生成时拼接颜色，不在热路径执行。
- XLSX 修改必须保留 Sheet、子表头、数据验证与未改单元格；保存后重新读取并核对目标单元格。
- 颜色表固定为：操作/数值 `#F9CE5A`；水 `#338CFF`；土/地貌 `#D0B185`；火 `#FF2D1A`；电 `#884CFF`；冰 `#66CDFF`；植物/生命 `#7EB88A`；危险 `#DA684C`。
- 目标红色标签行：`id = 7, 8, 10, 11, 12, 15, 16, 18, 19, 23, 28, 30, 36, 37, 49, 50, 66`。
- 测试不得删除既有断言；规则 17/18 的旧半径 1 断言必须改成新真值 0，并新增行为断言。

## 9. Acceptance Criteria

- AC-01：`基础水 + 水球` 与 `基础土 + 土块` 落点分别结算 1 点伤害，落点保持基础水/基础土，四邻格状态不变。
- AC-02：种子次数在 1–3 时完整显示于槽位右上角，不被 `Q 丢弃` 覆盖或裁切。
- AC-03：剧情表不再含 `#FF0000` 强调，操作/水/土/生命泉等命中目标语义色。
- AC-04：动态火、电、冰、植物种子提示各自包含对应色值。
- AC-05：编译门 0 error / 0 warning；Luban 镜像无差异；`git diff --check` 通过。
- AC-06：实际修改 100% 位于 File Scope，越权为 0。

## 10. Test Plan

- V0：`python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0005 --for worker`
- V1：`pwsh Tools/comment-lint.ps1`；记录当前既有 `TimerWheel.cs`、`SaveSlot.cs` 基线红灯，不新增超预算文件。
- V2：`pwsh Tools/compile-gate.ps1 -Topology`，要求 0 error / 0 warning。
- V3：Unity Test Runner → EditMode → Run All；新增同元素行为与文本颜色断言，记录通过/失败数。
- 配表专项：导表后执行 `pwsh Tools/luban-mirror.ps1`，要求待删除 0、待覆盖 0、孤儿 meta 0。
- Diff：`git diff --check`，并逐路径核对 File Scope。
- V4：`Level_0` 实跑；检查同元素落点、种子次数、1001/1002/4001 剧情与波前种子提示。
- V5：NOT APPLICABLE；本次不改持续热路径与资源规模。

## 11. Risks

| 风险 | 影响 | 概率 | 缓解 |
| --- | --- | --- | --- |
| XLSX 保存损坏格式 | 策划表不可编辑 | 中 | read-modify-save；重新读取并检查子表头、验证范围、非目标行 |
| 手改生成 JSON 被执行覆盖 | 结果不可复现 | 中 | 只运行 Luban，禁止直接编辑 JSON |
| 移动计数仍与角标相碰 | UI 未修复 | 低 | Prefab 锚点改右上并实机检查 1/2/3 位数 |
| 富文本拼色引入运行时分配 | 0 GC 风险 | 低 | 只在低频提示生成路径执行 |
| 旧测试仍钉死半径 1 | 红灯或假绿 | 高 | 同步修正规则 17/18 断言并新增行为测试 |

## 12. Rollback

- 基线：`main@03d8913e6ef755ca675bea5bd71b0526097d4faf`。
- 先把两份 XLSX 恢复为基线副本，再运行正常 Luban 导入恢复 JSON。
- 使用 `git checkout -- <File Scope 内文件>` 仅恢复本任务列出的文件；不得重置整仓。
- 不得覆盖用户或其他任务未提交修改。

## 13. Open Questions

- 阻塞：none。
- 非阻塞假设：“雷种子”是用户对“电种子”的口语称呼，保留正式名称。
- 非阻塞假设：Unity EditMode 可能由 CEO 在 Test Runner 手工执行；Worker 必须如实标记。

## 14. Approval

- Decision: APPROVED
- Plan Version: v01
- Approved Scope: 见第 6 节。
- Conditions: CEO 临时授权 Agent 定向修改两份 XLSX；仅改指定单元格，保留工作簿结构。
- Explicitly Forbidden: 生成物手改、无关文件、schema/依赖/公共接口扩展、提交/推送。
- Base Commit: `03d8913e6ef755ca675bea5bd71b0526097d4faf`
- Approver: CEO
- Approved At: 2026-11-26 09:52 Asia/Shanghai
