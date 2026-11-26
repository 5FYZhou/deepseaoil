--- START OF TASK ---
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-2026-0004
- Plan Version: v01
- Status: APPROVED
- Task Level: L1 / Risk Level: R1
- Project / Engine / Platform: 荒原复苏指南 (deepseaoil) / Unity 2022.3.62f3c1 / Windows (URP 2D)
- Branch / Base Commit: `main` / `254b9cf1a2de413cb02594c6f2e2730fb06171a3`
- Planner: 外部 Planner（Chat 模式 · `planner-orchestrator` + `task-brief-writer` + `game-systems-designer` + `unity2d-architect`）
- Worker: 按 `AGENTS.md` 唯一路由表由 CEO 指定 Agent 模式执行
- Watchdog: 独立实例验证（按 L1 流程由 CEO 指定或组织最终复核）
- Context: `00_CONTEXT_MANIFEST.md` + `NEED_FILES/ROUND_01` + `254b9cf1a2de413cb02594c6f2e2730fb06171a3`
- Execution Attempt Cap: A01–A03
- Created At: 2026-11-26

## 1. Objective
使用经过真实源码、Luban 导出数据、`v2.0` 更新文档与小黑盒复盘素材核验后的完整 Markdown 草稿替换根 `README.md`，使其同时承担游戏对外主页（核心循环、反应与范围、键位、v2.0 更新摘要、实机截图、制作组）与工程开发入口（Unity 环境、启动场景、三道门禁、四层架构、配表边界、热路径 0 GC 与文档地图）。

## 2. Non-goals
- 不修改任何 C# 脚本、`.asmdef`、Unity 场景（`.unity`）、预制体（`.prefab`）、调参资产（`.asset`）、Excel 源表（`.xlsx`）或生成物目录。
- 不修改 `AI_Context/` 下的长期上下文正文（如 `README_AI.md`、`MODULE_MAP.md`、`KNOWN_ISSUES.md`、`PROJECT_OVERVIEW.md` 的 Context Drift 留待后续专项回写）。
- 除本次明确新增的 README 展示媒体与 `Docs/README.md` 媒体索引外，不修改其他 `Docs/**` 正文。
- 不读取、引用、修改或提交工作区未跟踪文件 `Docs/玩家反馈-数据分析.zip`。
- 不在文档中承诺尚未验证的完整通关完成状态、V5 性能采样数据或公开发布日期。

## 3. Current State
- **[VERIFIED]** `README.md` 当前仍包含 `via.placeholder.com` 远程占位图（第 24、63、112 行），且包含过期生命泉描述与仅列 `Left Shift` 的旧键位表（来自 `L3__README.md`）。
- **[VERIFIED]** 当前真源事实（来自 `NEED_FILES/ROUND_01`）：
  - 生命泉：`LifeFountainState.cs` 与 `Fountain.cs` 实现触发半径 `×1.15`、加速充能 `1.0 → 2.8/s`（约 1.65s 回 1 血）、圈内移动 `-0.35/s` / 离圈 `-0.60/s` 缓降。
  - 键位：`InputSys.inputactions` 已将 `Left Shift` 与 `Space` 同绑 `Player/Dash`，并由 `InputProvider.cs`、`PlayerController.cs`、`CombatRoot.cs` 消费 `WASD`、`Shift/Space`、`LMB/RMB`、`E`（播种）、`Q`（丢弃种子）、`Esc`（暂停）。
  - 反应范围：`dso_tbelementrule.json` 与 `dso_tbelementduoreaction.json` 当前作用半径为 `1`（5 格）、`1.5`（9 格）、`2`（13 格），且 `254b9cf` 已按 `priority`（覆盖秩）执行次级扩散。
  - 配表数量：`TablesMeta.cs` 与 `Assets/StreamingAssets/Luban/` 共有 14 张登记表。
  - 本地媒体与文档：`Assets/Resources/art/CG/illust_cover_game.png`、`Assets/Resources/Icons/icon_game_main.png`、`Assets/Resources/Icons/Seed_*.png` 与 `Docs/更新说明/v2.0更新说明.docx` 等路径均已存在于路径清单中。
  - **[VERIFIED]** 小黑盒复盘素材目录 `T:\杂七杂八\4.游戏开发\复盘帖` 存在，内含宣传图、关卡总览、光照、粒子与受击等 32 个图片/GIF；本任务只从中选少量展示素材，不复制整个目录。
- **[OBSERVED]** 工作区当前存在本任务修改与另一个“文档回写计划”Worker 产生的多个 `AI_Context/**`、`Docs/**` 修改；另有未跟踪任务目录 `?? AI_Context/TASKS/GD-2026-0004/` 与禁触文件 `Docs/玩家反馈-数据分析.zip`。本任务执行时必须保留这些并发改动，不得还原、覆盖或批量暂存。

## 4. Constraints
- 基线为 `main@254b9cf1a2de413cb02594c6f2e2730fb06171a3`。
- 主产物为 `README.md`；媒体文件只允许写入 `Docs/媒体/README/`，并同步更新 `Docs/README.md` 媒体索引。
- 不得保留任何远程占位图 URL；所有图片与相对文档路径必须与仓库真实路径逐字一致。
- 外部截图目录是只读素材源，不得在最终 README 中直接引用 `T:\` 路径；必须复制并优化为仓库内相对路径。
- 严禁触碰、读取或暂存 `Docs/玩家反馈-数据分析.zip`，不得覆盖既有未提交的 `AI_Context/PROJECT_BOARD.md`。
- 不得添加、删除或升级任何工程依赖。

## 5. Proposed Design
采用已交付的 `README.md` 完整草稿（见本回复第二节）作为唯一落盘内容：
1. **对外主页层**：以仓库现有 `icon_game_main.png` 与 `illust_cover_game.png` 作为主视觉；用精炼 Mermaid 流程图与核对后的文字说明核心循环、生命泉加速/缓降规则、5 / 9 / 13 格反应网络、4 种元素种子及全部当前键位。
2. **实机媒体层**：从外部复盘目录选取 4 个代表素材：
   - `宣传图.png` → `Docs/媒体/README/readme-promo.webp`
   - `关卡总览图.png` → `Docs/媒体/README/readme-level-overview.webp`
   - `粒子-汽.gif` → `Docs/媒体/README/readme-steam-demo.gif`
   - `受击形变.gif` → `Docs/媒体/README/readme-hit-demo.gif`
   使用 Pillow 将两张 PNG 转为宽度分别不超过 1200 / 1800 的 WebP，GIF 保持动画；新增素材总量控制在 6 MB 内。README 同时提供小黑盒复盘帖外链 `https://www.xiaoheihe.cn/app/bbs/link/7acd72af7ca7?h_camp=link&new_post_share_style_v2=1&h_src=YXBwX3NoYXJl`。
3. **版本同步层**：新增 `v2.0` 更新摘要章节并链接 `Docs/更新说明/v2.0更新说明.docx`，如实标注当前为“内部可玩版”，不夸大未验证项。
4. **开发入口层**：集中列出 Unity `2022.3.62f3c1`、URP `14.0.12`、`Begin.unity` / `Level_0` / `Range.unity` 启动说明、三道工程门禁（`compile-gate.ps1 -Topology`、`comment-lint.ps1`、`luban-mirror.ps1`）及 EditMode Test Runner 入口，并如实提示注释门既有基线状况。
5. **架构与索引层**：明确四层 `asmdef` 拓扑与引用不传递铁律、`GameRoot` 唯一组合根、热路径 0 GC、14 张 Luban 表与调参 SO 的边界、`TileGridLightmap` 核心原理、完整 `Docs/` 文档地图以及制作组名单。

## 6. File Scope
- **READ**:
  - CEO 批准后的 `README.md` 最终文本来源（`PLAN_v01.md` 附件 / CEO 确认稿）
  - `README.md`（写入前基线核对）
  - `T:\杂七杂八\4.游戏开发\复盘帖\宣传图.png`
  - `T:\杂七杂八\4.游戏开发\复盘帖\关卡总览图.png`
  - `T:\杂七杂八\4.游戏开发\复盘帖\粒子-汽.gif`
  - `T:\杂七杂八\4.游戏开发\复盘帖\受击形变.gif`
- **CREATE**:
  - `Docs/媒体/README/readme-promo.webp`
  - `Docs/媒体/README/readme-level-overview.webp`
  - `Docs/媒体/README/readme-steam-demo.gif`
  - `Docs/媒体/README/readme-hit-demo.gif`
  - `Docs/媒体/README/来源说明.md`
- **MODIFY**:
  - `README.md`
  - `Docs/README.md`（仅登记新的媒体目录）
- **FORBIDDEN**:
  - `Assets/**`（全部源码、`.asmdef`、场景、预制体、调参资产、美术资源、StreamingAssets、Tests）
  - `ConfigWorkspace/**`
  - `Tools/**`
  - `scripts/**`
  - `ProjectSettings/**`、`Packages/**`
  - `AI_Context/**`（含已有未提交修改的 `AI_Context/PROJECT_BOARD.md`）
  - `Docs/**`，除本计划明确列出的 `Docs/README.md` 与 `Docs/媒体/README/**`
  - `Docs/玩家反馈-数据分析.zip`（严禁读取、修改或提交）
  - `AGENTS.md`

## 7. Execution Steps
1. **Preflight 检查**（前置条件：持有 CEO `APPROVAL_RECORD.md`）
   - 动作：运行 `git status --short --branch` 与 `git rev-parse HEAD`，确认位于批准基线 `254b9cf`，且 `README.md` 无冲突未提交修改。
   - 预期：输出当前分支与基线一致，确认不触碰 `AI_Context/PROJECT_BOARD.md` 与 `Docs/玩家反馈-数据分析.zip`。
2. **准备 README 展示媒体**（前置条件：Preflight 通过，外部截图目录可读）
   - 动作：用 Pillow 将 `宣传图.png` 与 `关卡总览图.png` 转为优化 WebP，复制两张 GIF 到 `Docs/媒体/README/`，并写入来源说明与小黑盒复盘帖链接。
   - 预期：4 个媒体路径存在，新增总大小不超过 6 MB，外部原目录不被修改。
3. **写入 `README.md` 草稿**（前置条件：Preflight 与媒体准备通过）
   - 动作：将 CEO 批准版 `README.md` Markdown 草稿完整写入仓库根目录 `README.md`，保持 UTF-8（无 BOM）与仓库 `.md` 规范行尾（CRLF）。
   - 预期：`git status --short` 仅显示 `M README.md`、`M Docs/README.md` 与新增媒体目录（原有未提交项保持原样）。
4. **静态与本地链接/违禁词校验**（前置条件：步骤 3 完成）
   - 动作：在仓库根目录运行 `git diff --check`，检查过期词清零、远程占位 URL 清零，以及 `README.md` 中所有本地相对路径/图片路径全部存在于文件系统。
   - 预期：`git diff --check` 退出码 `0`；所有链接存在且无违禁过期表述。
5. **交付报告**（前置条件：步骤 4 完成）
   - 动作：向 CEO / Watchdog 输出实际 Diff 统计与命令输出证据，不自行提交 Git、不自批验收。
   - 预期：所有 AC 均有真实命令输出或文件核对证据。

## 8. Engineering Rules
- 代码与工程外资产不变；新增媒体只允许落在 `Docs/媒体/README/`。
- 保持 `.md` 文件 CRLF 行尾格式且无行尾多余空格，确保 `git diff --check` 零告警。
- 不运行修改仓库状态的 Git 命令（如 `git add` / `git commit` / `git checkout --` 其它文件）。
- 若发现 `README.md` 链接对应文件在本地意外缺失，立即停止并报告，不得擅自修改目标文件或编造替代路径。

## 9. Acceptance Criteria
- `AC-01`: 根 `README.md` 已替换为完整、自足且可正常渲染（Markdown 表格与 Mermaid 语法闭合正确）的内容，无 `via.placeholder.com` 等远程占位图 URL。（证据：`README.md` 全文及链接正则检索输出）
- `AC-02`: 全文不包含过期游戏规则表述（如旧的 3 秒原地不动归零规则、旧反应格数范围描述或旧泥浆/旧怪血量错位）。（证据：关键词检索零命中输出）
- `AC-03`: 明确并准确覆盖：一句话定位与核心循环、生命泉约 1.65s 加速回血与缓降规则、`5 / 9 / 13` 格反应机制、完整键位（`WASD`、`Shift / Space`、`LMB / RMB`、`E`、`Q`、`Esc`）、v2.0 更新摘要及 `Docs/更新说明/v2.0更新说明.docx` 入口。（证据：`README.md` 对应章节文本）
- `AC-04`: 明确并准确覆盖工程开发入口：Unity `2022.3.62f3c1`、`Begin.unity` / `Level_0` / `Range.unity` 入口、三道工程门禁、四层 asmdef 边界、14 张 Luban 表与调参 SO 边界、热路径 0 GC、文档地图与制作组名单。（证据：`README.md` 对应章节文本）
- `AC-05`: `README.md` 中引用的所有本地图片路径与 `Docs/` / `AI_Context/` / `Assets/` 相对链接在当前工作区 100% 真实存在，且不包含 `Docs/玩家反馈-数据分析.zip`。（证据：PowerShell / Python 路径存在性脚本校验输出）
- `AC-06`: 4 个 README 展示媒体均存在且新增总量不超过 6 MB；README 使用仓库内相对路径引用，并提供小黑盒复盘帖外链。（证据：文件清单、尺寸/大小审计与链接扫描）
- `AC-07`: `git diff --stat` 仅体现预期的 README、Docs 索引与媒体目录变更，未覆盖既有未提交修改；`git diff --check` 退出码为 `0`。（证据：终端命令输出）

## 10. Test Plan
本任务为纯文档重构（不修改 `.cs`、`.asmdef` 或 `.xlsx`），按 `AI_Context/BUILD_AND_TEST.md` 执行适用检查：

| 检查项 | 命令（工作目录：仓库根目录） | 期望结果 | 当前状态 |
| --- | --- | --- | --- |
| Diff 格式与空白检查 | `git diff --check -- README.md` | 退出码 `0`，无行尾空白或冲突标记 | `NOT RUN`（待落盘后执行） |
| 变更范围核对 | `git status --short` | 仅预期 `README.md` 变为 `M`，未动禁区及既有修改 | `NOT RUN`（待落盘后执行） |
| 相对路径与图片存在性校验 | 本地只读路径遍历校验脚本（检测 `README.md` 所有 `src=` / `]()` 相对路径 `Test-Path`） | 全部返回 `True`，零死链，零远程 URL | `NOT RUN`（待落盘后执行） |
| 展示媒体审计 | 检查 4 个媒体文件存在、格式正确、GIF 可解码、新增总大小 ≤ 6 MB | 全部通过 | `NOT RUN`（待落盘后执行） |
| 代码编译门 / 注释门 / 导表门 / EditMode | `compile-gate.ps1` / `comment-lint.ps1` / `luban-mirror.ps1` / EditMode | 不适用（未修改代码、注释、配表或资产）；若作为全仓检查则为 `NOT APPLICABLE` | `NOT APPLICABLE` |

## 11. Risks
- **R-01（中）**：部分历史子文档（如 `Docs/工程/人类接手与工程交接手册.md`、`Docs/README.md` 中“13 张表”等）仍处于 `STALE` 状态，读者从根 `README.md` 跳入子文档时可能产生认知差异。
  - 缓解：已在根 `README.md` 文档地图下方显式加注“若文档与源码、Luban 导出 JSON 或门禁输出冲突，以当前代码与门禁为准”；不在本次任务越界改写子文档。
- **R-02（低）**：部分 Markdown 预览器（如 GitHub Dark / Light 主题或本地编辑器）对 Mermaid `mindmap` 或带 `<br/>` 的标签渲染兼容性存在差异。
  - 缓解：所有流程图节点文本已用双引号包裹 `["..."]`，落盘后建议 CEO 在 GitHub 或本地 VS Code Markdown 预览确认渲染效果。
- **R-03（低）**：行尾混用 LF/CRLF 导致 `git diff --check` 或全文件无意义全量变更。
  - 缓解：写入时显式保持仓库 `.md` 的 CRLF 行尾约定并运行 `git diff --check`。
- **R-04（低）**：外部复盘素材不在仓库内，直接引用磁盘路径会在 GitHub 失效；GIF 未经压缩也可能明显增大仓库体积。
  - 缓解：只复制选定的 4 项到 `Docs/媒体/README/`，使用仓库相对路径，并执行格式、尺寸和体积审计。

## 12. Rollback
- 基线为 `main@254b9cf1a2de413cb02594c6f2e2730fb06171a3`。
- 若落盘后需回滚且尚未提交，仅对本次允许文件执行精准恢复（不触碰工作区其他未提交修改）：
  `git checkout -- README.md`
  `git checkout -- Docs/README.md`
  删除本次新建的 `Docs/媒体/README/` 下 5 个文件。
- 随后运行 `git status --short` 核实 `README.md` 已还原且 `AI_Context/PROJECT_BOARD.md`、`Docs/玩家反馈-数据分析.zip` 均未被覆盖或删除。

## 13. Open Questions
- CEO 已确认接受当前 README 草稿的语气、信息密度与小黑盒实机媒体方案。
- 落地方式：由 CEO 指定的临时 Worker 快速通道执行，完成后交 CEO 验收。

## 14. Approval
- Approved Plan Version: v01
- Allowed Files:
  - MODIFY `README.md`
  - MODIFY `Docs/README.md`（仅登记 README 展示媒体目录）
  - CREATE `Docs/媒体/README/readme-promo.webp`
  - CREATE `Docs/媒体/README/readme-level-overview.webp`
  - CREATE `Docs/媒体/README/readme-steam-demo.gif`
  - CREATE `Docs/媒体/README/readme-hit-demo.gif`
  - CREATE `Docs/媒体/README/来源说明.md`
- Conditions / Constraints:
  - README 正文使用当前 `README_DRAFT.md`，可按实际渲染做不改事实的局部排版修正。
  - 仅复制计划列出的 4 个外部截图/GIF；PNG 转为 WebP，新增媒体总量 ≤ 6 MB。
  - 所有 README 媒体引用使用仓库内相对路径，并保留小黑盒复盘帖外链。
  - 保留另一文档回写 Worker 正在产生的所有未提交修改，不得覆盖或批量暂存。
- Forbidden Changes:
  - `Assets/**`、`ConfigWorkspace/**`、`Tools/**`、`scripts/**`、`ProjectSettings/**`、`Packages/**`、`AGENTS.md`
  - 除批准文件外的 `AI_Context/**` 与 `Docs/**`
  - `Docs/玩家反馈-数据分析.zip`
- Approved By: CEO
- Approved At: 2026-11-26 00:52 (UTC+8)
--- END OF TASK ---
