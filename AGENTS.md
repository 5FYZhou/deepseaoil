# AGENTS.md

Unity 2022.3.62f3c1 + URP 2D，三层架构（Data / Logic / Presentation）＋ `Foundation` 地基，配表走 Luban。文档索引在 [`README.md`](README.md)。

- **编译门**（本机唯一的自动验证手段）：`pwsh Tools/compile-gate.ps1`，目标是 **0 error / 1 warning**（唯一基线警告是 `Assets/Scripts/Logic/Services/SaveService.cs:14` CS0649）。改完代码就跑一次。
- **层与层之间没有 asmdef 边界，命名空间是唯一的边界表达**：`DeepseaOil.Data` / `.Logic` / `.Presentation` / `.Foundation` ＋ `cfg`。越界的层依赖**不报错、不警告、测试也不查**，只能靠人守。
- `Logic/` 是纯逻辑：没有 `MonoBehaviour`、不碰场景对象（只吃 `Vector2` / `Mathf` 这类值类型）；`Data/` 只读、被所有层直连；`Presentation/` 实现逻辑层定义的端口再注入（端口住在"需要它"的那个文件里）。
- **目录与命名约定**见 [`Docs/目录说明.md`](Docs/目录说明.md)（它的目录树被测试机器校验：写进去的路径必须真实存在）。生成物禁区、`Docs/待办.md`（还没做的项）也在那边。
- 禁手改：`Assets/Scripts/Generated/**`、`Assets/StreamingAssets/Luban/**`。`ConfigWorkspace/` 的 schema 是契约，另有 `ConfigWorkspace/AGENTS.md`，改表前先读它。
- 只有 `GameRoot` 是真单例；每帧只有两个驱动入口（`GameRoot.Update` / `FixedUpdate`），场景对象经 `ISceneRoot` 报到后由它按 `Order` 装配与驱动。
