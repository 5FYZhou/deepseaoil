<div align="center">

<img src="Assets/Resources/Icons/icon_game_main.png" alt="荒原复苏指南图标" width="84" />

# 荒原复苏指南

当资源趋近枯竭，当大地不断呐喊，而你只有两个瓶子和一袋种子。

[![Unity 2022.3.62f3c1](https://img.shields.io/badge/Unity-2022.3.62f3c1-2f6feb.svg?style=flat-square)](#工程开发入口)
[![URP 14.0.12](https://img.shields.io/badge/URP%202D-14.0.12-2ea043.svg?style=flat-square)](#工程开发入口)
[![4-Tier asmdef](https://img.shields.io/badge/Architecture-4--Tier%20asmdef-d97706.svg?style=flat-square)](#工程开发入口)
[![v2.0 内部可玩版](https://img.shields.io/badge/Build-v2.0%20%E5%86%85%E9%83%A8%E5%8F%AF%E7%8E%A9%E7%89%88-1f6feb.svg?style=flat-square)](#v20-最近更新摘要)

<br />

<img src="Assets/Resources/art/CG/illust_cover_game.png" alt="荒原复苏指南主视觉" width="100%" />

</div>

---

## 🎯 核心定位与生存循环

俯视角 2D 策略生存追逐战。没有攻击技能，只有两个瓶子、一袋种子，以及整片可以被点燃、导电、铺路的地貌。

```mermaid
flowchart LR
    A["💧 水井 / 土丘补给"] --> B["🎯 投掷 / 播种"]
    B --> C["⚡ 地貌连锁"]
    C --> D["💚 神泉回血"]
    D --> A
```

- 💚 **5 点生命，没有数值成长**：普通怪接触 1 伤，精英怪接触 2 伤。
- 🛡️ **受击 0.8 秒穿怪脱困**：关闭敌人实体碰撞，直接冲出怪堆。
- 💧 **站定约 1.65 秒回 1 血**：圈内微动或短暂离圈只让进度缓降，不再一动清零。

---

## 🎞️ 实机画面

### 🌍 世界与光照

<p align="center">
  <img src="Docs/媒体/README/readme-promo.webp" alt="荒原复苏指南宣传图" width="360" />
  <img src="Docs/媒体/README/readme-day-night.webp" alt="昼夜与黄昏的网格光照效果" width="480" />
  <br />
  <sub>水土双投掷 · 昼 / 暮 / 夜的环境光照</sub>
</p>

<p align="center">
  <img src="Docs/媒体/README/readme-level-overview.webp" alt="荒原复苏指南关卡总览" width="860" />
  <br />
  <sub>六片荒原，每片都有自己的地形预设与压力路线</sub>
</p>

### ⚔️ 战斗与动态反馈

<p align="center">
  <img src="Docs/媒体/README/readme-fire-chain.webp" alt="火海沿着植物网格连锁扩散" width="420" />
  <img src="Docs/媒体/README/readme-steam-demo.gif" alt="蒸汽反应与格网扩散演示" height="220" />
  <br />
  <sub>火海铺开 · 蒸汽反应沿网格扩散</sub>
</p>

<p align="center">
  <img src="Docs/媒体/README/readme-wind-demo.gif" alt="荒原草丛随风摆动演示" height="220" />
  <img src="Docs/媒体/README/readme-hit-demo.gif" alt="受击形变反馈演示" height="220" />
  <br />
  <sub>风穿过草丛 · 受击顿帧与形变</sub>
</p>

白模、反应网、网格光照与粒子迭代全过程见小黑盒复盘：[《你胆子这是肥嘟嘟的，居然敢用 AI 开发游戏》](https://www.xiaoheihe.cn/app/bbs/link/7acd72af7ca7?h_camp=link&new_post_share_style_v2=1&h_src=YXBwX3NoYXJl)。

---

## 🧪 元素反应

落点格先生效；之后按地貌优先级扩散，泥浆与永久地貌不会被基础水土轻易覆盖，二级反应会跳过一级覆写。

### 三档规范作用范围

| 范围 | 形状 | 典型反应与效果 |
| --- | --- | --- |
| **5 格** | ✚ 十字 · `r = 1.0` | 💧 水 / 土铺地 · 🟤 泥浆 70% 移速 · 🧱 陶砖 15s · ⚡ 导电 1 伤 + 1.5s 麻痹 |
| **9 格** | ▦ 九宫 · `r = 1.5` | 🔥 火海 0.2s/格、2 伤/秒 · 🌵 带电荆棘 0.1s/格、50% 移速 + 1.5s 麻痹 · ❄️ 冰刺 38% 移速、2 伤/秒 |
| **13 格** | ✛ 二环十字 · `r = 2.0` | 💨 蒸汽 2 伤 + 1.5 格击退 · 💥 爆轰 4~5 伤 · 🕳️ 超导黑洞 5 单位/秒向心聚怪 |

### 便携元素种子

波前配给或击败精英怪获得，单槽可播撒 **3 次**。

| 种子 | 生成地貌 | 核心用途 |
| --- | --- | --- |
| <img src="Assets/Resources/Icons/Seed_Fire.png" height="26" alt="火种子" /> **火种子** | 🔥 基础火池 | 蒸汽、陶砖、爆轰 |
| <img src="Assets/Resources/Icons/Seed_Elec.png" height="26" alt="电种子" /> **电种子** | ⚡ 基础电源 | 全网麻痹、带电荆棘 |
| <img src="Assets/Resources/Icons/Seed_Ice.png" height="26" alt="冰种子" /> **冰种子** | ❄️ 基础冰面 | 霜冻减速、聚怪控场 |
| <img src="Assets/Resources/Icons/Seed_Plant.png" height="26" alt="植物种子" /> **植物种子** | 🌿 基础花 | 扩成植物区，承接火海与荆棘 |

---

## ⌨️ 操作键位

| 键位 | 动作 | 说明 |
| --- | --- | --- |
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> / 方向键 | 🚶 移动 | 8 向平滑吸附（支持手柄） |
| <kbd>Shift</kbd> / <kbd>Space</kbd> | ⚡ 冲刺 | 1.5s 冷却，含 0.12s 输入缓冲 |
| <kbd>LMB</kbd>（鼠标左键） | 💧 投掷水球 | 铺水、导电或激发水系反应 |
| <kbd>RMB</kbd>（鼠标右键） | 🪨 投掷土块 | 筑陶砖、混泥浆 |
| <kbd>E</kbd> | 🌱 播撒种子 | 在瞄准格插下当前种子 |
| <kbd>Q</kbd> | 🗑️ 丢弃种子 | 丢到脚边，1.2s 防误拾 |
| <kbd>Esc</kbd> | ⏸️ 暂停 | 呼出暂停菜单 |

> 补充：`F` 键用于 UI 面板切换；编辑器与开发包下，关卡内按 `K` 可快速清场验证波次流，按 `L` 可轮转昼/暮/夜光照。

---

<a id="v20-最近更新摘要"></a>

## 📌 v2.0 最近更新摘要

当前版本为 **v2.0 内部可玩版**（基线 `main@254b9cf`，已完成 `GD-2026-0001 ~ GD-2026-0003` 三批更新及反应扩散优先级修补，完整通关与性能采样尚在收口）。

- ⚔️ **玩法**：规范 5 / 9 / 13 格扩散；生命泉约 1.65s 加速回血与缓降；受击穿怪、死亡急停；冰刺去硬定身；满槽种子落脚边。
- 🧭 **引导**：HUD 常驻键位提示，水 / 土 / 生命泉增加屏外导航；残血与种子补给浮窗；50 句教学文案重写；弹窗 15 字/秒并停留 3s。
- ✨ **视听**：14Hz 受击闪烁、延长爆轰与电弧光照、稳住暗处光标，并补齐打字音与弹窗进出音效。

详细对照表与待验清单见：[`Docs/更新说明/v2.0更新说明.docx`](Docs/更新说明/v2.0更新说明.docx)。

---

## 工程开发入口

面向接手开发者、协作成员与 AI Agent 的工程约束与验证入口已折叠如下，点击按需展开。

<details>
<summary><strong>1. 运行环境、启动场景与三道工程门禁</strong></summary>

### 环境基线

- **Unity Editor**：`2022.3.62f3c1`（LTS，2D 模板）
- **渲染管线**：URP `14.0.12`（2D Renderer，Linear 色彩空间）
- **工具链**：PowerShell 7+ (`pwsh`)、Python `3.10+`、Input System `1.14.2`、TMP `3.0.7`、Cinemachine `2.10.7`

### 启动场景

- **完整游戏入口**：`Assets/Scenes/Begin.unity`（Build Settings 首场景）→ 教学关 `Assets/Scenes/Levels/Level_0.unity` → `Level_1` ~ `Level_5`。
- **战斗主调试场景**：`Assets/Scenes/Range.unity`（直接 Play 拉起 HUD 与完整玩法链；取消 `CombatRoot.enableLevel` 可单测反应）。
- **切片与导航沙盒**：`Assets/Scenes/Sandboxes/`（`Slice_GridReaction` / `Slice_CombatSandbox` / `Slice_PlayerFeel`）与 `Assets/Scenes/Maza.unity`。

### 工程门禁与测试

在仓库根目录执行（详见 [`AI_Context/BUILD_AND_TEST.md`](AI_Context/BUILD_AND_TEST.md)）：

```powershell
pwsh Tools/compile-gate.ps1 -Topology   # 0 Error / 0 Warning，含四层拓扑反向依赖守卫
pwsh Tools/comment-lint.ps1             # 注释预算与 XML 白名单检查（注：当前基线存在少量超预算历史文件）
pwsh Tools/luban-mirror.ps1             # 导表镜像干跑检查：待删 / 待覆盖 / 孤儿 .meta 应均为 0
```

- **EditMode 测试**：Unity 内运行 `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`（当前无统一 CLI 入口）。
- **交接结构校验**：`python scripts/validate_handover.py AI_Context/TASKS/<TASK_ID> --for <planner|worker|watchdog|archive>`。

</details>

<details>
<summary><strong>2. 四层架构、14 张 Luban 表与热路径 0 GC 约束</strong></summary>

### 四层程序集拓扑

Unity `.asmdef` 引用**不传递**，跨层使用的基类或接口所在程序集必须显式写入 `references`。

```mermaid
graph TD
    P["Presentation<br/>GameRoot 组合根 · Adapters · UI · 特效 · 光照"] --> L["Logic（纯 C# 核心）<br/>禁 MonoBehaviour · 禁场景引用 · 注入时间"]
    P --> D["Data<br/>ConfigModule · *Spec 防腐层 · AssetModule · SO"]
    P --> F["Foundation<br/>Pool · StateMachine · Rng · Ballistics"]
    L --> D
    L --> F
    D --> C["cfg → Luban.Runtime<br/>generated config rows"]
    L --> C
```

- **组合根与驱动收口**：全工程仅 `GameRoot` 为常驻单例，统一调度 `Update` / `FixedUpdate` 推进 `ISceneRoot`，业务组件禁止私自散落 `Update`。
- **热路径 0 GC**：`Tick`、`FixedTick`、`OnBallHit`、`TakeDamage` 等高频热路径严禁 LINQ、匿名闭包、热路径 `GetComponent` 与瞬时容器分配；物理查询优先 `NonAlloc`。
- **瓦片网格光照场**：`TileGridLightmap` 每帧按地板尺寸烘烤一格一纹素的 Linear `RGBA32` 光照图，通过 8 邻域两趟正反扫描扩散（空气透过率 `0.75`、墙体透光 `0.20`），协同 `EnvironmentDirector` 昼/暮/夜过渡驱动全屏 `CelLit` 着色器。

### 配表与调参边界

- **14 张 Excel 真源**：核心规则与数值维护于 `ConfigWorkspace/Data/*.xlsx`（清单以 [`Assets/Scripts/Data/Config/TablesMeta.cs`](Assets/Scripts/Data/Config/TablesMeta.cs) 为准），严禁运行脚本重写源表。
- **生成物禁区**：Unity 菜单 `Luban ▸ 表格数据导入` 镜像生成至 `Assets/Scripts/Generated/Config/` 与 `Assets/StreamingAssets/Luban/`（`cfg.asmdef` 置于 `Generated/` 上层以避免被覆盖）。项目使用本地 `Assets/Luban.Runtime/`，禁止安装同名 UPM 包。
- **SO 调参与防腐层**：运动学、抛物线、震屏与视觉偏好存放于 `Assets/Resources/tuning/*.asset` 与 `config/*.asset`，Play Mode 即时生效；上层仅通过 `*Spec` 包装类读取配置。

</details>

<details>
<summary><strong>3. 文档地图（点击展开）</strong></summary>

| 分类 | 核心文档入口 |
| --- | --- |
| 总索引与待办 | [`Docs/README.md`](Docs/README.md) · [`Docs/待办.md`](Docs/待办.md) |
| AI 协作宪法 | [`AGENTS.md`](AGENTS.md) · [`AI_Context/README_AI.md`](AI_Context/README_AI.md) |
| 新手接手 | [`Docs/工程/人类接手与工程交接手册.md`](Docs/工程/人类接手与工程交接手册.md) · [`Docs/工程/加内容清单.md`](Docs/工程/加内容清单.md) |
| 架构与规范 | [`Docs/工程/架构约束.md`](Docs/工程/架构约束.md) · [`Docs/工程/目录说明.md`](Docs/工程/目录说明.md) · [`Docs/工程/注释规范.md`](Docs/工程/注释规范.md) |
| 关卡与更新 | [`Docs/工程/关卡编号与场景映射.md`](Docs/工程/关卡编号与场景映射.md) · [`Docs/更新说明/v2.0更新说明.docx`](Docs/更新说明/v2.0更新说明.docx) |
| 配表与数据 | [`Docs/配表/策划配表手册.md`](Docs/配表/策划配表手册.md) · [`Docs/配表/技术文档_配表管线.md`](Docs/配表/技术文档_配表管线.md) · [`Docs/配表/最新表格与SO规范.md`](Docs/配表/最新表格与SO规范.md) |
| 设计与反馈 | [`Docs/设计/策划案_荒原复苏指南_v4.0.md`](Docs/设计/策划案_荒原复苏指南_v4.0.md) · [`Docs/设计/玩家反馈处理指导思想.md`](Docs/设计/玩家反馈处理指导思想.md) |
| 表现与美术 | [`Docs/表现/全局光照.md`](Docs/表现/全局光照.md) · [`Docs/表现/新场景接线.md`](Docs/表现/新场景接线.md) · [`Docs/表现/粒子特效系统.md`](Docs/表现/粒子特效系统.md) · [`Docs/美术装配指南.md`](Docs/美术装配指南.md) |

> 注：若个别旧文档尚未随 v2.0 同步回写，一律以当前源码、Luban 导出 JSON 与门禁输出为准。

</details>

---

## 制作组名单

**深海鱼油工作室**

| 分组 | 人员 |
| --- | --- |
| 策划 | 代码鱿鱼、秋墨 |
| 文案 | 代码鱿鱼 |
| 程序 | 枫叶舟、V.Reason-愚人舟 |
| 美术 | Banth、芝麻王、爵士JAZZEL |
| 运营 | 鹦鹉螺 |
| 特别鸣谢 · 音乐 / 音效 | 李tingke |
| 特别鸣谢 · 美术 | 既灵 |

<div align="center">
  <br />
  <em>荒原之上，废墟与新生正同时蔓延。那是复苏的曙光，还是更深邃的黑夜？</em>
</div>
