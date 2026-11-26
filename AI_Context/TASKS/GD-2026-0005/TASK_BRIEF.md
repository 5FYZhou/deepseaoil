# TASK BRIEF — 同元素反应、种子计数与剧情强调色快速修复

Task ID: GD-2026-0005
Project / Engine / Tool Version: 荒原复苏指南 / Unity 2022.3.62f3c1

Task Level: L1    Risk Level: R1

Status: APPROVED

1. 我想实现什么（Objective）
   修复同元素砸同地貌仍向外铺格、种子次数被“Q 丢弃”遮挡，以及剧情/弹窗强调色全部为红色三项可见问题；保留同元素落地伤害，并让操作、元素、生命与危险文案按美术色板分色。

2. 为什么需要它（Background）
   CEO 实机发现三项直接影响玩法理解与界面可读性的问题。前两项属于明确的行为/布局缺陷，第三项需要把已有富文本强调从单一红色改为稳定的语义配色。

3. 当前行为与问题（Current Behavior / Problem）
   - `element_rule` 中 `基础水 + 水球` 与 `基础土 + 土块` 的 `impact_radius` 当前被误改为 0，应恢复为 1。
   - `GridLogic.OnBallHit` 在 `NextTile == current` 时短路，导致同元素规则即使半径为 1 也不会铺设相邻同元素格。
   - `GamePlayPanel` 的种子次数 `TxtSeed` 与代码生成的 `Q 丢弃` 角标同在种子槽右下区域，次数会被遮挡。
   - `slot_dialogue` 的强调富文本统一使用 `#FF0000`，操作键、水、土、生命泉、元素种子和危险对象没有语义区分。

4. 期望行为（Expected Behavior）
   - 水球砸基础水、土块砸基础土时，规则半径恢复为 1；中心与十字四邻各受 1 点伤害，并生成对应水格/土格，对角格不变。
   - 种子 0–3 次数在槽位右上角稳定可见，不遮挡且不被裁切；左下 `E`、右下 `Q 丢弃` 保持可读。
   - 剧情和弹窗按语义使用美术色板：操作/数值明黄，水蓝，土黄，火红，电紫，冰青，植物与生命泉绿，危险复古红。
   - 波前支援等运行时生成的“火种子/电种子/冰种子/植物种子”提示也使用各自颜色，不统一为青色。

5. 非目标（Non-goals）
   不改反应算法或其他元素规则；不改开局 CG、README、04 任务产物；不改输入映射、波次、生命泉、关卡或 UI 面板结构。

6. 已知约束（技术 / 性能 / 平台版本 / 必须保持不变的行为）
   - 四层依赖必须保持 `Presentation → Logic → Data → Foundation`。
   - 热路径 0 GC；不得在 Tick 中执行富文本扫描或颜色字符串分配。
   - 基线为本地 `main@03d8913`；工作区开工前必须干净。
   - CEO 临时授权 Agent 修改 `element_rule.xlsx` 与 `slot_dialogue.xlsx`；必须读入现有工作簿后只改指定单元格并保存，禁止重建工作表、改 schema、改写子表头或运行覆盖源表的生成脚本。
   - Luban 生成物只能由正常导表更新，禁止手改 `Assets/StreamingAssets/Luban/*.json`。
   - 不得回滚、覆盖或暂存与本任务无关的文件。

7. 相关模块与文件（若已知，标注证据状态）
   - `ConfigWorkspace/Data/element_rule.xlsx`（VERIFIED，`Sheet1!I21` / `I22` 当前为 1）
   - `ConfigWorkspace/Data/slot_dialogue.xlsx`（VERIFIED，现有强调色为 `#FF0000`）
   - `Assets/Resources/ui/Panel/GamePlayPanel.prefab`（VERIFIED，`TxtSeed` 锚定右下）
   - `Assets/Scripts/Presentation/UI/Panel/GamePlayPanel.cs`（VERIFIED，`Q 丢弃` 为运行时代码生成）
   - `Assets/Scripts/Data/Settings/VisualPalette.cs`（VERIFIED，已有水火电冰元素色）
   - `Assets/Scripts/Presentation/UI/Component/HudHintBanner.cs`（VERIFIED，种子名当前统一青色）
   - `Assets/Scripts/Logic/Grid/GridLogic.cs`（VERIFIED，当前 `NextTile == current` 会跳过一级地貌铺设）
   - `Assets/Tests/Runtime/Editor/地块范围Tests.cs`（VERIFIED，含元素半径真表与行为断言）
   - `Assets/Tests/Runtime/Editor/地块效果Tests.cs`（VERIFIED，当前规则 17/18 半径断言被误改为 0）
   - `Assets/Tests/Runtime/Editor/引导与UI反馈Tests.cs`（VERIFIED，适合放剧情/提示文本断言）
   - `AI_Context/BUILD_AND_TEST.md`（VERIFIED，验证入口）

8. 已观察到的错误、截图或日志
   - CEO 实机反馈：种子槽截图可见圆形种子图标、左下 E 与右下“Q 丢弃”，种子次数被角标覆盖。
   - 源码事实：`GamePlayPanel.CreateSlotKeyLabels()` 将 `Q 丢弃` 设为右下角标；Prefab 的 `TxtSeed` 也锚定右下。
   - 数据事实：`element_rule` 规则 17/18 伤害为 1、半径当前为 0；Luban JSON 与 XLSX 一致。
   - 源码事实：`OnBallHit` 用 `outcome.NextTile != current` 短路 `ApplyTileChange`，同值规则无法执行半径铺设。

9. 验收方式（我如何判断完成）
   - EditMode 回归证明同元素中心与十字四邻各命中 1 次并生成对应元素格；对角格保持 Normal 且命中 0 次。
   - `pwsh Tools/compile-gate.ps1 -Topology` 为 0 error / 0 warning。
   - `pwsh Tools/luban-mirror.ps1` 为待删除 0 / 待覆盖 0 / 孤儿 meta 0。
   - `git diff --check` 退出码 0；实际修改全部在批准白名单。
   - Level_0 实跑确认水+水、土+土铺出十字同元素格；同时复核种子次数、剧情与种子提示语义分色。

10. 其他说明与不确定事项
    “雷种子”按当前正式名称保留“电种子”，仅改变颜色。CEO 已确认十字四邻保留 1 点范围伤害。测试数可能需由 CEO 在 Unity Test Runner 手工执行；Worker 必须如实记录，不得把未运行写成 PASS。
