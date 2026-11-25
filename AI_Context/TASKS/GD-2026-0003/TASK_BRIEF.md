# TASK BRIEF — 引导、UI 与音频体验升级

> Secretary 登记稿。Planner 计划尚未产出，本任务不得施工。

Task ID: GD-2026-0003
Project / Engine / Tool Version: 荒原复苏指南 / Unity 2022.3.62f3c1

Task Level: L2    Risk Level: R2

Status: ACCEPTED_WITH_FOLLOW_UP

1. 我想实现什么（Objective）
   改善首局引导与操作可理解性：补充 HUD 键位提示、水珠边缘导航、支援/残血轻量浮窗；完善弹窗打字机、短音效、滑入/消退与动态时长；降低剧情专有名词理解成本。

2. 为什么需要它（Background）
   玩家反馈集中出现“不知道切换/回血/攻击键位”“对话框不融入”“伤害与命中反馈不足”等问题。Planner 已将该批从 `GD-2026-0001` 拆分，以便 UI/音频代码与玩法批次并行准备。

3. 当前行为与问题（Current Behavior / Problem）
   - HUD 未完整标示鼠标左右键与放置键，玩家需要试错。
   - 探索、支援与残血信息提示不足。
   - Popup 打字机短音效、滑动和持续时间需要核查并统一。
   - `slot_dialogue` 中专业名词密度较高。
   具体现状与根因尚待 Planner/Worker 基于源码和配表查证。

4. 期望行为（Expected Behavior）
   - 关键键位与可用操作在 HUD 上直接可读。
   - 水珠在屏幕边缘提供清晰导航。
   - 支援和残血提示轻量出现，不遮挡战斗主体。
   - 弹窗打字与短音效节奏一致，不支持音效截断或重复叠加。
   - 剧情文本在不损害世界观的前提下降低理解门槛。

5. 非目标（Non-goals）
   不修改批 1 的网格、寻路、生命泉与战斗数值；不修改批 2 的 PlayerStats、PlayerController、CombatRoot；不新增框架、第三方依赖、asmdef 或音频资产加载体系。

6. 已知约束（技术 / 性能 / 平台版本 / 必须保持不变的行为）
   UI 通过 `UIMgr` 调度；场景内不重复放置 Canvas/EventSystem；热路径 0 GC；不得在业务组件散落 Update；`.prefab`、`.asset`、`.xlsx`、音频资产和 `Tools/comment-budget.json` 的修改须绑定 CEO 专项授权；涉及 Luban 时必须等待导表窗口并保留表结构。

7. 相关模块与文件（若已知，标注证据状态）
   - `Assets/Scripts/Presentation/UI/**`（VERIFIED，Planner 拆批文件域）
   - `Assets/Scripts/Presentation/Story/StoryFlow.cs`（VERIFIED）
   - `Assets/Scripts/Presentation/Audio/AudioManager.cs`、`SfxPlayback.cs`（VERIFIED）
   - `Assets/Resources/ui/Panel/GamePlayPanel.prefab`（VERIFIED，路径已确认）
   - `ConfigWorkspace/Data/slot_dialogue.xlsx`、`audio_map.xlsx`（OBSERVED，按需改表）
   - 对应 EditMode 测试（UNKNOWN，待 Planner 指定）

8. 已观察到的错误、截图或日志
   - 玩家反馈原文：`Docs/玩家反馈/`
   - Planner 拆批依据：`AI_Context/TASKS/GD-2026-0001/PLAN_v01.md` 第 2 章及 Planner 授权备忘

9. 验收方式（我如何判断完成）
   由 Planner 在后续 PLAN 中给出可判定验收。至少应覆盖：HUD 键位可读、支持提示不遮挡战斗、打字机与短音效同步且不重复、剧情文本可理解、`compile-gate.ps1 -Topology` 全绿、相关 EditMode 测试及 `Range.unity`/`Level_0` 实机观察。

10. 其他说明与不确定事项
    `PLAN_v01.md` 已执行并经 CEO 以 `ACCEPT WITH FOLLOW-UP` 签收。独立 Play/V4 与 V5 性能证据保留为后续项。
