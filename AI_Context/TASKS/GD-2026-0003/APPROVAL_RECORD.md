# APPROVAL RECORD — GD-2026-0003

Task ID / Approved Plan: GD-2026-0003 / v01  
Decision: APPROVED  
Approved Scope:
- CREATE `Assets/Scripts/Presentation/UI/Component/PopupMotion.cs`
- CREATE `Assets/Scripts/Presentation/UI/Component/SupplyNavIndicator.cs`
- CREATE `Assets/Scripts/Presentation/UI/Component/HudHintBanner.cs`
- CREATE `Assets/Tests/Runtime/Editor/引导与UI反馈Tests.cs`
- MODIFY `Assets/Scripts/Presentation/UI/Component/Typewriter.cs`
- MODIFY `Assets/Scripts/Presentation/UI/Component/UIFader.cs`
- MODIFY `Assets/Scripts/Presentation/UI/Panel/DialoguePanel.cs`
- MODIFY `Assets/Scripts/Presentation/Story/StoryFlow.cs`
- MODIFY `Assets/Scripts/Data/Settings/AudioConfig.cs`
- MODIFY `Assets/Scripts/Presentation/Audio/AudioManager.cs`
- MODIFY `Assets/Scripts/Presentation/Audio/SfxPlayback.cs`
- MODIFY `Assets/Scripts/Presentation/UI/Panel/GamePlayPanel.cs`
- MODIFY `Assets/Resources/config/AudioConfig.asset`
- MODIFY `Assets/Resources/ui/Panel/DialoguePanel.prefab`
- MODIFY `Assets/Resources/ui/Panel/GamePlayPanel.prefab`
- MODIFY `ConfigWorkspace/Data/slot_dialogue.xlsx`
- MODIFY `Tools/comment-budget.json`
- TOOL-GENERATE `Assets/Scripts/Generated/Config/**`、`Assets/StreamingAssets/Luban/**`，只允许在批 1 合入后经 Luban 镜像生成

Conditions / Explicitly Forbidden:
- `.xlsx` 只允许单元格级修改，禁止 `openpyxl` 重建工作表或覆盖结构。
- 严禁删除、重排或改变 `slot_dialogue` 中 `paragraph == 1002` 的 `line.Id = 10/11/12/13` 镜头语义。
- 修改 `AudioConfig.asset`、UI Prefab 时必须保留 `.meta` 与 GUID。
- 注释仅允许在本批改动文件内清理或登记；不得扩大其他文件预算。
- Luban 导表与 `comment-budget.json` 最终合入必须等待批 1 合入主线后基于最新基线执行。
- 严禁修改批 1 玩法/网格/配表文件与批 2 的 `PlayerStats.cs`、`PlayerController.cs`、`CombatRoot.cs`。
- 严禁修改 `TimerWheel.cs`、`SaveSlot.cs`、`InputSys.cs`、`Packages/manifest.json`、`ProjectSettings/**`。
- 多 Worktree 流程若与旧版单工作树校验器冲突，以本批准记录为准；不得因此放宽编译、测试、导表或范围门禁。

Branch: `ai/GD-2026-0003`  
Base Commit: `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`  
Worktree Start: approved `VReason/Update` commit.  
Approver: CEO  
Approval Time: 2026-11-24 23:48 (UTC+8)

## 审核七问确认

1. 目标已确认，无擅自增加需求。
2. 方案符合现有架构与文件边界。
3. 改动范围已按批 1/2/3 隔离。
4. 执行顺序与依赖已确认，未知项保留为 Step 1 查证。
5. 验收标准可观察、可执行。
6. 风险与回滚充分，主工作区已有未提交文档不得被覆盖。
7. 暂停点与升版条件明确。
