# PROJECT_OVERVIEW — 荒原复苏指南

## 一句话定位

一款俯视角 2D 生存追逐战：玩家没有直接攻击技能，通过纯水、泥土与火池、电桩、冰面、植被等地貌发生离散化学反应来求生。

## 核心体验

- 投掷水球或土块，与网格地貌发生可预测、可观察的元素反应。
- 用泥浆减速、陶砖封路、导电与燃烧连锁改变战场。
- 在敌群压力下规划路线，并利用唯一生命神泉创造安全恢复窗口。
- 核心规则采用离散查表与首跳当帧，不使用旧的连续温湿度浮点模型。

## 交付目标

- `Logic` 保持纯 C#，核心规则可脱离场景进行 EditMode 测试。
- 四层 asmdef 依赖单向、可编译期拦截。
- 热路径保持 0 GC。
- Excel / Luban、调参 SO、表现资产三者职责清晰，不互相越权。
- 每次代码改动通过真实编译门、注释门和适用测试。

## 非目标

- 不让 Agent 自动改写 `ConfigWorkspace/Data/*.xlsx`。
- 不把运动学、手感曲线、颜色频率等表现调参塞回核心配表或代码常量。
- 不为单次需求引入新的通用框架、依赖、程序集或资源加载体系。
- 不以“编译通过”代替运行时行为和视觉验收。

## 技术基线

| 项 | 值 |
| --- | --- |
| 引擎 | Unity 2022.3.62f3c1 |
| 模板 | 2D |
| 渲染 | URP 14.0.12，2D Renderer |
| 输入 | New Input System 1.14.2 |
| UI | UGUI + TextMeshPro 3.0.7 |
| 程序集 | Foundation / Data / Logic / Presentation + cfg / Generated.Input / EditorTools / Tests |
| 配置管线 | Luban 本地工具与本地 Runtime，不使用 UPM Luban 包 |
| 基准场景 | `Assets/Scenes/Range.unity` 为完整玩法调试场景；新游戏入口为 `Level_0` |

## 机器判据

1. `pwsh Tools/compile-gate.ps1 -Topology`：0 error / 0 warning，拓扑守卫通过。
2. `pwsh Tools/comment-lint.ps1`：退出码 0。
3. EditMode Test Runner：相关测试全绿；未运行必须写 `NOT RUN`。
4. `pwsh Tools/luban-mirror.ps1`：涉及导表时，待删除、待覆盖、孤儿 `.meta` 均为 0。

最后核验日期：2026-11-24｜分支：main｜维护人：CEO
