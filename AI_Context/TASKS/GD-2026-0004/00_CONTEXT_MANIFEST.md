# 00 CONTEXT MANIFEST — GD-2026-0004

- Task ID: GD-2026-0004
- Base Commit: 254b9cf1a2de413cb02594c6f2e2730fb06171a3
- Generated At: 2026-11-26T00:42:56+08:00
- Generation Mode: rebuild
- Chat Safe: ON
- Budget Limit: 150000

## Token Budget

| 层级 | 估算 Token | 预算/状态 |
| --- | ---: | --- |
| L1 | 2581 | 不在 4,096–8,000 隐式缓存甜点区 |
| L2 | 3621 | 模块层建议 ≤ 30,000 |
| L3 | 10719 | 代码层建议 ≤ 100,000 |
| Total | 16921 | 未超过 150000 |

> Token 为启发式估算：每个 CJK 字符计 1，其余非空白字符合计 ÷ 4 后向上取整。

## File Mapping

| 暂存名 | 层级 | 原路径 | 估算 Token |
| --- | --- | --- | ---: |
| `L1__PROJECT_OVERVIEW.md` | L1 | `AI_Context/PROJECT_OVERVIEW.md` | 649 |
| `L1__CODING_STANDARDS.md` | L1 | `AI_Context/CODING_STANDARDS.md` | 912 |
| `L1__BUILD_AND_TEST.md` | L1 | `AI_Context/BUILD_AND_TEST.md` | 1020 |
| `L2__PROJECT_BOARD.md` | L2 | `AI_Context/PROJECT_BOARD.md` | 938 |
| `L2__ARCHITECTURE.md` | L2 | `AI_Context/ARCHITECTURE.md` | 896 |
| `L2__MODULE_MAP.md` | L2 | `AI_Context/MODULE_MAP.md` | 1181 |
| `L2__DEPENDENCIES.md` | L2 | `AI_Context/DEPENDENCIES.md` | 606 |
| `L3__Docs_README.md` | L3 | `Docs/README.md` | 1779 |
| `L3__Docs_工程_人类接手与工程交接手册.md` | L3 | `Docs/工程/人类接手与工程交接手册.md` | 5061 |
| `L3__README.md` | L3 | `README.md` | 3283 |
| `L3__git_recent_context.txt` | L3 | `git:recent` | 596 |

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
AI_Context/EVIDENCE/GD-2026-0001/
AI_Context/EVIDENCE/GD-2026-0003/
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
AI_Context/TASKS/GD-2026-0004/
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
Assets/Shaders.meta
Assets/Shaders/
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
