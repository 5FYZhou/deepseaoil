# TASK BRIEF — 玩家生存与波前配给修复

> Secretary 登记稿。Planner 计划尚未产出，本任务不得施工。

Task ID: GD-2026-0002
Project / Engine / Tool Version: 荒原复苏指南 / Unity 2022.3.62f3c1

Task Level: L2    Risk Level: R2

Status: ACCEPTED_WITH_FOLLOW_UP

1. 我想实现什么（Objective）
   修复玩家受击时穿入或穿过怪群、角色死亡后持续滑行的体验缺陷；调整波前种子配给和掉落位置，使新种子落到玩家脚边且不静默覆盖现有种子。

2. 为什么需要它（Background）
   玩家反馈持续指出虫群速度快、命中与受击反馈难以理解。Planner 已将该批从 `GD-2026-0001` 拆分，避免与玩法基座、UI/音频批次产生文件冲突。

3. 当前行为与问题（Current Behavior / Problem）
   - 受击或短暂无敌期间，玩家可能与敌人碰撞体积重叠，形成“穿怪/幽灵化”观感。
   - 死亡后角色可能继续滑行，削弱死亡反馈。
   - 波前种子配给可能在已有种子时丢弃或覆盖，且掉落位置不利于玩家读取与拾取。
   以上行为来自 Planner 拆批说明，具体根因与复现条件尚待 Planner/Worker 查证。

4. 期望行为（Expected Behavior）
   - 受击期间不穿过敌人，不因碰撞体积陷入怪群内部。
   - 死亡发生后立即停止玩家主动滑行，保留明确且可预测的死亡反馈。
   - 波前种子按明确规则发放到玩家脚边；若无法同时持有两种种子，不静默丢失旧种子或新配给。

5. 非目标（Non-goals）
   不修改批 1 的 NavField、SceneObstacleBaker、TileObstacleDirector、LifeFountain、TileGridLightmap；不修改批 3 的 UI、剧情、音频与弹窗表现；不修改未批准的输入动作或程序集。

6. 已知约束（技术 / 性能 / 平台版本 / 必须保持不变的行为）
   保持四层 asmdef 拓扑与 Logic 纯净度；热路径 0 GC；不得直接在场景组件中新增 Update/FixedUpdate；涉及 `.xlsx`、`.asset`、`.prefab`、`Tools/comment-budget.json` 时必须使用 CEO 已授权的专项范围并写入批准记录；不得覆盖既有用户修改。

7. 相关模块与文件（若已知，标注证据状态）
   - `Assets/Scripts/Logic/Player/PlayerStats.cs`（VERIFIED，Planner 拆批指定）
   - `Assets/Scripts/Presentation/Root/PlayerController.cs`（VERIFIED，Planner 拆批指定）
   - `Assets/Scripts/Presentation/Root/CombatRoot.cs`（VERIFIED，Planner 拆批指定）
   - `Assets/Scripts/Logic/Wave/WaveLogic.cs`（OBSERVED，波前种子配给链）
   - 对应 EditMode 测试（UNKNOWN，待 Planner 指定）

8. 已观察到的错误、截图或日志
   - 玩家反馈原文：`Docs/玩家反馈/`
   - Planner 拆批依据：`AI_Context/TASKS/GD-2026-0001/PLAN_v01.md` 第 2 章及 Planner 授权备忘

9. 验收方式（我如何判断完成）
   由 Planner 在后续 PLAN 中给出可判定验收。至少应覆盖：受击不穿怪、死亡不滑行、波前种子不静默覆盖、`compile-gate.ps1 -Topology` 全绿、相关 EditMode 测试与 `Range.unity` 实机观察。

10. 其他说明与不确定事项
    `PLAN_v01.md` 已执行并经 CEO 以 `ACCEPT WITH FOLLOW-UP` 签收。独立 Play/V4 与 V5 性能证据保留为后续项。
