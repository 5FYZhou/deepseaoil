# AGENTS.md

Unity 2022.3.62f3c1 + URP 2D，三层架构（Data / Logic / Presentation）＋ `Foundation` 地基，配表走 Luban。文档索引在 [`README.md`](README.md)。

- **编译门**（本机唯一的自动验证手段）：`pwsh Tools/compile-gate.ps1`，目标是 **0 error / 1 warning**（唯一基线警告是 `Assets/Scripts/Logic/Services/SaveService.cs:14` CS0649）。改完代码就跑一次。它按 asmdef **分别**生成临时工程并跑四条拓扑守卫（反向依赖 / 缺引用 / 重名 / asmdef 落进导表镜像区）。
- **层与层之间已有 asmdef 边界**：`DeepseaOil.Foundation` / `.Data` / `.Logic` / `.Presentation` ＋ `cfg` / `DeepseaOil.Generated.Input`。越界的层依赖现在**编译不过**（代码里是 CS0234，asmdef 里是拓扑守卫，互引是 MSBuild 循环依赖）。拓扑表、依赖方向铁律、改动 checklist 见 [`Docs/架构约束.md`](Docs/架构约束.md)；层内**各组之间**仍是命名空间约定。
- `Logic/` 是纯逻辑：没有 `MonoBehaviour`、不碰场景对象（只吃 `Vector2` / `Mathf` 这类值类型）；`Data/` 只读、被所有层直连；`Presentation/` 实现逻辑层定义的端口再注入（端口住在"需要它"的那个文件里）。
- **目录与命名约定**见 [`Docs/目录说明.md`](Docs/目录说明.md)（它的目录树被测试机器校验：写进去的路径必须真实存在）。生成物禁区、`Docs/待办.md`（还没做的项）也在那边。
- 禁手改：`Assets/Scripts/Generated/**`、`Assets/StreamingAssets/Luban/**`。`ConfigWorkspace/` 的 schema 是契约，另有 `ConfigWorkspace/AGENTS.md`，改表前先读它。
- 只有 `GameRoot` 是真单例；每帧只有两个驱动入口（`GameRoot.Update` / `FixedUpdate`），场景对象经 `ISceneRoot` 报到后由它按 `Order` 装配与驱动。
