# TASK BRIEF — README 对外主页与开发入口重构

> Secretary 登记稿。材料交给外部 Planner 后直接交付完整 Markdown 草稿；未经 CEO 批准不写入仓库。

Task ID: GD-2026-0004
Project / Engine / Tool Version: 荒原复苏指南 / Unity 2022.3.62f3c1

Task Level: L1    Risk Level: R1

Status: AWAITING_CEO_REVIEW

1. 我想实现什么（Objective）
   重新组织根 `README.md`，使其同时承担游戏对外主页和工程开发入口，并由 CEO 选定的外部 Planner 直接产出一份完整、可替换的 README Markdown 草稿。

2. 为什么需要它（Background）
   现有根 README 的视觉方向和信息层次可继续利用，但部分玩法事实已经落后于当前 `main`，并且远程占位图、旧生命泉规则和键位说明会误导玩家与接手者。

3. 当前行为与问题（Current Behavior / Problem）
   - 生命泉仍写“静止整整 3 秒、动一下归零”，与当前站立加速约 1.65 秒回血、短移动缓降的规则不符。
   - 冲刺只列 `Left Shift`，未同步当前 `Shift / Space`。
   - 元素反应范围仍可能让人理解为旧的 `21 / 37` 格，而当前表为 `5 / 9 / 13` 格。
   - 横幅和 GIF 使用 `via.placeholder.com` 远程占位图。
   - 页面偏宣传长文，开发入口、当前更新说明和文档地图不够集中。

4. 期望行为（Expected Behavior）
   外部 Planner 输出一份完整 Markdown，至少覆盖：
   - 游戏一句话定位、核心循环和本地主视觉；
   - 当前反应机制与准确的 5 / 9 / 13 格范围；
   - 当前键位：WASD、Shift / Space、LMB / RMB、E、Q、Esc；
   - 最近大更新摘要及 `Docs/更新说明/v2.0更新说明.docx` 入口；
   - Unity 版本、启动场景、三道工程门禁和开发入口；
   - 四层架构、Luban 配表边界、热路径 0 GC 的简明说明；
   - 文档地图与制作组名单。

5. 非目标（Non-goals）
   不修改业务代码、配置、Unity 资产、测试或 `AI_Context` 长期上下文正文；不重写 `Docs/README.md`、`AI_Context/README_AI.md` 或其他子目录 README；不承诺尚未完成的完整通关、性能验证或公开发布日期。

6. 已知约束（技术 / 性能 / 平台版本 / 必须保持不变的行为）
   - 基线为 `main@254b9cf`。
   - 根 README 是本任务唯一拟修改文件。
   - 使用仓库现有美术资源；不得保留远程占位图。
   - 保留“无直接攻击技能、靠地貌反应求生”的核心表达，但避免无法验证的营销夸张。
   - 工作区现有未跟踪文件 `Docs/玩家反馈-数据分析.zip` 不得读取、修改或提交。

7. 相关模块与文件（若已知，标注证据状态）
   - `README.md`（VERIFIED，当前待重构主页）
   - `AI_Context/PROJECT_OVERVIEW.md`（VERIFIED，定位、目标与非目标）
   - `AI_Context/ARCHITECTURE.md`（VERIFIED，分层与技术边界）
   - `AI_Context/BUILD_AND_TEST.md`（VERIFIED，门禁与环境）
   - `Docs/README.md`（VERIFIED，文档地图）
   - `Docs/工程/人类接手与工程交接手册.md`（VERIFIED，运行入口、场景与操作）
   - `Docs/更新说明/v2.0更新说明.docx`（VERIFIED，最近更新事实）
   - `Assets/Resources/art/CG/illust_cover_game.png`（VERIFIED，本地主视觉候选）
   - `Assets/Resources/Icons/icon_game_main.png`（VERIFIED，本地图标候选）
   - `T:\杂七杂八\4.游戏开发\复盘帖\`（VERIFIED，外部只读截图仓库；候选为宣传图、关卡总览图、粒子-汽 GIF、受击形变 GIF）
   - `Docs/媒体/README/`（PLANNED，上述素材优化后的仓库内展示副本）
   - `https://www.xiaoheihe.cn/app/bbs/link/7acd72af7ca7?h_camp=link&new_post_share_style_v2=1&h_src=YXBwX3NoYXJl`（VERIFIED，小黑盒完整复盘帖）
   - `AI_Context/TASKS/GD-2026-0004/NEED_FILES/ROUND_02/`（VERIFIED，补齐 EndPanel 署名真源、复盘外链、4 个媒体与当前工作区边界）
   - `AI_Context/TASKS/GD-2026-0004/NEED_FILES/ROUND_01/`（VERIFIED，外部 Planner 首轮补料；61 条来源映射、9 个自动提取文本/路径索引，含索引共 71 个平铺文件）

8. 已观察到的错误、截图或日志
   - 根 README 第 48、54、55、219 行存在旧生命泉与旧冲刺键表述。
   - 根 README 第 24、63、112 行仍含远程占位媒体。

9. 验收方式（我如何判断完成）
   - 外部 Planner 返回完整可复制的 `README.md`，不是建议清单。
   - Markdown 与 Mermaid 语法可渲染，所有本地图片及相对文档链接存在。
   - 全文不再出现“原地 3 秒”“动一下归零”“21 / 37 格”等过期表述。
   - 明确写出 Shift / Space、5 / 9 / 13 格和正确的开发门禁。
   - CEO 审阅通过后才允许落盘；文档改动执行 `git diff --check`。

10. 其他说明与不确定事项
   README v02 已按 CEO 批准直接执行，并仅替换首屏世界观文案；自动核验通过，等待 CEO 视觉验收。
