---

> 🔴 **本文件的表契约对齐「战斗与元素反应重构」之后的状态。**
> 温湿度电三轴、`tile_effect` 效果档位表、`TileEffectValue` 多槽位结构体**已物理删除**。
> 设计口径另见：
> - `Docs/设计/策划案_荒原复苏指南_v4.0.md` 第 8 节（配置契约附录，含全部列名速查）
> - `Docs/配表/技术文档_配表管线.md` 第 2 节（10 张表逐表消费者）
> - `Docs/配表/策划配表手册.md`
>
> **本文档是"划界原则"（Excel 放什么 / SO 放什么 / 资产怎么寻址）与当前表契约的快照**，
> 第四节「美术资源加载规约」是资产寻址的唯一入口。

### 一、划分原则（第一性原则）

为了让策划不再面对无意义的物理常数，同时让程序在 Unity Editor 调手感时无需反复导表，确立以下**铁律**：

1. **Excel（策划驱动·核心玩法与规则网）**：
   - **只放**：核心结算数值（HP / 伤害 / 每波配给种子）、元素反应网（`element_rule` / `element_duo_reaction`）、地块生效节奏（存活秒数 / 移速倍率 / 每秒伤害）、波次节奏（三阶段时长）。
   - **严禁**：出现相机深度、像素尺寸、闪白频率、物理加速度、击退衰减曲线、资源物理路径。
   - **现状核对**：`tile_state` 的 `slow_rate`（移速倍率）是**消费者语义**（属性乘以倍率），不是物理加速度；
     `element_rule` 的 `impact_knockback` 用的是**格数**（冲量由执行者按格边长折算），
     所以"击退衰减率"这类手感曲线仍然在 SO（`EnemyTuning.knockbackDecay`）里。
2. **ScriptableObject（程序驱动·物理手感与视效体验）**：
   - **只放**：刚体运动学参数（加速度/转向衰减/最大外力）、投掷抛物线（弧高/时长/相机深度）、接触检测物理裕量、颜色调色板。
   - **收益**：Play Mode 下修改即时生效，调手感零编译、零导表成本。
3. **美术资产寻址（程序装配·`AssetModule.Load` 懒加载）**：
   - **禁止**在 Excel 里让策划手抄 `Resources\Icons\...` 路径（易错且无校验）。
   - **约定寻址**：美术按命名规范给图，程序按枚举统一通过 `AssetModule.Load<T>($"tiles/Tile_{state}")` 按需懒加载，进缓存池（`TilemapAdapter` 另有局部字典缓存，这两个键刻意不配对 `Release`，见第四节），绝不启动全量加载。
   - **唯一例外**：`seed.icon_key` 是**登记式 Key**（`Icons/Seed_Fire` 这种短 Key，不是 Windows 路径）。
     当前无读取点，接 HUD 图标时它会成为消费者 —— 这是刻意留的接口，不是漏删。

---

### 二、各表格字段裁剪与划界方案（一锤定音）

#### 1. `projectile.xlsx`（投掷资源表）

**表里现在只有 `id` / `name` 两列**（`type` 列与 `dso.ElementType` / `dso.Quality` 枚举都不存在；抛物线字段在 `ThrowTuning.asset`）。

| 字段 | 类型 | 说明 |
| :-- | :-- | :-- |
| `id` | `BallType` | 球种枚举（**只剩 `Water` 纯水 / `Earth` 纯土**） |
| `name` | `string!` | 显示名 |

> 落地结果由 `element_rule` 查表决定，球本身不再携带任何"地形倾向"。

#### 2. `player.xlsx`（玩家配置表）
- **表里没有 `contact_damage` 列**：接触伤害的唯一权威是敌人表的 `EnemySpec.ContactDamage`（`ContactProbe` 经 `IContactDamager` 从敌人自己身上读）。
- **仍未移出**：`knockback_impulse`（击退冲量）、`knockback_speed_limit`（受击限速）、`contact_radius`（身体接触判定半径）、`retry_delay`（死亡复活等待）**仍在表里、且由表消费** —— 分别由 `CombatRoot`（冲量与重来延时）、`PlayerLogic`（限速）、`CombatRoot` → `ContactProbe`（接触半径）读取；`PlayerConfig.cs` 里没有这几个字段，要搬就按第一节的划界原则走。
- **表里的核心理则列**（表实际还有上面那四列以及水·土弹药上限/开局、神泉回血间隔；逐列消费者见 `Docs/配表/技术文档_配表管线.md` 第 2 节）：
  | 字段                    | 类型     | 说明                               |
  | :---------------------- | :------- | :--------------------------------- |
  | `id`                    | `int`    | 编号（固定为 1）                   |
  | `name`                  | `string` | 玩家标识                           |
  | `max_hp`                | `float`  | 血量上限（当前 **5**，`float!#range=[1,99999]`） |
  | `invulnerable_duration` | `float`  | 受击无敌时长（秒，防连续暴毙）     |
  | `attack_interval`       | `float`  | 投掷间隔/CD（秒）                  |

#### 3. `enemy.xlsx`（敌人配置表）
- **表里现在是五列**：`id` / `name` / `max_speed` / `hp` / `contact_damage`。`radius` / `acceleration` / `knockback_decay` / `stop_distance` / `chase_range` 在 `EnemyTuning`（`Assets/Resources/tuning/EnemyTuning.asset`，`ConfigModule.BindAssets` 取不到即抛 `ConfigLoadException`），`flash_hz` 归 `VisualPalette`（字段名 `enemyFlashHz`）。
- **`contact_damage` 是敌人伤害的唯一权威**，类型 `int!#range=[0,999]`，两行分别是 1 / 2。`ContactProbe` 经 `IContactDamager` 从敌人自己身上读 `EnemySpec.ContactDamage`；`player` 表根本没有这一列。
- **表里保留的列**：
  - **不在表里、在 SO**：`enemyFlashHz`（受击闪烁频率）在 `VisualPalette`；`radius`（碰撞半径）、`acceleration`（加速度）、`knockbackDecay`（受击滑停衰减）、`stopDistance`（停止逼近距离）、`chaseRange`（脱战距离）在 `EnemyTuning`。
  - **保留在 Excel**（支持普通怪与精英怪两行）：
    | 字段             | 类型              | 说明                                           |
    | :--------------- | :---------------- | :--------------------------------------------- |
    | `id`             | `int`             | 1=普通怪，2=精英怪                             |
    | `name`           | `string`          | 普通怪 / 精英怪                                |
    | `hp`             | `int`             | 耐久（策划案 3.0：普通怪 **3**，精英怪 **6**） |
    | `max_speed`      | `float`           | 追击极速（策划调追逐压迫感）                   |
    | `contact_damage` | `int!#range=[0,999]` | 接触伤害（普通怪 **1**，精英怪 **2**）      |

#### 4. `tile_state.xlsx`（地块状态表）

**表里现在只有「地面残留」四项 + 导通标记**（`icon` / `willSpread` 不在表里，贴图走 `tiles/Tile_{成员名}` 约定）。

| 字段 | 类型 | 说明 |
| :-- | :-- | :-- |
| `id` | `TileStateType` | 地块状态枚举（16 项） |
| `name` | `string!` | 显示名（HUD / 诊断面板读数） |
| `duration` | `float` | 存活秒数（-1 为永久） |
| `slow_rate` | `float` | 踩在上面的移速倍率（1 = 不减速） |
| `dot_damage` | `int` | 每秒伤害（0 = 无伤害） |
| `touch_stun` | `float` | 踩上去的麻痹秒数（0 = 不麻痹；当前只有 `带电荆棘` = 1.5） |
| `is_obstacle` | `bool` | 物理阻挡墙体 |
| `is_conductor` | `bool` | **网格连锁导通体**（`基础水` / `基础土` / `普通泥浆` / `稀泥` / `导电区` 为真） |

> **权责边界**：这张表只管"留在地上之后"。**落地那一下的冲击归 `element_rule`**，
> 两张表不重叠、不互斥 —— 不再有"规则表有效果就顶掉地块表效果"的补丁逻辑。

#### 5. `wave.xlsx`（波次配置表）

**表里现在是三阶段时钟 + 战备配给，共 8 列、2 行**（`spawn_radius` / `enemies_per_wave` 都不在表里）。

| 字段 | 类型 | 说明 |
| :-- | :-- | :-- |
| `id` | `int` | 波次编号（1, 2, 3...） |
| `name` | `string!` | 显示名（HUD 预警文案） |
| `prep_time` | `float` | 备战时长（默认 10s）：发配给、让玩家铺地形 |
| `battle_time` | `float` | 战斗时长（默认 30s）：按 `spawn_interval` 出怪 |
| `settle_time` | `float` | 结算时长（默认 8s）：**必须等残余敌人清空**才推进 |
| `spawn_interval` | `float!` | 刷怪间隔（秒），下限 0.05 |
| `grant_seed` | `SeedType` | **本波备战期配给的战备种子**（表里填 `Fire` / `Elec` / `Ice`；可留空 = 不发） |

> **本波怪量与出生环都不在这张表里**：每波刷几只走 `wave_enemy` 表的 `count` 汇总；出生点是
> `CombatDirector.SpawnPosition` 以**相机可见区**为圆心、在「可见区外接半径＋`spawnScreenMargin`」到再乘
> `spawnRadiusMultiplier` 的外环上按随机角度取点（两个字段挂在场景的 `CombatDirector` 上，既不是表列也不是调参 SO）。

> 跑完全表后**循环回第一行**并继续递增波次号（无尽模式），没有"通关"终态。

#### 6. `element_rule.xlsx`（一级反应）与 `element_duo_reaction.xlsx`（二级反应）

**判定：100% 留在 Excel。这是策划的核心资产。** 表结构就是下面这两张（没有 `priority` 列）：

`element_rule.xlsx` — 球砸地面（14 行，9 列）：

| 字段 | 类型 | 说明 |
| :-- | :-- | :-- |
| `id` | `int` | 主键 |
| `source_tile` | `TileStateType` | 作用的地块原状态 |
| `ball_type` | `BallType` | 投掷的球种 |
| `result_tile` | `TileStateType` | 生成的新地貌（等于原格 = 地形不变、只结算冲击） |
| `impact_damage` | `int` | **落地瞬间伤害，当帧结算** |
| `impact_knockback` | `float` | 落地瞬间击退（格） |
| `impact_stun` | `float` | 落地瞬间麻痹（秒） |
| `impact_radius` | `float` | 落地瞬间的波及范围（格） |
| `trigger_chain` | `bool` | 是否触发网格连锁泛洪 |

> 🔴 **业务唯一键是 `(source_tile, ball_type)`**：`ReactionResolver` 按它建哈希表做 O(1) 查询，
> 重复配置会被当场 `LogError` 报出（后者不算数）；表里没有"优先级"列 —— 匹配不看行序。
> `impact_stun` **不要加 `!`**：14 行全部天然是 0，加 `!` 会导表失败。

`element_duo_reaction.xlsx` — 地面 × 地面（6 行）：

| 字段 | 类型 | 说明 |
| :-- | :-- | :-- |
| `id` | `int` | 主键 |
| `elem_a` / `elem_b` | `TileStateType` | 两个元素发生器地貌（**无序对**） |
| `result_tile` | `TileStateType` | 激发出的地貌 |
| `impact_damage` | `int` | 瞬发伤害 |
| `impact_knockback` | `float` | 瞬发击退（格） |
| `result_duration` | `float` | 结果存续秒数 |
| `effect_radius` | `float` | 波及半径（格，欧氏距离） |
| `pull_strength` | `float` | 向心吸力（单位/秒，0 = 不吸；当前只有 `超导` = 5） |
| `break_aggro` | `bool` | 是否清空范围内敌人的仇恨（当前只有 `白雾` = True） |
| `trigger_chain` | `bool` | 是否沿网格继续泛洪 |

> `(elem_a, elem_b)` 与 `(elem_b, elem_a)` 是同一条：`DuoReactionCatalog` 建**双向字典**，
> 重复配置同样当场报错。

**已退役**：`tile_effect.xlsx`（效果多档参数表）。效果不再是"效果号 + 档位"，
而是直接写在 `element_rule`（落地）与 `tile_state`（残留）两张表里。

---

### 三、ScriptableObject 承接体系（程序调参盘）

在 `Assets/Scripts/Data/Settings/` 下，程序持有以下 SO 文件（**下面这五份由 `ConfigModule.BindAssets` 装载，并经 `ConfigModule` 暴露给上层**；`Resources/config/` 里是 `PlayerConfig` / `UIConfig` / `AudioConfig`（这三份与 `EffectTuning` 也由 `BindAssets` / `EffectModule.Init` 装载），其余调参资产在 `Resources/tuning/`）：

1. **`PlayerConfig.cs`**（继承 `CharacterConfig`：`moveSpeed` / `snapToEightDirections` / `moveAcceleration` / `turnDecayRate` / `hurtDecay` / `extraForceScale` / `dashSpeed` / `dashDuration`）：
   - 自身只有输入与冲刺三项：`inputBufferTime`、`dashCooldown`、`dashBufferTime`。
   - **不含**：`knockback_impulse` / `knockback_speed_limit` / `contact_radius` / `retry_delay` 这四个字段仍在 `player` 表里由表消费。
2. **`ThrowTuning.cs`**：
   - 包含：抛物线基准时长 `flightDuration`、弧高 `arcHeight`、投掷最大/小距离、瞄准环压扁比例与透视。
3. **`EnemyTuning.cs`**：
   - 包含：碰撞半径 `radius`、加速度 `acceleration`、受击滑停衰减 `knockbackDecay`、停止逼近距离 `stopDistance`、脱战距离 `chaseRange`。
   - 每行还带一组**受击形变**调参 `squash`：强度 `amount`、普通一击时长 `normalDuration`、致死一击时长 `deathDuration`、X 压扁比 `compressRatio`、回弹曲线指数 `easePower`、强度硬上限 `maxAmount`。改这里即改观感，不用碰 `ScalePunchDriver`；`amount = 0` 即关掉该怪种的形变。**资产缺失即抛 `ConfigLoadException`**（不退回默认值）。
4. **`VisualPalette.cs`**：
   - 包含：球种颜色、敌人两态颜色（`enemyBodyNormal` / `enemyBodySlowed`）、高亮提示色、贴地件阴影色、伤害飘字四色（`damagePhysical` / `damageFire` / `damageElectric` / `damageIce`）。**受击闪白不吃色值**：它走材质属性块（见 `Docs/表现/粒子特效系统.md` §8），本表只管颜色乘数。
   - 飘字配色走 `DamageColor(int)`：形参是**元素序号**（`ElementKind` 的 0/1/2/3）而不是枚举本身 —— 刻意避免 Data → Logic 的反向依赖，未列出的序号回落 `damagePhysical`。
5. **`VisualFeelTuning.cs`**（键 `tuning/VisualFeelTuning`，资产在 `Resources/tuning/`；26 个字段）：
   - **受击**：顿帧 `hitStopSeconds`（秒）、闪白时长 `flashDuration`（秒，填进 `EffectContext.Duration`）、挤压形变兜底 `squashDuration` / `squashDurationLethal`（秒）/ `squashIntensity`（`EffectContext.Value`）—— 后三项**只在 `EnemyTuning` 对应条目填 0 时兜底**。
   - **死亡**：碎裂增强层强度 `shatterIntensity`（`EffectContext.Value`，与死亡动画帧打架时调低）。
   - **尸体回收兜底** `deathFallbackSeconds`（秒）：它**不是剪辑时长，是保险丝**。`EnemyActor.Die` 在"有 Animator 但死亡剪辑末帧没挂 `OnDeathAnimationEnd` 事件"与"没有可用 Animator"（`ActorAnimationView.HasValidAnimator` 为假）两种情况下都按它到点收口；事件先到则立即收口并取消这条兜底协程。**若该值大于死亡剪辑时长会提前截断剪辑**，所以别拿它当"死亡动画多长"的旋钮。
   - **震屏**：玩家受创 `hitShakeAmplitude` / `hitShakeDuration`（秒）；蒸汽爆轰 `blastShakeAmplitudeMin` / `blastShakeAmplitudeMax`（按波及半径区间插值）/ `blastShakeDuration`（秒）。
   - **扬尘**：`dustSpeedThreshold`（低于此速度不扬尘）/ `dustInterval`（满速时的发射节拍，秒）/ `dustReferenceSpeed`（节拍缩放的基准速度，一般取 `PlayerConfig.moveSpeed`）。
   - **伤害飘字**（`PopupDriver`）：上飘高度 `popupRiseHeight`（世界单位）、总时长 `popupDuration`（秒）、相对 `ActorOrder` 的排序偏移 `popupSortingOffset`（int）、位移缓出指数 `popupRiseEase`、alpha 淡出指数 `popupFadeEase`、**落点抬升 `popupAnchorHeight`**（世界单位：0＝物理体中心，0.6≈普通敌人头顶）、**落点随机半径 `popupJitterX` / `popupJitterY`**（±世界单位，0＝该轴不随机）。三个新旋钮由 `PopupDriver.ResolveSpawn` 一次算进 `StartPos`，随后的上升 / 淡出不受影响。落点随机走 **`PopupDriver` 自持的 `DefaultRng` 私有流**，不借全局 `Rng`（借了会把 `CombatDirector` 刷怪散布的随机序列整体平移），也不用 `UnityEngine.Random`；`sortingOrder` 仍按**锚点** y 计算，抖动不进 Y-Sort 档位。
   - **Z-Hop 伪高度**（`HopDriver`）：基准峰值高度 `hopPeakHeight`（世界单位，`Value = 1` 时的上飘高度）、滞空时长 `hopDuration`（秒）、归一化基准速度 `hopReferenceSpeed`（单位/秒，`Value = v0 / 本值`）。
   - `EffectModule` **不读这份 SO**：调用方读值填进 `EffectContext`，它只负责传递（具体消费点在各 Driver：`PopupDriver` / `HopDriver` / `ScalePunchDriver` 等）；消费者经 `ConfigModule.VisualFeel` 取。
   - 与 `EnemyTuning` 不同，资产缺失时 `LoadOrDefault()` **退回字段默认值实例**（并打一条 Warning），不抛 `ConfigLoadException`。

---

### 四、美术资源加载规约（动态按需，个别键常驻）

针对“美术只管出图，程序做配置，杜绝全量进内存”的要求，制定以下统一调用通道：

```csharp
// 规则：所有 Tile 资源统一放于 Assets/Resources/tiles/ 目录下，命名为 Tile_<枚举成员名>.asset
private TileBase TileFor(TileStateType state)
{
    // 特例表 → 本帧缓存 → tiles/Tile_{state} 约定
    if (_runtimeTileCache.TryGetValue(state, out TileBase cached)) return cached;

    // 资源系统没起来时不寻址，否则每次状态变化都抛异常
    if (!AssetModule.IsInitialized) return null;

    string key = $"tiles/Tile_{state}";

    // AssetModule 内部维护了 CacheStore 与 RefCounter：
    // 首次调用从 Resources 加载并缓存；资源不存在时返回 null，不抛异常
    TileBase tile = AssetModule.Load<TileBase>(key);

    if (tile == null)
    {
        // 兜底提示：告知程序或美术缺图，但不打断游戏运行
        Debug.LogWarning($"[Grid] 未找到地块资源: {key}，该格将不显示覆盖贴图。");

        return null;
    }

    _runtimeTileCache[state] = tile;

    return tile;
}
```
- **内存安全**：
  - 只有在战场中**真正出现**的地块状态，才会触发 `AssetModule.Load` 加载其 Tile；`TilemapAdapter` 还有一层局部 `_runtimeTileCache`，同一状态不会反复寻址。
  - 关卡结算/切换场景时，`SceneService`（`Logic/Services/SceneService.cs:56`）会触发 `AssetModule.OnSceneSwitch()`，`refCount == 0` 的条目失去冷却期保护，超上限时按 LRU 淘汰。
  - ⚠️ **但 `TilemapAdapter.TileFor` 与 `HudPanel.UpdateSeedIcon` 刻意不配对 `AssetModule.Release`**，这两个键的 `refCount` 永久 > 0 ⇒ LRU 对它们永不生效，等于常驻。因此上一条是"其余资产的稳态机制"，不是"地块贴图/种子图标的内存峰值恒定"。要不要给常驻一个显式口径见 `Docs/待办.md`。

---

### 五、当前状态与仍未做的

**表侧现状**：`projectile` 只剩 `id` / `name` 两列；`tile_state` 没有 `icon` / `willSpread`；`enemy` 没有 `flash_hz` / `stun_seconds`，只剩 `id` / `name` / `max_speed` / `hp` / `contact_damage`；`wave` 没有 `spawn_radius` / `enemies_per_wave`。

**代码侧现状**：`ProjectileSpec.cs` / `PlayerSpec.cs` / `EnemySpec.cs` 把不在表里的字段重定向至对应的 Tuning SO（`ThrowTuning` / `PlayerConfig` / `EnemyTuning` / `VisualPalette`）；`TilemapAdapter.cs` 经 `AssetModule.Load<TileBase>($"tiles/Tile_{state}")` 按需加载，并只保留 Inspector `stateTiles` 特例覆盖。

**仍未做的一件**：`player` 表的 `knockback_impulse` / `knockback_speed_limit` / `contact_radius` / `retry_delay` **还在表里且由表消费**，没有移出到 `PlayerConfig.asset`（`PlayerConfig.cs` 里没有这四个字段）。要搬就按本文件第一节的划界原则走，并同步改 `PlayerSpec` 的重定向 —— 记在 `Docs/待办.md`。
