# TASK BRIEF — 玩家评论反馈驱动的 GameJam 大更新

> Secretary 登记稿。当前仅接收与整理需求，不批准方案、不启动施工。

Task ID: GD-2026-0001
Project / Engine / Tool Version: 荒原复苏指南 / Unity 2022.3.62f3c1

Task Level: PENDING    Risk Level: PENDING

Status: ACCEPTED_WITH_FOLLOW_UP

1. 我想实现什么（Objective）
   根据玩家评论原文，与 CEO 共同整理、去重和过滤诉求，形成可讨论的更新需求集合，再决定哪些诉求进入后续任务规划。

2. 为什么需要它（Background）
   本项目是 GameJam 作品，现计划依据玩家反馈进行一次宏观更新；当前重点是识别真实痛点、优先诉求与噪声，而非直接施工。

3. 当前行为与问题（Current Behavior / Problem）
   已收到第一批 10 张玩家反馈截图及 1 张 TapTap 评价页长截图，并完成原文转写；尚未进行诉求聚类、过滤、冲突识别与优先级判断。

4. 期望行为（Expected Behavior）
   收到反馈后，按“原始诉求、证据原句、问题类型、影响、重复/冲突关系、处置建议、待确认项”整理；处置建议仅作为讨论材料，最终范围由 CEO 拍板。

5. 非目标（Non-goals）
   不写业务源码、配置、资产或测试实现；不替 Planner 做技术方案；不批准计划；不承诺逐条评论都进入开发。

6. 已知约束（技术 / 性能 / 平台版本 / 必须保持不变的行为）
   遵守根 `AGENTS.md` 与 Agent Workflow Kit v3；保留生成物禁区边界；涉及架构、程序集、公共接口、配表、资产或验收标准时，须后续通过 Planner 生成草案并由 CEO 批准。

7. 相关模块与文件（若已知，标注证据状态）
   - 待玩家诉求聚类后确定（UNKNOWN）
   - `Docs/设计/玩家反馈处理指导思想.md`（VERIFIED，评论整理与过滤准则）
   - `Docs/玩家反馈/`（VERIFIED，第一批原始反馈记录及 TapTap 可见评价）
   - `AI_Context/TASKS/GD-2026-0001/PLANNER_PROJECT_OVERVIEW.md`（VERIFIED，Planner 项目现状入口）
   - `AI_Context/TASKS/GD-2026-0001/00_CONTEXT_MANIFEST.md`（VERIFIED，上下文文件映射与预算）
   - `AI_Context/TASKS/GD-2026-0001/FEED_DROP/`（VERIFIED，本地可重建上下文缓存）
   - `AI_Context/TASKS/GD-2026-0001/NEED_FILES/ROUND_01/`（VERIFIED，Planner 补充材料，采用与 `FEED_DROP` 一致的平铺命名）
   - `AI_Context/TASKS/GD-2026-0001/NEED_FILES/ROUND_02/`（VERIFIED，NavField、SfxPlayback、核心 EditMode 测试与完整测试索引）
   - `AI_Context/TASKS/GD-2026-0001/NEED_FILES/ROUND_03/`（VERIFIED，配表红线、Input Actions、注释预算与 6 个指定测试）

8. 已观察到的错误、截图或日志
   - `Docs/玩家反馈/`：第一批 10 张截图已转写为 6 份来源记录；TapTap 长截图可见的 26 条评价已新增归档
   - `00_CONTEXT_MANIFEST.md`：Total Token 未超过 150000，层级预算与文件映射以 Manifest 为准
   - Planner 结构校验前 9 项通过；第 10 项要求 `PLAN_vNN.md`，等待 Planner 产出
   - 主 `FEED_DROP` 保留首轮基础包；`NEED_FILES/ROUND_01` 单独存放 45 个补充文件
   - `NEED_FILES/ROUND_02` 平铺 3 个文件：2 个源码、1 个完整测试索引
   - `NEED_FILES/ROUND_03` 平铺 9 个文件：配表规则、Input Actions、注释预算与 6 个指定测试
   - `PLAN_v01.md` 已由 Planner 输出并落盘，状态 `DRAFT`；仅修正第 13 章标题格式以通过交接校验
   - `Docs/待办.md` 单文件约 16894 Token，若全量追加会突破 150000 预算，暂未追加

9. 验收方式（我如何判断完成）
   本轮需求整理至少产出：已分类诉求清单、重复/冲突标记、过滤理由、待 CEO 决策的短清单；最终是否进入开发及范围由 CEO 确认。

10. 其他说明与不确定事项
   `PLAN_v01.md` 已执行并经 CEO 以 `ACCEPT WITH FOLLOW-UP` 签收。独立 Play/V4 与 V5 性能证据保留为后续项。
