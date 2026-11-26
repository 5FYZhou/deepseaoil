--- START OF TASK ---
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-2026-0004
- Plan Version: v02
- Status: APPROVED
- Task Level: L1 / Risk Level: R1
- Project / Engine / Platform: 荒原复苏指南 (deepseaoil) / Unity 2022.3.62f3c1 / Windows (URP 2D)
- Branch / Base Commit: `main` / `254b9cf1a2de413cb02594c6f2e2730fb06171a3`（工作区含已生成的 v01 媒体与未提交修改，见第 3 章）
- Planner: 外部 Planner（Chat 模式 · `planner-orchestrator` + `task-brief-writer` + `game-systems-designer` + `unity2d-architect`）
- Worker: 按 `AGENTS.md` 唯一路由表由 CEO 指定 Agent 模式执行
- Watchdog: 独立实例验证（按 L1 流程由 CEO 安排最终复核）
- Context: `00_CONTEXT_MANIFEST.md` + `NEED_FILES/ROUND_01` + `NEED_FILES/ROUND_02` + `254b9cf1a2de413cb02594c6f2e2730fb06171a3`
- Execution Attempt Cap: A01–A03
- Created At: 2026-11-26

## 1. Objective
将根 `README.md` 从已落盘但未验收的 v01 版本替换为收敛美观的 v02 版本：在保留所有已核验游戏规则、工程入口与 4 个本地媒体/小黑盒外链的前提下，严格以 `EndPanel.cs` 为唯一署名真源重构制作组表格，精简首屏与 Mermaid（≤ 2 张），并以 `<details>` 折叠开发者长文档以兼顾桌面与移动端阅读。

## 2. Non-goals
- 不修改或重新生成 `Docs/媒体/README/` 下的 4 个媒体文件与 `来源说明.md`（直接复用现有 v01 媒体产物）。
- 不修改 `Docs/README.md` 及其他任何子目录文档，不处理工作区其他并发文档回写任务的修改。
- 不修改任何 C# 脚本（含 `EndPanel.cs`）、`.asmdef`、Unity 场景、预制体、调参 SO、Excel 源表、Luban 生成物或 `AI_Context/` 长期上下文正文。
- 不读取、引用、修改或提交 `Docs/玩家反馈-数据分析.zip`，不在 README 中泄露本地 `T:\` 源目录路径。
- 不承诺尚未完成的完整通关验收、V5 性能采样或公开发布日期。

## 3. Current State
- **[VERIFIED]** `README.md` 当前为 v01 落盘版本（`L3__README_current_v01.md`），存在 `EndPanel.cs` 未支持的职责扩写/分组改写、3 张 Mermaid 图表、宽 HTML `<table>` 媒体双栏布局以及未折叠的开发者长文本。
- **[VERIFIED]** `Assets/Scripts/Presentation/UI/Panel/EndPanel.cs` 的 `CreditLines` 唯一定义了游戏内制作组署名（策划：代码鱿鱼、秋墨；文案：代码鱿鱼；程序：枫叶舟、V.Reason-愚人舟；美术：Banth、芝麻王、爵士JAZZEL；运营：鹦鹉螺；特别鸣谢 音乐 / 音效：李tingke；特别鸣谢 美术：既灵）。
- **[VERIFIED]** 小黑盒复盘帖标题为《你胆子这是肥嘟嘟的，居然敢用AI开发游戏》，链接已于 `External_小黑盒复盘帖.md` 核验。
- **[VERIFIED]** 4 个本地媒体文件已位于 `Docs/媒体/README/`（`readme-promo.webp`、`readme-level-overview.webp`、`readme-steam-demo.gif`、`readme-hit-demo.gif`），连同 `icon_game_main.png` 与 `illust_cover_game.png` 均真实存在。
- **[VERIFIED]** 玩法与工程真源：`5 / 9 / 13` 格反应范围、生命泉约 1.65s 加速回血与缓降、`WASD` / `Shift / Space` / `LMB / RMB` / `E` / `Q` / `Esc` 键位、14 张 Luban 表（`TablesMeta.cs`）、Unity `2022.3.62f3c1` / URP `14.0.12`、三道工程门禁及 `v2.0` 内部可玩版状态。
- **[OBSERVED]** 工作区当前相对 `main@254b9cf` 包含 `M README.md`、`M Docs/README.md`、`?? Docs/媒体/`、`?? AI_Context/TASKS/GD-2026-0004/` 及既有并发修改（不得覆盖或回滚非本任务文件）。

## 4. Constraints
- `README.md` 是本次唯一允许修改的文件；严格禁止修改或删除 `Docs/媒体/README/**` 与 `Docs/README.md`。
- 制作组表格必须逐字对齐 `EndPanel.cs`，禁止任何职责扩写、分组改写与 Mermaid `mindmap`。
- 全文 Mermaid 图表数量 ≤ 2 张（核心循环 + 四层架构）。
- 媒体展示不得使用容易在窄屏下挤压的 HTML `<table>`，必须保证窄屏自动退化为单列；禁止引用 `T:\` 本地绝对路径、远程占位图与 `Docs/玩家反馈-数据分析.zip`。
- 保持 `.md` 文件 UTF-8（无 BOM）与仓库约定的 CRLF 行尾，无尾随空白。

## 5. Proposed Design
使用本轮交付的 `README v02` Markdown 全文（第一节）直接替换根 `README.md`：
1. **首屏收口**：保留居中图标、纯标题、一句定位、4 枚徽章与 `illust_cover_game.png`，移除顶部 9 锚点导航。
2. **核心玩法与媒体层**：保留核心循环 Mermaid（#1）与精简生存要点；将 `Docs/媒体/README/` 下 2 静（`readme-promo.webp`、`readme-level-overview.webp`）与 2 动（`readme-steam-demo.gif`、`readme-hit-demo.gif`）改用无表格 `<p align="center">` 流式容器，外接小黑盒复盘帖链接。
3. **反应、键位与更新**：用 3 列紧凑短表替代反应 Mermaid 流程图，精准覆盖 `5 / 9 / 13` 格与 4 种种子；列明 `Shift / Space` 等全部现行键位及 `v2.0` 更新摘要与 `v2.0更新说明.docx` 入口。
4. **开发者折叠层与署名**：使用 3 个独立 `<details>` 折叠块承载环境/门禁、四层架构（含 Mermaid #2）与 14 张配表边界、文档地图；文末以 2 列普通 Markdown 表格呈现 `EndPanel.cs` 唯一署名。

## 6. File Scope
- **READ**:
  - `README.md`（当前 v01 文件）
  - CEO 批准后的 `README v02` 定稿文本
  - `Assets/Scripts/Presentation/UI/Panel/EndPanel.cs`（如需在现场抽核署名）
- **CREATE**: 无
- **MODIFY**:
  - `README.md`
- **FORBIDDEN**:
  - `Docs/媒体/**`（已有 v01 媒体与来源说明只读复用，不得改写/删改）
  - `Docs/README.md` 及 `Docs/**` 其余全部文件（尤其禁止读取/修改/提交 `Docs/玩家反馈-数据分析.zip`）
  - `Assets/**`、`ConfigWorkspace/**`、`Tools/**`、`scripts/**`、`ProjectSettings/**`、`Packages/**`、`AI_Context/**`、`AGENTS.md`

## 7. Execution Steps
1. **Preflight 检查**（前置条件：持有 CEO 批准的 `APPROVAL_RECORD.md`）
   - 动作：执行 `git status --short --branch` 与 `git rev-parse HEAD`，记录基线 `254b9cf` 与既有工作区状态（确认保留 `Docs/媒体/`、`Docs/README.md` 及并发文档修改）。
   - 预期：环境与目标文件状态清楚，不触碰范围外文件。
2. **替换为 `README v02`**（前置条件：Preflight 通过）
   - 动作：将批准版 `README v02` Markdown 写入根目录 `README.md`，保证 UTF-8（无 BOM）与 CRLF 行尾。
   - 预期：`git status --short` 相对施工前仅更新了 `README.md` 内容，未新增/改动其他任何文件。
3. **静态、链接与合规核验**（前置条件：步骤 2 完成）
   - 动作：运行 `git diff --check -- README.md`；用只读脚本校验所有图片/相对路径存在性、Mermaid 块数量（`<= 2`）、禁用词清零（含职责扩写词、过期规则词、`via.placeholder.com`、`T:\`）及 `EndPanel.cs` 署名一致。
   - 预期：`git diff --check -- README.md` 退出码 `0`；所有校验项全部通过。
4. **输出执行报告**（前置条件：步骤 3 完成）
   - 动作：整理真实命令输出、`git diff --stat -- README.md` 与 AC 逐条证据提交 Watchdog / CEO；不自行提交 Git。
   - 预期：证据完整可复核。

## 8. Engineering Rules
- 严禁修改、重命名、压缩或删除 `Docs/媒体/README/` 下已有的 4 个媒体文件。
- 严禁触碰或覆盖工作区其他并发文档修改及 `Docs/玩家反馈-数据分析.zip`。
- 保持 `.md` 规范行尾（CRLF）且无行尾空格；不运行修改仓库历史的 Git 命令。

## 9. Acceptance Criteria
- `AC-01`: 根 `README.md` 已替换为完整 `README v02`；首屏仅保留图标、标题、一句定位、徽章与主视觉，无顶部锚点导航堆积。（证据：`README.md` 首屏源码对比）
- `AC-02`: 制作组名单使用普通 Markdown 表格且与 `EndPanel.cs` 逐字一致，无任何个人职责扩写、改写分组名或 Mermaid `mindmap`。（证据：`README.md` 与 `EndPanel.cs` 行对照）
- `AC-03`: 全文 Mermaid 图表数量 `<= 2`（核心循环 + 四层架构）；反应机制采用紧凑短表且准确保留 `5 / 9 / 13` 格与生命泉约 `1.65s` 加速回血/缓降、`Shift / Space`、14 张表及 `v2.0` 内部可玩版状态。（证据：```` ```mermaid ```` 计数与关键字段校验输出）
- `AC-04`: 保留小黑盒复盘帖外链与 4 个本地媒体（`Docs/媒体/README/*`），且未使用 HTML `<table>` 布局媒体区；未出现 `T:\` 路径、远程占位图或 `Docs/玩家反馈-数据分析.zip`。（证据：`README.md` 媒体区检查与关键字检索零命中）
- `AC-05`: 开发者长内容（环境/门禁、架构/配表/0 GC、文档地图）已封装进 `<details>` 折叠块，且文中所有相对路径和本地图片 `Test-Path` 均为真。（证据：只读路径检查脚本输出）
- `AC-06`: 全文无装饰性 Emoji 滥用与过期描述（“原地 3 秒”“动一下归零”“21 / 37 格”等为零命中）。（证据：关键词检索输出）
- `AC-07`: `git diff --check -- README.md` 退出码 `0`；工作区除 `README.md` 外其余既有改动保持原状，未改动 `Docs/媒体/`、`Docs/README.md` 或源码资产。（证据：`git status --short` 与 `git diff --stat` 输出）

## 10. Test Plan
本任务仅修改 `README.md` 单文件：

| 检查项 | 命令（工作目录：仓库根目录） | 期望结果 | 当前状态 |
| --- | --- | --- | --- |
| Diff 格式与空白检查 | `git diff --check -- README.md` | 退出码 `0`，无空白/冲突标记 | `NOT RUN` |
| 变更范围核对 | `git status --short` 与 `git diff --stat -- README.md` | 除既有前置状态外仅改写 `README.md`，未触碰 `Docs/媒体/` 等禁区 | `NOT RUN` |
| 链接、媒体、Mermaid 与禁用词校验 | 只读校验脚本：核对所有本地图片/文档路径存在性、`mermaid` 块数量 `<= 2`、无 `<table>` 媒体布局、无 `T:\` / 占位图 / 过期词 / 扩写词 | 全部通过，零死链、零违禁命中 | `NOT RUN` |
| 编译门 / 注释门 / 导表门 / EditMode | `compile-gate.ps1` / `comment-lint.ps1` / `luban-mirror.ps1` / EditMode | 本任务未改动 C#、注释、配表或资产 | `NOT APPLICABLE` |

## 11. Risks
- **R-01（低）**：`<details>` 折叠块内部包含标题、列表、表格和 Mermaid 代码块时，少数非主流本地 Markdown 查看器可能要求严格空行。
  - 缓解：草稿已在所有 `<summary>` 后、`</details>` 前以及代码块周围留出标准空行；落盘后在目标平台（GitHub / VS Code）抽查折叠展开态渲染。
- **R-02（低）**：工作区存在并发文档修改与 `README.md` 已有 v01 变更，误用整库覆盖或回滚指令可能破坏他人成果。
  - 缓解：File Scope 与 Rollback 均严格限制为单文件 `README.md`，禁止全仓 `git checkout` 或 `git reset`。

## 12. Rollback
- 若 v02 落盘后需退回至施工前（即保留已有 `v01` 与工作区现状）或退回至 `main@254b9cf`：
  - 退回至施工前 v01：施工前先将当前 `README.md` 备份至任务临时目录或对比 patch，仅覆盖恢复 `README.md`；
  - 若需彻底回退到基线提交版本：仅对单文件执行 `git checkout 254b9cf -- README.md`（严禁对其他路径运行回滚，确保不破坏 `Docs/媒体/`、`Docs/README.md` 及并发修改）。

## 13. Open Questions
- 无阻塞项。
- CEO 已批准直接执行，并追加唯一文案修订：首屏主文案必须为“当资源趋近枯竭，当大地不断呐喊，而你只有两个瓶子和一袋种子。”

## 14. Approval
- Approved Plan Version: v02
- Allowed Files: `README.md`
- Conditions / Constraints: 使用 `README_DRAFT_v03.md`；首屏主文案逐字采用 CEO 指定版本；其余沿用 v02；保留并发未提交修改。
- Forbidden Changes: 除 `README.md` 外的所有仓库文件；特别是 `Docs/媒体/**`、`Docs/README.md`、`AI_Context/**`、`Docs/玩家反馈-数据分析.zip`。
- Approved By: CEO
- Approved At: 2026-11-26 01:13 (UTC+8)
--- END OF TASK ---
