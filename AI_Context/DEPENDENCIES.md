# DEPENDENCIES — Unity 包与外部依赖台账

> 版本以 `Packages/manifest.json` 与 `Packages/packages-lock.json` 为准。本表在依赖变化时必须更新并重新审批。

## 引擎与核心包

| 包 / 模块 | 版本 | 用途 | 备注 |
| --- | --- | --- | --- |
| Unity Editor | 2022.3.62f3c1 | 工程编辑器基线 | 升级需专项重审 |
| `com.unity.render-pipelines.universal` | 14.0.12 | URP 2D Renderer | 管线与 Renderer Data 变更需重审 |
| `com.unity.feature.2d` | 2.0.1 | 2D 功能集 | Tilemap / Sprite 工作流 |
| `com.unity.inputsystem` | 1.14.2 | 输入 | `InputSys.cs` 为生成物 |
| `com.unity.textmeshpro` | 3.0.7 | 文本 | UI 文本与字体资源 |
| `com.unity.ugui` | 1.0.0 | UGUI | UI Panel 与 EventSystem |
| `com.unity.cinemachine` | 2.10.7 | 相机 | Presentation 层表现依赖 |
| `com.unity.timeline` | 1.7.7 | 过场与时间线 | `Start.playable` 等资产 |
| `com.unity.test-framework` | 1.1.33 | EditMode / PlayMode 测试 | 当前 EditMode 需人工运行 |
| `com.unity.visualscripting` | 1.9.4 | 工程包依赖 | 不作为主要玩法实现路径 |
| `com.unity.collab-proxy` | 2.12.4 | Unity 版本控制集成 | 不代替本地 Git 纪律 |

Unity 内置模块（physics2d、audio、animation、tilemap、ui 等）由 `manifest.json` 登记，新增或移除均视为工程配置变更。

## 本地依赖与工具

| 依赖 | 位置 | 用途 | 关键约束 |
| --- | --- | --- | --- |
| `Luban.Runtime` | `Assets/Luban.Runtime/` | 本地 Luban 运行库 | 禁止再安装同名 UPM 包 |
| Luban CLI / DLL | `ConfigWorkspace/Tools/Luban/` | 表格生成 | Git LFS 内容，不手改 |
| PowerShell 7 | 系统环境 | 编译、注释、镜像门禁 | 命令使用 `pwsh` |
| Python 3.10+ | 系统环境 | 上下文与交接校验 | 工作流脚本只用标准库 |

## 禁止与审批

1. 默认禁止新增或升级依赖，包括 UPM、Asset Store、原生插件和渲染管线包。
2. 如需新增，PLAN 必须写明量化收益、维护活跃度、移除成本、替代方案、对构建体积和程序集的影响。
3. `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/` 和 URP 配置变更必须由 CEO 重新批准版本。
4. 不得安装 `com.code-philosophy.luban` 或其它提供同名 `Luban.Runtime` 程序集的包。

最后核验日期：2026-11-24｜分支：main｜维护人：CEO
