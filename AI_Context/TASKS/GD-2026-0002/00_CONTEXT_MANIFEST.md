# 00 CONTEXT MANIFEST — GD-2026-0002

- Task ID: GD-2026-0002
- Base Commit: df60015f9d333a2e122f9cfca97fc3b289e0c1f8
- Generated At: 2026-11-24T23:52:41+08:00
- Generation Mode: append
- Chat Safe: ON
- Budget Limit: 150000

## Token Budget

| 层级 | 估算 Token | 预算/状态 |
| --- | ---: | --- |
| L1 | 2518 | 不在 4,096–8,000 隐式缓存甜点区 |
| L2 | 3262 | 模块层建议 ≤ 30,000 |
| L3 | 76212 | 代码层建议 ≤ 100,000 |
| Total | 81992 | 未超过 150000 |

> Token 为启发式估算：每个 CJK 字符计 1，其余非空白字符合计 ÷ 4 后向上取整。

## File Mapping

| 暂存名 | 层级 | 原路径 | 估算 Token |
| --- | --- | --- | ---: |
| `L1__PROJECT_OVERVIEW.md` | L1 | `AI_Context/PROJECT_OVERVIEW.md` | 572 |
| `L1__CODING_STANDARDS.md` | L1 | `AI_Context/CODING_STANDARDS.md` | 912 |
| `L1__BUILD_AND_TEST.md` | L1 | `AI_Context/BUILD_AND_TEST.md` | 1034 |
| `L2__PROJECT_BOARD.md` | L2 | `AI_Context/PROJECT_BOARD.md` | 781 |
| `L2__ARCHITECTURE.md` | L2 | `AI_Context/ARCHITECTURE.md` | 896 |
| `L2__MODULE_MAP.md` | L2 | `AI_Context/MODULE_MAP.md` | 979 |
| `L2__DEPENDENCIES.md` | L2 | `AI_Context/DEPENDENCIES.md` | 606 |
| `L3__AGENTS.md` | L3 | `AGENTS.md` | 2894 |
| `L3__AI_Context_DECISION_LOG.md` | L3 | `AI_Context/DECISION_LOG.md` | 712 |
| `L3__AI_Context_KNOWN_ISSUES.md` | L3 | `AI_Context/KNOWN_ISSUES.md` | 556 |
| `L3__AI_Context_README_AI.md` | L3 | `AI_Context/README_AI.md` | 941 |
| `L3__AI_Context_TASKS_GD-2026-0001_PLANNER_PROJECT_OVERVIEW.md` | L3 | `AI_Context/TASKS/GD-2026-0001/PLANNER_PROJECT_OVERVIEW.md` | 2830 |
| `L3__AI_Context_TASKS_GD-2026-0001_PLAN_v01.md` | L3 | `AI_Context/TASKS/GD-2026-0001/PLAN_v01.md` | 8208 |
| `L3__Assets_Scripts_Data_Config_Spec_EnemySpec.cs` | L3 | `Assets/Scripts/Data/Config/Spec/EnemySpec.cs` | 410 |
| `L3__Assets_Scripts_Data_Config_Spec_PlayerSpec.cs` | L3 | `Assets/Scripts/Data/Config/Spec/PlayerSpec.cs` | 505 |
| `L3__Assets_Scripts_Data_Config_Spec_WaveSpec.cs` | L3 | `Assets/Scripts/Data/Config/Spec/WaveSpec.cs` | 140 |
| `L3__Assets_Scripts_Data_Settings_EnemyTuning.cs` | L3 | `Assets/Scripts/Data/Settings/EnemyTuning.cs` | 1122 |
| `L3__Assets_Scripts_Data_Settings_PlayerConfig.cs` | L3 | `Assets/Scripts/Data/Settings/PlayerConfig.cs` | 173 |
| `L3__Assets_Scripts_Logic_Actor_EnemyBrain.cs` | L3 | `Assets/Scripts/Logic/Actor/EnemyBrain.cs` | 404 |
| `L3__Assets_Scripts_Logic_Actor_EnemyLogic.cs` | L3 | `Assets/Scripts/Logic/Actor/EnemyLogic.cs` | 1078 |
| `L3__Assets_Scripts_Logic_Actor_EnemyNavigator.cs` | L3 | `Assets/Scripts/Logic/Actor/EnemyNavigator.cs` | 1830 |
| `L3__Assets_Scripts_Logic_Actor_States_EnemyChaseState.cs` | L3 | `Assets/Scripts/Logic/Actor/States/EnemyChaseState.cs` | 174 |
| `L3__Assets_Scripts_Logic_Combat_ContactProbe.cs` | L3 | `Assets/Scripts/Logic/Combat/ContactProbe.cs` | 605 |
| `L3__Assets_Scripts_Logic_Combat_Damage.cs` | L3 | `Assets/Scripts/Logic/Combat/Damage.cs` | 509 |
| `L3__Assets_Scripts_Logic_Level_LevelLogic.cs` | L3 | `Assets/Scripts/Logic/Level/LevelLogic.cs` | 906 |
| `L3__Assets_Scripts_Logic_Movement_ActorLedger.cs` | L3 | `Assets/Scripts/Logic/Movement/ActorLedger.cs` | 1160 |
| `L3__Assets_Scripts_Logic_Movement_IActorMotor.cs` | L3 | `Assets/Scripts/Logic/Movement/IActorMotor.cs` | 569 |
| `L3__Assets_Scripts_Logic_Movement_IMovementMotor.cs` | L3 | `Assets/Scripts/Logic/Movement/IMovementMotor.cs` | 153 |
| `L3__Assets_Scripts_Logic_Player_CombatGroup.cs` | L3 | `Assets/Scripts/Logic/Player/CombatGroup.cs` | 678 |
| `L3__Assets_Scripts_Logic_Player_MoveGroup.cs` | L3 | `Assets/Scripts/Logic/Player/MoveGroup.cs` | 799 |
| `L3__Assets_Scripts_Logic_Player_PlayerLogic.cs` | L3 | `Assets/Scripts/Logic/Player/PlayerLogic.cs` | 863 |
| `L3__Assets_Scripts_Logic_Player_PlayerStats.cs` | L3 | `Assets/Scripts/Logic/Player/PlayerStats.cs` | 1154 |
| `L3__Assets_Scripts_Logic_Wave_WaveLogic.cs` | L3 | `Assets/Scripts/Logic/Wave/WaveLogic.cs` | 1818 |
| `L3__Assets_Scripts_Logic_World_SeedPlanter.cs` | L3 | `Assets/Scripts/Logic/World/SeedPlanter.cs` | 423 |
| `L3__Assets_Scripts_Presentation_Actor_CombatDirector.cs` | L3 | `Assets/Scripts/Presentation/Actor/CombatDirector.cs` | 2333 |
| `L3__Assets_Scripts_Presentation_Actor_EnemyActor.cs` | L3 | `Assets/Scripts/Presentation/Actor/EnemyActor.cs` | 3997 |
| `L3__Assets_Scripts_Presentation_Adapters_ActorMotor.cs` | L3 | `Assets/Scripts/Presentation/Adapters/ActorMotor.cs` | 1087 |
| `L3__Assets_Scripts_Presentation_Adapters_EnemyMotor.cs` | L3 | `Assets/Scripts/Presentation/Adapters/EnemyMotor.cs` | 116 |
| `L3__Assets_Scripts_Presentation_Adapters_PlayerMotor.cs` | L3 | `Assets/Scripts/Presentation/Adapters/PlayerMotor.cs` | 55 |
| `L3__Assets_Scripts_Presentation_Input_InputProvider.cs` | L3 | `Assets/Scripts/Presentation/Input/InputProvider.cs` | 1122 |
| `L3__Assets_Scripts_Presentation_Root_CombatRoot.cs` | L3 | `Assets/Scripts/Presentation/Root/CombatRoot.cs` | 7973 |
| `L3__Assets_Scripts_Presentation_Root_PlayerController.cs` | L3 | `Assets/Scripts/Presentation/Root/PlayerController.cs` | 2671 |
| `L3__Assets_StreamingAssets_Luban_dso_tbenemy.json` | L3 | `Assets/StreamingAssets/Luban/dso_tbenemy.json` | 70 |
| `L3__Assets_StreamingAssets_Luban_dso_tblevelwave.json` | L3 | `Assets/StreamingAssets/Luban/dso_tblevelwave.json` | 121 |
| `L3__Assets_StreamingAssets_Luban_dso_tbplayer.json` | L3 | `Assets/StreamingAssets/Luban/dso_tbplayer.json` | 72 |
| `L3__Assets_StreamingAssets_Luban_dso_tbseed.json` | L3 | `Assets/StreamingAssets/Luban/dso_tbseed.json` | 78 |
| `L3__Assets_StreamingAssets_Luban_dso_tbwave.json` | L3 | `Assets/StreamingAssets/Luban/dso_tbwave.json` | 900 |
| `L3__Assets_StreamingAssets_Luban_dso_tbwaveenemy.json` | L3 | `Assets/StreamingAssets/Luban/dso_tbwaveenemy.json` | 626 |
| `L3__Assets_Tests_Runtime_Editor_WaveLogicTests.cs` | L3 | `Assets/Tests/Runtime/Editor/WaveLogicTests.cs` | 488 |
| `L3__Assets_Tests_Runtime_Editor_存档Tests.cs` | L3 | `Assets/Tests/Runtime/Editor/存档Tests.cs` | 1766 |
| `L3__Assets_Tests_Runtime_Editor_敌人预制体Tests.cs` | L3 | `Assets/Tests/Runtime/Editor/敌人预制体Tests.cs` | 1720 |
| `L3__Assets_Tests_Runtime_Editor_移动Tests.cs` | L3 | `Assets/Tests/Runtime/Editor/移动Tests.cs` | 5711 |
| `L3__Docs_玩家反馈_G.肖昕怡.md` | L3 | `Docs/玩家反馈/G.肖昕怡.md` | 75 |
| `L3__Docs_玩家反馈_README.md` | L3 | `Docs/玩家反馈/README.md` | 341 |
| `L3__Docs_玩家反馈_TapTap评价.md` | L3 | `Docs/玩家反馈/TapTap评价.md` | 3044 |
| `L3__Docs_玩家反馈_浅浅丘.md` | L3 | `Docs/玩家反馈/浅浅丘.md` | 120 |
| `L3__Docs_玩家反馈_狂猫毛.md` | L3 | `Docs/玩家反馈/狂猫毛.md` | 250 |
| `L3__Docs_玩家反馈_聚光灯挑战群聊.md` | L3 | `Docs/玩家反馈/聚光灯挑战群聊.md` | 230 |
| `L3__Docs_玩家反馈_豆丁悠悠.md` | L3 | `Docs/玩家反馈/豆丁悠悠.md` | 159 |
| `L3__Docs_玩家反馈_转述反馈_2026-10-21.md` | L3 | `Docs/玩家反馈/转述反馈_2026-10-21.md` | 156 |
| `L3__Docs_设计_玩家反馈处理指导思想.md` | L3 | `Docs/设计/玩家反馈处理指导思想.md` | 1158 |
| `L3__Docs_设计_策划案_荒原复苏指南_v4.0.md` | L3 | `Docs/设计/策划案_荒原复苏指南_v4.0.md` | 7033 |
| `L3__git_recent_context.txt` | L3 | `git:recent` | 1126 |

## Project Tree Summary

```text
.DS_Store
.gitattributes
.gitignore
.vsconfig
AGENTS.md
AI_Context/
AI_Context/ARCHITECTURE.md
AI_Context/BUILD_AND_TEST.md
AI_Context/CODING_STANDARDS.md
AI_Context/DECISION_LOG.md
AI_Context/DEPENDENCIES.md
AI_Context/EVIDENCE/
AI_Context/EVIDENCE/GD-YYYY-NNNN/
AI_Context/KNOWN_ISSUES.md
AI_Context/METRICS.md
AI_Context/MODULE_MAP.md
AI_Context/PROJECT_BOARD.md
AI_Context/PROJECT_OVERVIEW.md
AI_Context/README_AI.md
AI_Context/ROLE_SESSIONS.md
AI_Context/TASKS/
AI_Context/TASKS/.gitkeep
AI_Context/TASKS/GD-2026-0001/
AI_Context/TASKS/GD-2026-0002/
AI_Context/TASKS/GD-2026-0003/
AI_Context/TASKS/_templates/
Assembly-CSharp-Editor.csproj
Assembly-CSharp.csproj
Assets/
Assets/.DS_Store
Assets/Editor.meta
Assets/Editor/
Assets/Editor/DeepseaOil.EditorTools.asmdef
Assets/Editor/DeepseaOil.EditorTools.asmdef.meta
Assets/Editor/LevelEnvironmentDeployer.cs
Assets/Editor/LevelEnvironmentDeployer.cs.meta
Assets/Editor/Lighting.meta
Assets/Editor/Lighting/
Assets/Editor/LubanAutoTrigger.cs
Assets/Editor/LubanAutoTrigger.cs.meta
Assets/Editor/LubanImport.cs
Assets/Editor/LubanImport.cs.meta
Assets/Editor/LubanMenu.cs
Assets/Editor/LubanMenu.cs.meta
Assets/Editor/LubanProject.cs
Assets/Editor/LubanProject.cs.meta
Assets/Editor/PolygonRaycastAreaEditor.cs
Assets/Editor/PolygonRaycastAreaEditor.cs.meta
Assets/Editor/Quote.cs
Assets/Editor/Quote.cs.meta
Assets/Fonts.meta
Assets/Fonts/
Assets/Fonts/SourceHanSansSC-Normal.otf
Assets/Fonts/SourceHanSansSC-Normal.otf.meta
Assets/Fonts/蒙黑体.ttf
Assets/Fonts/蒙黑体.ttf.meta
Assets/Luban.Runtime.meta
Assets/Luban.Runtime/
Assets/Luban.Runtime/BeanBase.cs
Assets/Luban.Runtime/BeanBase.cs.meta
Assets/Luban.Runtime/ByteBuf.cs
Assets/Luban.Runtime/ByteBuf.cs.meta
Assets/Luban.Runtime/Luban.Runtime.asmdef
Assets/Luban.Runtime/Luban.Runtime.asmdef.meta
Assets/Luban.Runtime/SimpleJSON.meta
Assets/Luban.Runtime/SimpleJSON/
Assets/Luban.Runtime/StringUtil.cs
Assets/Luban.Runtime/StringUtil.cs.meta
Assets/Resources.meta
Assets/Resources/
Assets/Resources/.DS_Store
Assets/Resources/Fonts.meta
Assets/Resources/Fonts/
Assets/Resources/Icons.meta
Assets/Resources/Icons/
Assets/Resources/animation.meta
Assets/Resources/animation/
Assets/Resources/art.meta
Assets/Resources/art/
Assets/Resources/audio.meta
Assets/Resources/audio/
Assets/Resources/config.meta
Assets/Resources/config/
Assets/Resources/demo.meta
Assets/Resources/demo/
Assets/Resources/drops.meta
Assets/Resources/drops/
Assets/Resources/effects.meta
Assets/Resources/effects/
Assets/Resources/enemies.meta
Assets/Resources/enemies/
Assets/Resources/lighting.meta
Assets/Resources/lighting/
Assets/Resources/materials.meta
Assets/Resources/materials/
Assets/Resources/palettes.meta
Assets/Resources/palettes/
Assets/Resources/projectiles.meta
Assets/Resources/projectiles/
Assets/Resources/scane_prebs.meta
Assets/Resources/scane_prebs/
Assets/Resources/tiles.meta
Assets/Resources/tiles/
Assets/Resources/timeline.meta
Assets/Resources/timeline/
Assets/Resources/tuning.meta
Assets/Resources/tuning/
Assets/Resources/ui.meta
Assets/Resources/ui/
Assets/Scenes.meta
Assets/Scenes/
Assets/Scenes/Begin.unity
Assets/Scenes/Begin.unity.meta
Assets/Scenes/Empty.unity
Assets/Scenes/Empty.unity.meta
Assets/Scenes/Levels.meta
Assets/Scenes/Levels/
Assets/Scenes/Maza.unity
Assets/Scenes/Maza.unity.meta
Assets/Scenes/Range.unity
Assets/Scenes/Range.unity.meta
Assets/Scenes/Sandboxes.meta
Assets/Scenes/Sandboxes/
Assets/Scripts.meta
Assets/Scripts/
Assets/Scripts/Data.meta
Assets/Scripts/Data/
Assets/Scripts/Foundation.meta
Assets/Scripts/Foundation/
Assets/Scripts/Generated.meta
Assets/Scripts/Generated/
Assets/Scripts/Logic.meta
Assets/Scripts/Logic/
Assets/Scripts/Presentation.meta
Assets/Scripts/Presentation/
Assets/Scripts/UI.meta
Assets/Scripts/UI/
Assets/Settings.meta
Assets/Settings/
Assets/Settings/DefaultGlobalVolumeProfile.asset
Assets/Settings/DefaultGlobalVolumeProfile.asset.meta
Assets/Settings/Lit2DSceneTemplate.scenetemplate
Assets/Settings/Lit2DSceneTemplate.scenetemplate.meta
Assets/Settings/Renderer2D.asset
Assets/Settings/Renderer2D.asset.meta
Assets/Settings/Scenes.meta
Assets/Settings/Scenes/
Assets/Settings/UniversalRP.asset
Assets/Settings/UniversalRP.asset.meta
Assets/Shader.meta
Assets/Shader/
Assets/Shader/CircleStencil.mat
Assets/Shader/CircleStencil.mat.meta
Assets/Shader/CircleStencil.shader
Assets/Shader/CircleStencil.shader.meta
Assets/Shader/CircleStencilInside.mat
Assets/Shader/CircleStencilInside.mat.meta
Assets/Shader/CircleStencilInside.shader
Assets/Shader/CircleStencilInside.shader.meta
Assets/Shader/CircleStencilOutside.mat
Assets/Shader/CircleStencilOutside.mat.meta
Assets/Shader/CircleStencilOutside.shader
Assets/Shader/CircleStencilOutside.shader.meta
Assets/Shader/PlayerHealth.mat
Assets/Shader/PlayerHealth.mat.meta
Assets/Shader/PlayerHealth.shader
Assets/Shader/PlayerHealth.shader.meta
Assets/Shaders.meta
Assets/Shaders/
Assets/Shaders/Procedural_CartoonTile.shader
Assets/Shaders/Procedural_CartoonTile.shader.meta
Assets/StreamingAssets.meta
Assets/StreamingAssets/
Assets/Tests.meta
Assets/Tests/
Assets/TextMesh Pro.meta
Assets/TextMesh Pro/
Assets/UniversalRenderPipelineGlobalSettings.asset
Assets/UniversalRenderPipelineGlobalSettings.asset.meta
ConfigWorkspace/
DeepseaOil.Data.csproj
DeepseaOil.EditorTools.Tests.csproj
DeepseaOil.EditorTools.csproj
DeepseaOil.Foundation.csproj
DeepseaOil.Generated.Input.csproj
DeepseaOil.Logic.csproj
DeepseaOil.Presentation.csproj
DeepseaOil.Tests.EditMode.csproj
DeepseaOil.Tests.Scene.csproj
Docs/
Luban.Runtime.csproj
Packages/
ProjectSettings/
README.md
Tools/
UserSettings/
cfg.csproj
deepseaoil.csproj
deepseaoil.sln
scripts/
```

> 目录摘要最多深度 3、最多 200 项；达到上限时尾部内容被截断。

## Explicit Exclusions

- `.git/`、`.venv/`、各类缓存、`Library/`、`Temp/`、`Logs/`、`logs/`、`tmp/`
- 所有 `FEED_DROP/` 物理投喂箱及本 Manifest 自身
- 未通过 `--files` / `--from-list` 选择的业务文件
- `.env`、`.env.*`（`.env.example` 除外）、`*.key`、`*.pem` 与名称含 secret/credential 的文件
