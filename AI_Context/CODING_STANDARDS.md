# CODING_STANDARDS — 本工程 C# 与 Agent 施工规范

> 完整工程规则以根 `AGENTS.md`、`Docs/工程/架构约束.md` 和 `Docs/工程/注释规范.md` 为准。本文件是任务装载用的执行摘要。

## 1. 编译与证据

1. 每次代码改动最终必须运行 `pwsh Tools/compile-gate.ps1 -Topology`，得到 0 error / 0 warning。
2. 注释改动必须运行 `pwsh Tools/comment-lint.ps1`，退出码 0。
3. 测试未运行只能写 `NOT RUN`；工具输出、退出码和日志才是证据。
4. 不得删除、跳过或弱化既有断言来换取绿色。

## 2. 程序集与目录

1. 依赖方向：`Presentation → Logic → Data → Foundation`；`cfg` 只服务 Data 方向。
2. asmdef references 不传递。使用到的基类、接口和公开签名类型，其程序集必须显式加入 references。
3. `Foundation` references 必须保持为空。
4. 新增程序集、引用、公共接口或 Serialize 字段必须先升 L1/L2 并重新批准。
5. 手写文件不得进入 `Assets/Scripts/Generated/Config/`、`Assets/StreamingAssets/Luban/` 或覆盖 `InputSys.cs`。

## 3. 运行期纪律

1. 只有 `GameRoot` 是真单例和唯一常驻组合根。
2. 不在业务组件中散布 `Update()` / `FixedUpdate()`；由 `GameRoot` 统一驱动。
3. `Logic` 不继承 `MonoBehaviour`，不碰场景对象，不直接读 `Time.time` / `Time.deltaTime`。
4. 场景不得重复放置 Canvas、EventSystem、AudioListener。
5. UI 通过 `UIMgr.ShowPanel<T>()` / `HidePanel<T>()` 调度。

## 4. 性能

1. Tick、FixedTick、Update、受击、碰撞等高频繁路径保持 0 GC。
2. 禁 LINQ、匿名 lambda 闭包、频繁 `new List` / `new Array`、热路径 `GetComponent`。
3. 碰撞检测优先 NonAlloc；可复用缓冲应成为成员字段或池化对象。
4. 2D 排序使用 `RenderOrder` 与 Sorting Layer，不靠 Z 轴调整。
5. 资产按需通过 `AssetModule` 懒加载，禁止启动全量预载。

## 5. 注释

1. 注释必须防止“编译器不拦、测试不红，但手感 / 观感 / 性能会错”的改动。
2. 只写九类信息；禁止重复命名、变更史、进度 TODO 和排版标签。
3. XML 标签只允许 `<summary>` 与 `<remarks>`。
4. 注释预算只降不升；修改注释等同修改代码，不得顺手改变 token、字符串、缩进或 BOM。

## 6. 数据与资产

1. `ConfigWorkspace/Data/*.xlsx` 是人工维护的唯一配置真源，Agent 默认只读。
2. 运动学、投掷、颜色、频率等手感值放在调参 SO，不塞回 Excel 或代码常量。
3. Logic / Presentation 读 `*Spec`，不直接绑定生成表行字段。
4. Agent 默认不修改 `.prefab / .unity / .asset / .meta / .xlsx`、`ProjectSettings/`、`Packages/manifest.json` 或渲染管线配置。
5. 资产与 `.meta` 必须成对处理；GUID 不确定即停止。

## 7. Git 与工作区

1. 默认只读 Git；commit、merge、rebase、push、tag 必须有 CEO 明确授权。
2. 不覆盖用户未提交修改；无法隔离时停止并报告。
3. 标准 L1/L2 使用 `ai/GD-YYYY-NNNN` 隔离分支，除非批准记录另写要求。
4. 每次施工前后检查 `git status` 和 Diff，实际路径必须 100% 落在 File Scope。

## 8. 可信度标记

事实只使用：`VERIFIED / OBSERVED / INFERRED / UNKNOWN / STALE`。推断不得写成已验证事实；代码与文档冲突时以代码和真实门禁输出为准。

最后核验日期：2026-11-24｜分支：main｜维护人：CEO
