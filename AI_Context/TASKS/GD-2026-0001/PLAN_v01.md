--- START OF TASK ---
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-2026-0001
- Plan Version: v01
- Status: APPROVED
- Task Level: L2 / Risk Level: R2
- Project / Engine / Platform: 荒原复苏指南 (deepseaoil) / Unity 2022.3.62f3c1 (URP 14.0.12, 2D Renderer) / Windows Standalone & Unity Editor
- Branch / Source Base Commit: `ai/GD-2026-0001` / `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`（施工 Worktree 从批准提交后的 `VReason/Update` 分出；批准提交仅增加任务文档，不改变源码基线）
- Planner: Planner Chat（`planner-orchestrator` + `game-systems-designer` + `unity2d-architect` + `task-brief-writer`）
- Worker: 独立 Worker 会话（Agent 模式，已配置 Unity / 改表技能）
- Watchdog: 独立 Watchdog 会话（`$watchdog-inspector`，不得与 Worker 共用会话）
- Context: `AI_Context/TASKS/GD-2026-0001/00_CONTEXT_MANIFEST.md` + Base Commit `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`
- Execution Attempt Cap: A01–A03
- Created At: 2026-11-25

## 1. Objective
完成游戏核心玩法底层修复与手感数值升级（批 1 / 3），实现以下可观察且可验证的结果：
1. 修复静态距离场烘焙污染 Bug：开局存在生命周期的临时障碍地貌不再被永久写入 `NavMap`，动态阻挡地貌由 `NavField` 实时计算邻域净空。
2. 修复陶砖生成压怪卡死：生成 `TerracottaBrick` 时，位于砖格内重叠的敌人被立即挤推至最近可通行位置，不再卡死于砖体碰撞盒内部。
3. 升级生命神泉回血曲线与范围：静止蓄力加速（空蓄至满最快约 1.5–1.8s），圈内移动不再清零而是缓慢衰减（走停体感约 2.0–2.5s），出圈缓慢衰减，总门槛口径保持 3s 上限；生命泉触发半径扩大 15%。
4. 调整敌人速度曲线与元素规则表：降低敌人满速与提高入弯保速，去除霜冻冰刺冻结麻痹改为大幅减速，全面升级元素反应作用与铺场范围并修正泥浆覆盖优先级防止平级误盖。
5. 增强网格光照脉冲可见度：拉长蒸汽与电弧闪光脉冲余晖并增大电弧脉冲半径，使光效时长与粒子特效匹配。

## 2. Non-goals
1. 本批**不修改** `CombatRoot.cs`、`PlayerController.cs`、`PlayerStats.cs`（受击穿怪幽灵化、死亡滑行修复及 Bug 2 配给种子不覆盖已拆分至 `GD-2026-0002`）。
2. 本批**不修改** `DialoguePanel.cs`、`Typewriter.cs`、`UIFader.cs`、`StoryFlow.cs`、`AudioManager.cs`、`GamePlayPanel.cs` 与 `slot_dialogue.xlsx`（已拆分至 `GD-2026-0003`）。
3. 明确**不做**：抛物线弧线预测线、靶场模式入口、扩大 `5+5(+3)` 弹药上限、修改水/土泉眼“随机吐+1拾取”机制。
4. **不清理**非本批涉及的既有注释超标文件（`TimerWheel.cs`、`AudioManager.cs`、`SaveSlot.cs`）。
5. **不更新** `Docs/设计/策划案_荒原复苏指南_v4.0.md`（留待三批全部合入后统一依据代码真值回写）。
6. 严禁更改 `Packages/manifest.json`、新增 `.asmdef` 或安装 UPM Luban 包。

## 3. Current State
- `[VERIFIED]` **Bug 1（临时障碍永久进距离场）**：`Assets/Scripts/Presentation/Root/CombatRoot.cs:572-578` 先执行 `LoadInitialSetupTiles` / `LoadInitialStates`（可写入带生命周期的 `TerracottaBrick`），再调用 `SceneObstacleBaker.Bake`；而 `Assets/Scripts/Presentation/Grid/SceneObstacleBaker.cs:46-68` 将 `grid.IsObstacle(cell)` 永久写入静态 `NavMap` 的 `blocked[]` 与 `TerrainClearance`。与此同时 `Assets/Scripts/Logic/Grid/NavField.cs:195-219` 已有基于 `terrainVersion` 的 `BuildTerrain` 动态阻挡过滤，但未计算动态阻挡格对邻格边缘采样点的净空缩减。
- `[VERIFIED]` **陶砖生成卡怪**：`Assets/Scripts/Presentation/Grid/TileObstacleDirector.cs:104-125` 在 `OnTileStateChanged` 同帧激活 `BoxCollider2D` 盖在砖格中心；而 `element_rule` 的 `impact_knockback = 1` 仅排队到下一物理帧由 `EnemyLogic` 执行，导致怪物被同步生成的碰撞盒卡死在砖体内部。
- `[VERIFIED]` **生命神泉**：`Assets/Scripts/Logic/World/LifeFountainState.cs` 只要触发移动、速度 > `0.05` 或位移 > `0.002` 即调用 `Leave()` 将 `_still` 清零；`Assets/Scripts/Presentation/World/Fountain.cs` `autoTriggerRadius = 1.5f` 且在预制体已有 `Collider2D` 时不覆盖半径；`Fountain.cs` 当前处于 `comment-lint.ps1` 超预算状态（`Tools/comment-budget.json` 上限 280）。
- `[VERIFIED]` **敌我速度与曲线**：`Assets/Resources/config/PlayerConfig.asset` 玩家 `moveSpeed = 8`；`Assets/StreamingAssets/Luban/dso_tbenemy.json` 普通怪 `max_speed = 7.2`、精英怪 `max_speed = 8.6`（超玩家速度）；`Assets/Resources/tuning/EnemyTuning.asset` 中 `cornerSpeedFactor = 0.4` 造成拐角骤降速。
- `[VERIFIED]` **元素反应与冰刺现状**：
  - `Assets/StreamingAssets/Luban/dso_tbtilestate.json`：`霜冻冰刺 (id=23)` 当前 `touch_stun = 1`、`slow_rate = 0.6`、`dot_damage = 2`；`基础水(14)` / `基础土(15)` / `普通泥浆(2)` / `导电区(20)` / `燎原火海(21)` 均为 `priority = 10`（平级后写赢）。
  - `Assets/StreamingAssets/Luban/dso_tbelementrule.json`：`泥浆(1/2)` 与 `陶砖(5/9/10/11/16)` 已为 `impact_radius = 1`；`空地+水/土(13/14)`、`稀泥(3)`、`导电(6)`、`结冰(7/12)`、`植物+水(8)`、`同元素重复(17/18)` 仍为 `0`；`蒸汽(4)` 与 `基础花+水(15)` 为 `1.5`。
  - `Assets/StreamingAssets/Luban/dso_tbelementduoreaction.json`：`id 1/2/5/7` 为 `effect_radius = 1`，`id 3/6`（均为蒸汽）为 `1.5`，`id 4`（超导结冰）为 `2.5`。
- `[VERIFIED]` **光脉冲参数**：`Assets/Scripts/Presentation/Lighting/TileGridLightmap.cs:111` 的 `impulseDecayRate` 默认 `14f`（约 0.18s 熄灭）且 `[Range(6f, 30f)]` 限制了 Inspector 下限；`Assets/Scripts/Data/Settings/VisualFeelTuning.cs` 默认 `arcImpulseRadius = 0f`、`arcImpulseIntensity = 1.4f`、`steamImpulseIntensity = 1.8f`（资产 `VisualFeelTuning.asset` 实值待 Worker 读取确认 `[UNKNOWN]`）。
- `[UNKNOWN]` `Assets/Tests/Runtime/Editor/测试夹具.cs` 中 `RowFactory.EnemyRow().MaxSpeed` 的硬编码值未在上下文内；而 `寻路Tests.cs:638` 断言 `Mathf.Sqrt(cornerSpeed^2 + 2*5*d) < MaxSpeed`，需 Worker 在修改 `cornerSpeedFactor` 时实核。
- `[UNKNOWN]` 场景内生命泉实例或预制体在 `Assets/Resources/` 中的精确路径与现有 `CircleCollider2D.radius` 值，需 Worker 检索确认。

## 4. Constraints
1. **程序集与分层边界**：遵守 `Presentation -> Logic -> Data -> Foundation` 单向依赖；`Logic` 严禁引用 `UnityEngine.Collider2D`、`MonoBehaviour` 或 `Time.*`。
2. **热路径 0 GC**：`NavField.BuildTerrain`、`TileObstacleDirector.OnTileStateChanged`、`TileGridLightmap.Tick` 严禁 LINQ、匿名闭包与每帧容器分配；物理查询须使用 `NonAlloc`。
3. **配表安全铁律（`ConfigWorkspace/AGENTS.md`）**：
   - 本批涉及 `.xlsx`、`.asset`、`.prefab` 及 `comment-budget.json` 的修改已由 CEO 在本任务 `APPROVAL_RECORD.md` 中**专项授权越权**；
   - **严禁**使用 `openpyxl` 重建工作表或任何覆盖源表的脚本（会破坏第 1–3 行嵌套子表头与别名下拉框）；仅允许保留结构的单元格级更新；
   - Luban 导入必须先写 `ConfigWorkspace/output/` 校验通过后再镜像到 `Assets/Scripts/Generated/Config/` 与 `Assets/StreamingAssets/Luban/`，且禁止手写覆盖生成目录。
4. **注释预算**：修改 `Fountain.cs` 等文件必须使 `pwsh Tools/comment-lint.ps1` 覆盖文件不增债（清理 `Fountain.cs` 冗长旧注释至预算内，或按批准更新 `Tools/comment-budget.json`），XML 注释只允许 `<summary>` / `<remarks>`。
5. **Worktree 隔离约束**：不得修改 Batch 2/3 独占的 `CombatRoot.cs`、`PlayerController.cs`、`PlayerStats.cs`、`DialoguePanel.cs`、`StoryFlow.cs`、`AudioManager.cs`、`GamePlayPanel.cs`。

## 5. Proposed Design

### 5.1 Bug 1：距离场与动态障碍净空解耦（`SceneObstacleBaker.cs` + `NavField.cs`）
- **`Assets/Scripts/Presentation/Grid/SceneObstacleBaker.cs`**：
  - `Bake` 中 `blocked[i]` 改为仅标记无地板格：`!grid.HasCell(cell) ? (byte)1 : (byte)0`，**不再读取 `grid.IsObstacle(cell)`**。
  - `TerrainClearance` 仅围绕缺失地板格计算静空边界（或仅针对 `!grid.HasCell(cell)` 边界，避免初始临时 `TerracottaBrick` 进入静态 `NavMap`）。静态场景碰撞体净空逻辑保持不变。
- **`Assets/Scripts/Logic/Grid/NavField.cs`**：
  - 在 `BuildTerrain(NavMap map, GridLogic grid, float skin)` 中（仅在 `terrainDirty` 时运行，0 GC）：
  - 先按格扫描其 8 邻域是否存在 `grid.IsObstacle(neighbor)`（每格 1 次检查，无邻障碍格时零额外计算）；
  - 若某非阻挡格存在至少 1 个动态阻挡邻格，则对其内部每个采样点计算到各阻挡邻格矩形 `[obsCenter - half, obsCenter + half]` 的欧氏距离平方；若 `minDistSq <= clearance * clearance`（即等效 `clearance <= Radius + skin`），则置 `_free[index] = 0`。
  - **效果**：开局临时 `TerracottaBrick` 到期消散后，`_navFields.MarkDirty()` 触发 `BuildTerrain` 重建，动态阴影同步恢复可走；同时局内玩家新砌的陶砖也获得精准净空边缘。

### 5.2 陶砖生成防卡怪（`TileObstacleDirector.cs` + `TilemapAdapter.cs`）
- **`Assets/Scripts/Presentation/Adapters/TilemapAdapter.cs`**：
  - 在 `RegisterCells(GridLogic grid)` 中将 `grid` 引用传给 `obstacleDirector?.SetGrid(grid)`（不修改 `CombatRoot.cs` 签名）。
- **`Assets/Scripts/Presentation/Grid/TileObstacleDirector.cs`**：
  - 预分配 `Collider2D[] _pushBuffer = new Collider2D[16]` 与 `List<EnemyActor> _pushedEnemies = new List<EnemyActor>(8)` 复用缓冲（0 GC）。
  - 在 `OnTileStateChanged(TileStateChanged evt)` 刚激活阻挡体（或在放置 `BoxCollider2D` 前同步瞬间）：使用 `Physics2D.OverlapBoxNonAlloc(center, boxSize, 0f, _pushBuffer)` 检出该格内重叠的非玩家受控角色（识别 `EnemyActor` 与其 `EnemyMotor` / `Rigidbody2D`）。
  - 对每个重叠敌人，按从 `center` 指向 `enemy.Position` 的方向（重合时固定取可用邻格方向）搜索目标格的 8 邻格（优先 4 正交邻格）：选择满足 `_grid == null || (_grid.HasCell(n) && !_grid.IsObstacle(n) && (_grid.Nav == null || _grid.Nav.IsWalkable(n, enemy.Radius, ConfigModule.NavSkin)))` 且与当前方向最接近/距离最近的邻格。
  - 若找到安全邻格，将物理体坐标直接移出当前砖格边界至邻格安全落点（保证距砖格边至少 `enemy.Radius + 0.05f`），并清零朝向砖格内的速度分量；随后立即同步推进该 `EnemyActor` 格子归属（避免继续留在该砖格登记表里被遮挡或无法寻路）。

### 5.3 生命神泉：静止加速蓄力 + 移动缓衰 + 15% 扩圈（`LifeFountainState.cs` + `Fountain.cs`）
- **`Assets/Scripts/Logic/World/LifeFountainState.cs`**：
  - 保留既有公有契约（`StillSeconds`、`HealInterval`、`IsStill`、`Leave()`、`Tick(...)`），保证 `CombatRoot.TickLifeFountain` 无需修改即可无缝接入。
  - 引入连续静止时长 `_stillStreak`（秒），参数口径（可定义为只读常量，与 `HealInterval = 3` 对齐）：
    - **静止蓄力（加速）**：当 `withinRange && !hasMoveInput && speed <= SpeedEpsilon && delta <= MoveEpsilon` 时，`IsStill = true`，`_stillStreak += deltaTime`，有效推进速率 `rate = Mathf.Min(MaxChargeRate, BaseRate + AccelPerSec * _stillStreak)`（基准选型：`BaseRate = 1.0f`、`AccelPerSec = 1.0f`、`MaxChargeRate = 2.8f`；从 0 连续站满 `3.0` 仅需 **~1.65s**，落在 1.5–1.8s 目标区间）；`_still += rate * deltaTime`；当 `_still >= _healInterval` 时回血并清零 `_still` 与 `_stillStreak`。
    - **圈内移动（缓慢衰减，不清零）**：当 `withinRange` 为真但玩家正在移动/未停稳时，`IsStill = false`，`_stillStreak = 0f`，`_still = Mathf.Max(0f, _still - InCircleDecayPerSec * deltaTime)`（选型 `InCircleDecayPerSec = 0.35f`，停走拉扯平均体感 ~2.0–2.5s，最长有效阈值仍对齐 `_healInterval = 3`）。
    - **离开圈外（缓慢衰减，不清零）**：当 `!withinRange` 时，`IsStill = false`，`_stillStreak = 0f`，`_still = Mathf.Max(0f, _still - OutCircleDecayPerSec * deltaTime)`（选型 `OutCircleDecayPerSec = 0.6f`）；`Leave()` 保留显式硬清零语义（供重置/切场用）。
- **`Assets/Scripts/Presentation/World/Fountain.cs`**：
  - 增加 `[SerializeField] private float lifeTriggerRadiusMultiplier = 1.15f;`；在 `EnsureTrigger()`（或 `Awake`）中，当 `kind == FountainKind.Life` 且已有 `CircleCollider2D` 时，按乘数将其 `radius *= lifeTriggerRadiusMultiplier`（若无则先创建 `autoTriggerRadius * lifeTriggerRadiusMultiplier`），保证无论预制体是否自带碰撞体都能统一扩大 **15%**。若预制体上有对应 `radiusView` 可视圈，同步微调其局部缩放 ×1.15（仅表现同步，不增设环形读条）。
  - 清理 `Fountain.cs` 内超预算注释，使其符合 `Tools/comment-budget.json`（上限 280 或最小定点登记）。

### 5.4 光脉冲持续时间与范围增强（`TileGridLightmap.cs` + `VisualFeelTuning.asset`）
- **`Assets/Scripts/Presentation/Lighting/TileGridLightmap.cs`**：
  - 将 `impulseDecayRate` 的 `[Range(6f, 30f)]` 放宽为 `[Range(1f, 30f)]`，代码默认初值从 `14f` 下调至 `4.5f`（约 0.55–0.7s 可见余晖，与粒子消散匹配）。
  - （注意：若场景中 `TileGridLightmap` 实例或预制体覆写了序列化值 `14`，Worker 须同步检查并调整场景/预制体上的 `impulseDecayRate`，或检查是否只有脚本默认值生效。）
- **`Assets/Resources/tuning/VisualFeelTuning.asset`**：
  - 扩大 `arcImpulseRadius`（由 `0` 调大至 `1.5` 左右，使电弧照亮中心及周围一圈），微调 `arcImpulseIntensity`（如 `1.4 -> 1.55`）与 `steamImpulseIntensity`（核对实资产原值后保持高亮不过曝）。

### 5.5 数值、范围与冰刺调优（`EnemyTuning.asset` + 4 张 `.xlsx` + Luban 导表）
1. **`Assets/Resources/tuning/EnemyTuning.asset`**：
   - `cornerSpeedFactor`：`0.4` -> `0.68`（大幅减轻拐弯骤降导致的“忽快忽慢”不可预判感；须与 `寻路Tests.拐角前按刹车距离降速` 交叉验算）。
2. **`ConfigWorkspace/Data/enemy.xlsx`**（对比玩家移速 `8.0`）：
   - `id=1 (普通怪)`：`max_speed` `7.2` -> `6.3`（HP/接触伤暂不变，后期靠波次数量保压）。
   - `id=2 (精英怪)`：`max_speed` `8.6` -> `7.7`（不超越玩家极速，配合更高 `cornerSpeedFactor` 保持持续追压，不削弱中后期难度）。
3. **`ConfigWorkspace/Data/tile_state.xlsx`**：
   - `id=23 (霜冻冰刺)`：`touch_stun` `1` -> `0`（移除踩踏冻结/麻痹），`slow_rate` `0.6` -> `0.38`（改为大幅减速）。
   - `id=2 (普通泥浆)`：`priority` `10` -> `12`（高于 `基础水(14)` 与 `基础土(15)` 的 `10`，防止十字铺水/土误覆盖已成型的泥浆；低于 `陶砖/蒸汽/结冰/冰刺/荆棘(15)` 与 `永久发生器(20)`）。
4. **`ConfigWorkspace/Data/element_rule.xlsx`**（范围统一升级，`陶砖` 保十字）：
   - `空地+水/土 (id 13, 14)`：`impact_radius` `0` -> `1`（默认十字 5 格）。
   - `同元素重复 (id 17, 18)`：`impact_radius` `0` -> `1`（仅冲击伤害扩十字，因目标地貌同当前态不触发重铺）。
   - `稀泥 (id 3)`、`导电区 (id 6)`、`结冰 (id 7, 12)`、`植物+水->泥浆 (id 8)`：`impact_radius` `0` -> `1`（十字 5 格）。
   - `泥浆 (id 1, 2)`、`陶砖 (id 5, 9, 10, 11, 16)`：保持 `1`（十字，避免 3×3 砖墙自堵）。
   - `蒸汽 (id 4)`、`基础花+水->植物区 (id 15)`：`impact_radius` `1.5` -> `2.5`（3×3 升 5×5）。
5. **`ConfigWorkspace/Data/element_duo_reaction.xlsx`**（高阶整体升一档）：
   - `id 1, 2 (燎原火海)`、`id 5 (带电荆棘)`、`id 7 (霜冻冰刺)`：`effect_radius` `1` -> `1.5`（十字 -> 3×3）。
   - `id 3 (火×电 蒸汽)`、`id 6 (冰×火 蒸汽)`：`effect_radius` `1.5` -> `2.5`（3×3 -> 5×5）。
   - `id 4 (冰×电 超导结冰)`：`effect_radius` `2.5` -> `3.5`（5×5 -> 约 7×7）。

## 6. File Scope

### READ
- `AGENTS.md`、`ConfigWorkspace/AGENTS.md`、`AI_Context/BUILD_AND_TEST.md`
- `Assets/Scripts/Logic/Grid/GridLogic.cs`、`Assets/Scripts/Logic/Grid/NavMap.cs`、`Assets/Scripts/Logic/Grid/GridQuery.cs`
- `Assets/Scripts/Presentation/Root/CombatRoot.cs`（只读核对装配时序，**禁止修改**）
- `Assets/Tests/Runtime/Editor/测试夹具.cs`、`Assets/Tests/Runtime/Editor/地块优先级Tests.cs`、`Assets/Tests/Runtime/Editor/状态工厂Tests.cs`、`Assets/Tests/Runtime/Editor/寻路Tests.cs`、`Assets/Tests/Runtime/Editor/地块效果Tests.cs`、`Assets/Tests/Runtime/Editor/场景碰撞体烘焙Tests.cs`、`Assets/Tests/Runtime/Editor/TileGridLightmapTests.cs`、`Assets/Tests/Runtime/Editor/泉眼发光Tests.cs`
- 场景与预制体只读检索：`Assets/Scenes/Range.unity`、`Assets/Scenes/Levels/Level_*.unity`、`Assets/Resources/scane_prebs/`（仅查生命泉与 `TileGridLightmap` 挂载覆写情况）

### CREATE
- `Assets/Tests/Runtime/Editor/生命神泉Tests.cs`（或对应 `LifeFountainState` + `TileObstacleDirector` 逻辑测试文件，须同步在 `Tools/comment-budget.json` 登记）

### MODIFY（含 CEO 审批专项授权项）
- `Assets/Scripts/Presentation/Grid/SceneObstacleBaker.cs`
- `Assets/Scripts/Logic/Grid/NavField.cs`
- `Assets/Scripts/Presentation/Adapters/TilemapAdapter.cs`
- `Assets/Scripts/Presentation/Grid/TileObstacleDirector.cs`
- `Assets/Scripts/Logic/World/LifeFountainState.cs`
- `Assets/Scripts/Presentation/World/Fountain.cs`
- `Assets/Scripts/Presentation/Lighting/TileGridLightmap.cs`
- `Assets/Resources/tuning/EnemyTuning.asset` *(CEO 授权修改 SO)*
- `Assets/Resources/tuning/VisualFeelTuning.asset` *(CEO 授权修改 SO)*
- `ConfigWorkspace/Data/enemy.xlsx` *(CEO 授权改表，禁脚本覆盖)*
- `ConfigWorkspace/Data/tile_state.xlsx` *(CEO 授权改表，禁脚本覆盖)*
- `ConfigWorkspace/Data/element_rule.xlsx` *(CEO 授权改表，禁脚本覆盖)*
- `ConfigWorkspace/Data/element_duo_reaction.xlsx` *(CEO 授权改表，禁脚本覆盖)*
- Luban 导表输出区（仅经导表工具镜像覆盖，严禁手写）：`Assets/Scripts/Generated/Config/**`、`Assets/StreamingAssets/Luban/**`
- 若场景/预制体存在覆盖值且必须同步：`Assets/Resources/scane_prebs/TilemapAdapter.prefab`（或生命泉/光场对应 Prefab，仅限 `impulseDecayRate` / 生命泉 `CircleCollider2D` 同步）*(须记录精确路径于执行报告)*
- `Assets/Tests/Runtime/Editor/场景碰撞体烘焙Tests.cs`、`寻路Tests.cs`、`地块效果Tests.cs`（按需补充回归用例）
- `Tools/comment-budget.json` *(CEO 授权清理/登记新增测试或更新 `Fountain.cs` 注释预算)*

### FORBIDDEN（与批 2/3 并行冲突防线）
- **严禁修改**：`Assets/Scripts/Presentation/Root/CombatRoot.cs`、`Assets/Scripts/Presentation/Root/PlayerController.cs`、`Assets/Scripts/Logic/Player/PlayerStats.cs`、`Assets/Scripts/Presentation/UI/**`、`Assets/Scripts/Presentation/Story/**`、`Assets/Scripts/Presentation/Audio/**`
- **严禁修改**：`Assets/Scripts/Logic/Services/Timer/TimerWheel.cs`、`Assets/Scripts/Presentation/UI/Panel/SaveSlot.cs`、`Assets/Scripts/Generated/Input/InputSys.cs`、`Assets/Scripts/Generated/cfg.asmdef`、`Packages/manifest.json`、`ProjectSettings/**`
- **严禁手写**：`Assets/Scripts/Generated/Config/**`、`Assets/StreamingAssets/Luban/**`、`ConfigWorkspace/output/**`

## 7. Execution Steps
1. **Preflight & 只读查证**：
   - 检查分支/Worktree、`git status --short --branch`、Base Commit。
   - 读取 `ConfigWorkspace/AGENTS.md` 与 `Assets/Tests/Runtime/Editor/测试夹具.cs`，确认 `RowFactory.EnemyRow().MaxSpeed` 与 `寻路Tests.拐角前按刹车距离降速` 的数学关系；检索 `Assets/Resources/` 及 `Level_*.unity`/`Range.unity` 确认生命泉预制体与 `TileGridLightmap.impulseDecayRate` 是否有序列化覆写。
   - 确认预期结果：无未知阻碍后进入实施；若 `测试夹具.cs` 校验与 `cornerSpeedFactor=0.68` 冲突，记录并与 Planner/CEO 确认是否微调 `0.62~0.68`。
2. **修复 Bug 1（`SceneObstacleBaker.cs` + `NavField.cs`）**：
   - 前置：已阅读 `场景碰撞体烘焙Tests.cs`、`寻路Tests.cs`。
   - 动作：移除 `SceneObstacleBaker.Bake` 中对 `grid.IsObstacle(cell)` 的静态烘焙，仅保留地板缺失与静态碰撞体烘焙；在 `NavField.BuildTerrain` 增加对 8 邻域动态 `grid.IsObstacle` 单元格边缘采样点的 `clearance` 距离剔除（0 GC）。
   - 验证：新增/运行 `场景碰撞体烘焙Tests`（验证初始临时 `TerracottaBrick` 不进入 `NavMap` 且到期后完全可走）与 `寻路Tests` 全绿。
3. **修复陶砖生成压怪卡死（`TilemapAdapter.cs` + `TileObstacleDirector.cs`）**：
   - 前置：Step 2 完成，不触碰 `CombatRoot.cs`。
   - 动作：`TilemapAdapter.RegisterCells` 注入 `GridLogic` 到 `TileObstacleDirector`；`TileObstacleDirector.OnTileStateChanged` 激活阻挡砖时通过 `Physics2D.OverlapBoxNonAlloc` 检出格内敌人并向最近可通行邻格推挤释放（同时更新其格子登记），无临时 GC 分配。
   - 验证：编写单元测试或 Play 验证确保敌人站在生成点时变砖后立即脱离砖体可继续绕路/受击。
4. **升级生命神泉曲线与扩圈 15%（`LifeFountainState.cs` + `Fountain.cs`）**：
   - 前置：检查 `泉眼发光Tests.cs` 与生命泉预制体。
   - 动作：在 `LifeFountainState` 实现静止加速蓄力（空蓄约 1.65s 满）、圈内移动缓慢衰减（`0.35/s`）、出圈缓衰（`0.6/s`）；在 `Fountain.cs` 为 `FountainKind.Life` 应用 `1.15` 半径倍率（若预制体已有 `CircleCollider2D` 确保不重复乘算）；清理 `Fountain.cs` 冗长注释至预算内（或记录更新 `comment-budget.json`）。
   - 验证：`生命神泉Tests.cs` 覆盖静止加速（1.5–1.8s）、停走拉扯（2.0–2.5s）、离圈缓衰及满血不触发；`泉眼发光Tests.cs` 通过。
5. **光脉冲增强（`TileGridLightmap.cs` + `VisualFeelTuning.asset`）**：
   - 动作：放宽 `TileGridLightmap.impulseDecayRate` `[Range(1f, 30f)]` 并将默认率设为 `4.5f`（并核查场景/预制体覆盖值）；更新 `VisualFeelTuning.asset` 的 `arcImpulseRadius`（`0 -> 1.5`）与强度。
   - 验证：`TileGridLightmapTests.cs`、`LightingVisualCorrectionTests.cs` 通过，脉冲余晖时长明显大于 0.4s。
6. **配表修改与 Luban 导表（4 张 `.xlsx` + `EnemyTuning.asset`）**：
   - 前置：再次核对 `ConfigWorkspace/AGENTS.md`（禁用 `openpyxl` 重建工作表）。
   - 动作：安全修改 `enemy.xlsx`、`tile_state.xlsx`、`element_rule.xlsx`、`element_duo_reaction.xlsx` 中 §5.5 指定列与行；更新 `EnemyTuning.asset` 的 `cornerSpeedFactor`；运行 Luban 严格校验并生成至 `ConfigWorkspace/output/`，成功后镜像到 `Assets/Scripts/Generated/Config/` 与 `Assets/StreamingAssets/Luban/`。
   - 验证：运行 `pwsh Tools/luban-mirror.ps1`（待删/待覆盖/孤儿 `.meta` 均为 0）及 `Data层Tests.cs`、`状态工厂Tests.cs`、`ReactionResolverTests.cs`、`地块效果Tests.cs`、`地块优先级Tests.cs`。
7. **全量门禁与回归测试**：
   - 运行 `pwsh Tools/compile-gate.ps1 -Topology`、`pwsh Tools/comment-lint.ps1`（核对本批文件及新增测试零超标）、EditMode Test Runner 全跑并保存证据。

## 8. Engineering Rules
1. 保持 0 GC：新增 `Physics2D.OverlapBoxNonAlloc`、邻居扫描与 `NavField` 循环不得分配任何临时集合或 lambda 闭包。
2. 严守 `DeepseaOil.Logic` 纯净度：`NavField.cs`、`LifeFountainState.cs` 不得引入场景对象或 `Time.*`。
3. 严禁修改、删除或弱化既有测试断言迎合实现；若因数值调表（如测试写死了 `7.2` 等旧常数）产生变动，须以读表取代硬编码并说明。
4. 不使用 `openpyxl` 重建 `.xlsx` 表结构，保持所有中文别名与数据验证下拉框完整；导表必经 `output/` 暂存校验。
5. 注释严格遵守 `AGENTS.md` 第 7 节（9 类有效信息、单行或配平 `<summary>/<remarks>`）。

## 9. Acceptance Criteria
- `AC-01`: `pwsh Tools/compile-gate.ps1 -Topology` 输出 `0 Error(s)`、`0 Warning(s)` 且拓扑守卫全部通过。（证据：CLI 输出日志 `AI_Context/EVIDENCE/GD-2026-0001/`）
- `AC-02`: `pwsh Tools/luban-mirror.ps1` 待删除、待覆盖、孤儿 `.meta` 均为 `0`；4 张更新 `.xlsx` 在 Excel/校验器下别名与子表头无损。（证据：导表与镜像 CLI 日志）
- `AC-03`: Bug 1 验证：`SceneObstacleBaker.Bake` 不将初始 `TerracottaBrick` 写进 `NavMap`，`NavField` 动态阻挡并在砖消散后恢复通行，相关 EditMode 用例通过。（证据：`场景碰撞体烘焙Tests`、`寻路Tests` 结果）
- `AC-04`: 陶砖防卡怪：敌人站在即将生成 `TerracottaBrick` 的格上时，变砖后立即被挤推至可走邻格，不卡死且可继续寻路/受击。（证据：新增/回归测试与 `Range.unity` Play 验证录屏/截图）
- `AC-05`: 生命神泉：静止空蓄在 1.5–1.8s 内完成 1 次治疗；圈内停走移动缓慢衰减（不归零）并在合理拉扯下蓄满；生命泉触发半径为原半径的 1.15 倍。（证据：`生命神泉Tests` 结果 + 实机测试记录）
- `AC-06`: 冰刺与优先级：`霜冻冰刺` `touch_stun == 0`、`slow_rate` 为 `0.35~0.4`；`普通泥浆` priority 高于 `基础水/土` 且不覆盖永久地形；`状态工厂Tests` 与 `地块优先级Tests` 全绿。（证据：`dso_tbtilestate.json` Diff + EditMode 测试）
- `AC-07`: 敌人速度与范围：`普通怪` < `精英怪` < `玩家(8.0)`，`cornerSpeedFactor` 提升，一二级反应半径按 §5.5 扩大且 `陶砖` 保持十字；`ReactionResolverTests`、`地块效果Tests` 全绿。（证据：JSON Diff + EditMode 测试）
- `AC-08`: 光脉冲增强：`TileGridLightmap` 脉冲衰减耗时从 ~0.18s 明显拉长（≥ 0.45s），电弧产生可见脉冲半径。（证据：`TileGridLightmapTests` + `Range.unity` 截图/对比）
- `AC-09`: 注释门检查：本批全部改动与新增文件满足 `Tools/comment-lint.ps1` 预算；若既定红基线仅剩未修改的 3 个历史文件（`TimerWheel.cs`、`AudioManager.cs`、`SaveSlot.cs`），需明确记录 `Fountain.cs` 已转绿或按预算对齐。（证据：`comment-lint.ps1` 输出）
- `AC-10`: 全量 Diff 文件路径 100% 落在第 6 章白名单内，Batch 2/3 独占文件零触碰。（证据：`git diff --name-only`）

## 10. Test Plan
> 依据 `AI_Context/BUILD_AND_TEST.md` 登记入口；无统一 CLI EditMode 测试入口，测试由 Unity GUI 运行并记录截图/日志，未执行项标 `NOT RUN`。

1. **V1 — 静态与注释门**（工作目录：工程根）：
   - `pwsh Tools/comment-lint.ps1` -> 检查本批改动文件（含 `Fountain.cs`）与新增测试文件无报错/超限；记录既有 3 份未改动红文件基线状态。
2. **V2 — 编译与拓扑门**（工作目录：工程根）：
   - `pwsh Tools/compile-gate.ps1 -Topology` -> 期望 `0 Error(s)`、`0 Warning(s)`、四类守卫全部通过。
3. **导表与镜像门**（工作目录：工程根 / `ConfigWorkspace`）：
   - Unity `Luban ▸ 表格数据导入` 或 `ConfigWorkspace/AGENTS.md` 所列 Luban `--strict` 命令。
   - `pwsh Tools/luban-mirror.ps1` -> 期望待删除 `0`、待覆盖 `0`、孤儿 `.meta` `0`。
4. **V3 — EditMode 自动测试**（Unity Editor GUI）：
   - 打开 `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`（重点复核：`表清单Tests`、`Data层Tests`、`状态工厂Tests`、`ReactionResolverTests`、`地块优先级Tests`、`地块效果Tests`、`场景碰撞体烘焙Tests`、`寻路Tests`、`网格连锁Tests`、`敌人预制体Tests`、`TileGridLightmapTests`、`泉眼发光Tests`、`LightingVisualCorrectionTests` 及新增 `生命神泉Tests`）。
   - 期望：全部通过（0 failed）；若受环境限制未跑写 `NOT RUN`。
5. **V4 — 实机场景冒烟验收**（Unity Play Mode）：
   - `Assets/Scenes/Range.unity`：
     - 验临时砖生成/过期后敌人寻路恢复；
     - 验站位生成陶砖时敌人瞬间挤出不卡死；
     - 验冰刺只大幅减速不麻痹、电弧与蒸汽光脉冲余晖时长；
     - 验生命泉 1.5–1.8s 静止回血、走停不归零与 +15% 圈边界。
   - `Assets/Scenes/Levels/Level_1.unity`、`Level_4.unity`、`Level_5.unity`：抽查怪速体感、十字铺地防泥浆覆盖与战斗节奏。

## 11. Risks
1. **R2 — `寻路Tests.拐角前按刹车距离降速` 硬边界风险**：提高 `cornerSpeedFactor` 后，若测试夹具 `RowFactory.EnemyRow().MaxSpeed` 仍使用旧值 `3.5`，`expected` 可能高于 `MaxSpeed` 触发测试失败。*缓解*：Step 1 先读 `测试夹具.cs`，按真实单测公式校验并选择安全值或协同评估测试夹具。
2. **R2 — `ConfigWorkspace/Data/*.xlsx` 表结构损坏风险**：用错脚本库（如 `openpyxl` 重建表）会毁坏嵌套子表头与下拉框。*缓解*：严格只做单元格读写，导表后跑 `Luban --strict` 与 `luban-mirror.ps1` 双重校验。
3. **R1 — 陶砖挤出边缘穿透静态墙体**：若敌人四周被静态墙/连续砖块死围导致 8 邻格全不可走，强制位移可能穿透邻墙。*缓解*：邻格必须同时校验 `HasCell && !IsObstacle && IsWalkable`，全不可走时退回原地不穿墙（仅保留伤害/后续处理）。
4. **R1 — `Fountain_Life` 预制体或场景覆写不一致**：若某关卡覆盖了生命泉 `CircleCollider2D.radius` 或 `TileGridLightmap.impulseDecayRate` 值，脚本默认值可能被场景序列化遮盖。*缓解*：Step 1 检索场景 YAML 序列化覆写，采用运行时乘法倍率（对生命泉）并同步检查场景覆写。
5. **R1 — 多 Worktree 并行合入冲突**：若批 1 意外触碰 `CombatRoot.cs` 或批 2 触碰 `Fountain.cs`，合入必然冲突。*缓解*：严格锁死第 6 章 `FORBIDDEN` 跨批隔离区。

## 12. Rollback
1. **代码与 SO 回滚**：在独立 Worktree 分支 `ai/GD-2026-0001` 施工，若方案失败或验证不通过，主分支 `VReason/Update`（`df60015`）完全不受影响；Worktree 内可通过 `git restore` 指定文件或废弃该 Worktree 回滚，**严禁覆盖用户既有工作区文件（如 `PROJECT_BOARD.md`、`Docs/`）**。
2. **配表与生成物回滚**：若 Luban 导表异常，同时回滚 `ConfigWorkspace/Data/*.xlsx`、`Assets/Scripts/Generated/Config/` 与 `Assets/StreamingAssets/Luban/` 至 Base Commit `df60015`，再重跑 `pwsh Tools/luban-mirror.ps1` 确认镜像一致。

## 13. Open Questions
> 非阻塞，Worker Step 1 必查验证项。
1. `Assets/Tests/Runtime/Editor/测试夹具.cs` 中 `RowFactory.EnemyRow().MaxSpeed` 是 `3.5` 还是 `7.2`？（决定 `EnemyTuning.cornerSpeedFactor = 0.68` 是否触碰 `寻路Tests:638` 断言。）
2. `Assets/Scenes/Levels/Level_*.unity` 与 `Range.unity` 中，生命泉预制体路径是什么、`CircleCollider2D.radius` 原值为何，以及 `TileGridLightmap.impulseDecayRate` 在场景中是否已显式序列化为 `14`？
3. 本批仅解决“陶砖生成时把怪安全挤出”；水/土能否额外砸碎陶砖或对其旁敌人溅射留作后续观察，不扩大本批范围。

## 14. Approval
*(由 CEO 审阅并填写至 `APPROVAL_RECORD.md`，Planner 不得预先代填。)*
- 批准 Plan 版本：
- 批准人 / 时间：
- 专项越权授权确认（`ConfigWorkspace/Data/*.xlsx`、`*.asset`、`*.prefab`、`Tools/comment-budget.json`、Worktree 并行）：
- 附加条件或拒绝项：
--- END OF TASK ---
