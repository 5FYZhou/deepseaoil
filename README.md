<div align="center">

# 荒原复苏指南 (Wasteland Revival Guide)

**当末日降临，法师在吟唱，而你只有两个水龙头和一把铲子。**

[![Unity 2022.3.62f3c1](https://img.shields.io/badge/Engine-Unity%202022.3-blue.svg?style=flat-square&logo=unity)](https://unity.com/)
[![URP 14.0.12](https://img.shields.io/badge/Render-URP%202D-brightgreen.svg?style=flat-square)](https://unity.com/srp)
[![Architecture](https://img.shields.io/badge/Architecture-4--Tier%20asmdef-orange.svg?style=flat-square)](#-骨骼清奇的技术地基)
[![Hot--Path 0 GC](https://img.shields.io/badge/GC-Hot--Path%200%20Alloc-red.svg?style=flat-square)](#-骨骼清奇的技术地基)
[![Config Toolchain](https://img.shields.io/badge/Config-Luban%20JSON-yellowgreen.svg?style=flat-square)](#-数据与手感调参)

<br/>

<a href="#-30秒看懂这游戏玩什么">快速上手</a> •
<a href="#-离散化学反应实验室">反应网拆解</a> •
<a href="#-骨骼清奇的技术地基">底层架构</a> •
<a href="#-快速发车指南">运行项目</a> •
<a href="#-制作组名单">演职员表</a>

<br/><br/>

<!-- ====================【留位：主视觉横幅 BANNER】==================== -->
```
<div align="center">
  <img src="https://via.placeholder.com/1200x500/1e272e/ffffff?text=%E3%80%90%E4%B8%BB%E8%A7%86%E8%A7%89%E7%8C%9B%E7%8C%9B%E6%A8%AA%E5%B9%8FBanner%E7%95%99%E4%BD%8D%E3%80%91+1200x500" alt="游戏主KV横幅" width="100%" />
  <p><em>▲ <===[[[ 留位：放置游戏主视觉横幅 / Title Banner (建议比例 21:9 或 16:9) ]]]===></em></p>
</div>
```
<!-- =================================================================== -->

</div>

---

## 🎮 30秒看懂这游戏玩什么？

这是一款**俯视角 2D 生存追逐战**。你是一名被派往废弃基地 **REC-006** 的前线勘探员。面对铺天盖地的变异虫群，你**没有任何攻击技能**。

你唯一能做的事：**用手里的纯水和废土，跟战场上本来就有的火池、电桩、冰面、枯藤搞化学反应，借大自然的手灭了它们。**

```mermaid
flowchart LR
    A[🚰 井里打水 / 丘上挖土] --> B[🎯 瞄准地块 扔就完事]
    B --> C{💥 发生反应}
    C -->|水 + 泥| D[💩 铺泥浆 怪物移速减半]
    C -->|土 + 火| E[🧱 烧出陶砖 堵死路口]
    C -->|水 + 电| F[⚡ 全屏导电电击瘫痪]
    C -->|火 + 草| G[🌋 燎原爆燃 连锁烧尽]
    D & E & F & G --> H[🚑 跑进神泉发呆 3 秒回 1 血]
    H --> A
```

> [!TIP]
> **【废土重症 ICU · 生命神泉】**  
> 玩家只有 **5 滴血**，数值终身不成长。想回血？全图只有一口泉眼，而且你必须在里面**原地发呆静止整整 3 秒**。  
> 动一下、手抖一下、被摸一下，计时立刻归零。想活命？先进去之前用泥浆减速、陶砖封路，替自己买好这 3 秒钟。

<br/>

<!-- ====================【留位：实机核心循环 GIF】==================== -->
```
<div align="center">
  <img src="https://via.placeholder.com/800x450/2d3436/dfe6e9?text=%E3%80%90%E5%AE%9E%E6%9C%BA%E6%A0%B8%E5%BF%83%E6%88%98%E6%96%97%E4%B8%8EICU%E5%9B%9E%E8%A1%80%E5%BE%AA%E7%8E%AF+GIF%E7%95%99%E4%BD%8D%E3%80%91" alt="实机战斗动图" width="85%" />
  <p><em>▲ <===[[[ 留位：展示 投掷泥浆减速 ➔ 陶砖阻挡 ➔ 跑进神泉原地回血 的爽快 GIF 动图 ]]]===></em></p>
</div>
```
<!-- =================================================================== -->

---

## 🧪 离散化学反应实验室

我们曾经做过一套极其硬核的“温湿度+连续浮点数计算”的反应模型，最后发现不仅把自己人算懵了，玩家也看不懂。  
所以 4.0 版本坚决执行**【所见即所得 · 离散查表 · 首跳当帧】**——水就是水，火就是火，没有玄学数值。

### 1. 元素连锁网扑面而来

```mermaid
flowchart TD
    classDef ball fill:#0984e3,stroke:#74b9ff,color:#fff;
    classDef tile fill:#636e72,stroke:#b2bec3,color:#fff;
    classDef react fill:#d63031,stroke:#ff7675,color:#fff;
    classDef duo fill:#e17055,stroke:#fab1a0,color:#fff;

    W([💧 纯水弹药]):::ball
    E([🪨 纯土弹药]):::ball

    subgraph Ground [战场自然地貌]
        FT[🔥 基础火池]:::tile
        ET[⚡ 基础电源]:::tile
        PT[🌿 基础植物]:::tile
        IT[❄️ 基础冰面]:::tile
    end

    subgraph Direct [一级碰撞反应 (拿球砸)]
        W -->|砸| FT --> R_Steam[💨 蒸汽爆轰: 2伤 + 1.5格击退]:::react
        E -->|砸| FT --> R_Brick[🧱 陶砖矮墙: 实体阻挡 存活15秒]:::react
        W -->|砸| ET --> R_Elec[⚡ 导电区: 泛洪全网导电]:::react
        W -->|砸| PT --> R_Mud[💩 泥浆带: 移速直接腰斩 50%]:::react
    end

    subgraph Duo [二级跨界质变 (发生器相邻触发)]
        FT -.->|相邻| PT ==> D_Fire[🌋 燎原火海: 沿草丛 0.2s/格 延时连环炸]:::duo
        FT -.->|相邻| ET ==> D_Plasma[💥 等离子爆轰: 4伤 + 4格击退 斩杀精英怪]:::duo
        IT -.->|相邻| ET ==> D_BlackHole[🕳️ 超导黑洞: 聚怪向心吸力 5m/s]:::duo
        ET -.->|相邻| PT ==> D_Thorn[🌵 带电荆棘: 踩中硬直麻痹 1.5秒]:::duo
    end
```

### 2. 网格连锁引擎 (`TileChainReactor`)
- **闪电五连鞭**：水和湿土全是电线。只要一片水网通电，**同帧全网麻痹结算**，单次最大支持 128 格扩散，电翻全场。
- **森林大火**：火遇到植物不会瞬间算完，而是以 $0.2\text{s}/\text{格}$ 的波前节奏像多米诺骨牌一样扩散，给足视觉压迫感。

<br/>

<!-- ====================【留位：元素质变与全网导电截图】==================== -->
```
<div align="center">
  <img src="https://via.placeholder.com/800x450/2d3436/dfe6e9?text=%E3%80%90%E7%AD%89%E7%A6%BB%E5%AD%90%E7%88%86%E8%BD%B0+%2F+%E5%85%A8%E7%BD%91%E5%AF%BC%E7%94%B5%E7%89%B9%E6%95%88%E7%85%A7%E7%89%87%E7%95%99%E4%BD%8D%E3%80%91" alt="元素连锁反应图" width="85%" />
  <p><em>▲ <===[[[ 留位：电弧蔓延整片水池 / 燎原火海点燃草丛的大魄力视觉截图 ]]]===></em></p>
</div>
```
<!-- =================================================================== -->

---

## 🛠️ 骨骼清奇的技术地基

谁说独立小游戏代码就可以乱成一锅粥？本项目贯彻了强迫症级别的工业化整洁度。

### 1. 严格四层物理单向隔离 (`asmdef`)
我们给每个层级都打了独立程序集定义。**想反向引用？编译器直接给你甩脸看。**

```mermaid
graph TD
    classDef pres fill:#ff7675,stroke:#d63031,stroke-width:2px,color:#fff;
    classDef logic fill:#74b9ff,stroke:#0984e3,stroke-width:2px,color:#fff;
    classDef data fill:#ffeaa7,stroke:#fdcb6e,stroke-width:2px,color:#333;
    classDef fdn fill:#55efc4,stroke:#00b894,stroke-width:2px,color:#333;

    P["🎮 Presentation (表现层)<br/>GameRoot · 战斗切片 · UI · 特效"]:::pres
    L["🧠 Logic (纯 C# 逻辑层)<br/>🚫 0 MonoBehaviour · 🚫 0 Unity引用 · 纯状态机与数学"]:::logic
    D["📦 Data (数据与配置层)<br/>Luban 自动生成类 · Spec 防腐层 · 调参 ScriptableObject"]:::data
    F["🧱 Foundation (纯净地基)<br/>自研对象池 · 确定性弹道 · 零依赖工具库"]:::fdn

    P -->|单向驱动| L
    L -->|纯数据查询| D
    D -->|底层依赖| F
    L -.->|数学调用| F
```

- **纯 C# 的逻辑核**：`Logic/` 目录下没有任何 `MonoBehaviour`、不读 `Transform`，甚至连 `Time.time` 都不许直接碰（全靠驱动层精准喂入）。脱离 Unity 引擎它照样能跑单元测试。
- **双帧收敛模型**：杜绝全场景散落各处的流氓 `Update`。全工程唯二驱动入口由常驻单例 `GameRoot` 统一编排调度。
- **热路径绝对 0 GC**：在每帧 Tick、碰撞结算、怪物伤害等高频跑道上，**禁用 LINQ、禁用匿名 Lambda 闭包、禁用瞬时 new**，物理检测全走 `NonAlloc`。

### 2. 告别传统点光：泰拉瑞亚同款网格光照场 (`TileGridLightmap`)
用 Unity URP 2D 原生点光，同屏放超过 16 盏灯显卡就得喘气，而且火海的光斑是圆的，一点都不贴合地砖。
我们自研了一套**微型瓦片网格扩散光场**：

```mermaid
flowchart LR
    Map[🗺️ 地貌火/电/泉眼] --> Diffuse[🧮 8 邻域两趟正反扫描扩散<br/>考虑空气透过率 0.75 + 墙体遮挡]
    Diffuse --> Tex[🖼️ 烘成 RGBA32 微型光照图<br/>一格一像素 · 零显存浪费]
    Tex --> Shader[🎨 注入全屏 CelLit 着色器<br/>max 通道混合 · 绝不过曝发灰]
```

<br/>

<!-- ====================【留位：昼夜光照与网格光对比图】==================== -->
```
<div align="center">
  <img src="https://via.placeholder.com/800x450/2d3436/dfe6e9?text=%E3%80%90%E5%A4%9C%E6%99%9A%E7%81%AB%E5%85%89%E7%85%A7%E4%BA%AE%E5%A2%99%E8%A7%92%E9%98%B4%E5%BD%B1+%E6%AF%94%E8%BE%83%E5%9B%BE%E7%95%99%E4%BD%8D%E3%80%91" alt="光照系统展示" width="85%" />
  <p><em>▲ <===[[[ 留位：展示黑夜环境下，火光贴着障碍物扩散出硬核阴影的视觉效果图 ]]]===></em></p>
</div>
```
<!-- =================================================================== -->

---

## 📊 数据与手感调参

策划改数值不想老求着程序改代码，程序也不想因为策划想试下击退速度就重新打一遍表。所以我们严厉划界：

```mermaid
flowchart TD
    subgraph Excel [Excel 策划真源 (Luban管线)]
        E1[敌人生命/伤害]
        E2[元素规则 element_rule]
        E3[波次时长与配给]
    end

    subgraph SO [ScriptableObject 程序调参]
        S1[运动学加速度 / 转向衰减]
        S2[投掷抛物线弧度]
        S3[受击闪白时长 / 震屏参数]
    end

    Excel -->|导表生成 C# & JSON| Generated[Generated/ 只读类]
    Generated --> Spec[🛡️ Spec 防腐包装层]
    SO --> Spec
    Spec --> Runtime[🚀 运行时无感读取]
```

- **策划管玩法**：所有的血量、反应配方、波次敌种都在 `ConfigWorkspace/Data/*.xlsx` 里，一键导入。
- **手感管体验**：抛物线、惯性滑行、相机震动全在 `Resources/tuning/*.asset` 里，**游戏运行态（Play Mode）直接拖滑杆即时生效**。

---

## 🚀 快速发车指南

### 1. 环境准备
- **Unity 编辑器版本**：`2022.3.62f3c1` (LTS)
- **渲染管线**：URP 14.0.12 (2D Renderer, Linear 色彩空间)
- **脚本终端**：PowerShell 7+ (`pwsh`)

### 2. 跑起你的第一局
1. 双击打开主玩法基准场景：`Assets/Scenes/Range.unity`。
2. 确保场景第一个根挂有 `[LevelEnvironment_Preset]`，最后一个根是 `Weapon`。
3. 点击 Unity 的 **Play** 按钮直接进入战场！
   - *（想安安静静测试投掷反应？在 Hierarchy 里选中 `CombatRoot`，把 `Enable Waves` 取消勾选，全世界就清静了。）*

### 3. 操作键位速查
|                        键位                         |     动作     | 功能说明                                                |
| :-------------------------------------------------: | :----------: | :------------------------------------------------------ |
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> |   **走位**   | 8 轴平滑带吸附移动                                      |
|                <kbd>Left Shift</kbd>                |   **冲刺**   | 战术位移，带独立 CD 与输入缓冲（按早了不会吞键）        |
|              <kbd>鼠标左键 (LMB)</kbd>              | **喷射水球** | 消耗 1 格水，制造水体/稀泥/蒸汽                         |
|              <kbd>鼠标右键 (RMB)</kbd>              | **投掷土块** | 消耗 1 格土，制造泥浆/陶砖路障                          |
|                    <kbd>E</kbd>                     | **播撒种子** | 消耗便携种子，在空地上插下专属环境桩                    |
|                    <kbd>Q</kbd>                     | **丢弃种子** | 把拿在手里的种子丢在脚边（带 $1.2\text{s}$ 防误拾冷却） |
|                   <kbd>Esc</kbd>                    | **战术暂停** | 呼出菜单，冻结物理与逻辑时钟                            |

### 4. 严苛的代码守门员
提交代码前，终端敲这三行，保你一生平安：

```powershell
pwsh Tools/compile-gate.ps1 -Topology   # 必须 0 Error, 0 Warning！强力拦截反向引用
pwsh Tools/comment-lint.ps1             # 注释不能水，超出字符预算当场报警
pwsh Tools/luban-mirror.ps1             # 检查配表镜像与生成代码是否对齐
```

---

## 🎬 制作组名单

**深海鱼油工作室 (DeepseaOil Studio)**

```mermaid
mindmap
  root((荒原复苏指南))
    策划组
      代码鱿鱼 (玩法架构 / 数值)
      秋墨 (系统 / 关卡设计)
    文案剧作
      代码鱿鱼
    程序工程
      枫叶舟 (架构 / 表现系统)
      V.Reason-愚人舟 (核心逻辑 / 工具链)
    美术动效
      Banth
      芝麻王
      爵士JAZZEL
    宣发运营
      鹦鹉螺
    特邀鸣谢
      李tingke (音乐 / 音效制作)
      既灵 (美术协力)
```

<div align="center">

<br/>

*“荒原之上，废墟与新生正同时蔓延。那是复苏的曙光，还是更深邃的黑夜？”*

</div>
