--- START OF TASK ---
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-2026-0002
- Plan Version: v01
- Status: APPROVED
- Task Level: L2 / Risk Level: R2
- Project / Engine / Platform: 荒原复苏指南 (deepseaoil) / Unity 2022.3.62f3c1 (URP 14.0.12, 2D Renderer) / Windows Standalone & Unity Editor
- Branch / Source Base Commit: `ai/GD-2026-0002` / `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`（施工 Worktree 从批准提交后的 `VReason/Update` 分出；批准提交仅增加任务文档，不改变源码基线）
- Planner: Planner Chat（`planner-orchestrator` + `game-systems-designer` + `unity2d-architect` + `task-brief-writer`）
- Worker: 独立 Worker 会话（Agent 模式，禁止触碰批 1/批 3 独占文件）
- Watchdog: 独立 Watchdog 会话（`$watchdog-inspector`，不得与 Worker 共用会话）
- Context: `AI_Context/TASKS/GD-2026-0002/00_CONTEXT_MANIFEST.md`（待 Secretary 登记生成）+ Base Commit `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`
- Execution Attempt Cap: A01–A03
- Created At: 2026-11-25

## 1. Objective
完成玩家生存手感升级与资源丢失 Bug 修复（批 2 / 3），实现以下可观察且可验证的结果：
1. 实现玩家受击后“幽灵化”无敌穿怪与闪烁：在 `InvulnerableDuration`（当前配置 `0.8s`）期间，玩家自身碰撞体暂不与敌人层发生物理阻挡（可自由穿出怪堆脱困），并伴随清晰可见的周期闪烁表现；死亡、重生或退出场景时洁净复位，绝不泄漏到普通墙体或敌怪之间。
2. 修复玩家死亡后无阻尼滑出屏幕缺陷：玩家致死后不再因 `PlayerController.FixedTick` 停算而带着既有速度无摩擦滑出地图，而是平滑快速刹停（或立即停住）。
3. 修复 Bug 2（波前配给种子覆盖既有种子）：当玩家手中已持有种子时，波次 `Prep` 阶段的战备配给不再调用 `TryDiscardSeed` 销毁原有种子，而是将新配给种子作为掉落物投落在玩家脚边，保留现有单槽拾取护栏与自主选择权。

## 2. Non-goals
1. 本批**不修改**任何批 1 文件（`SceneObstacleBaker.cs`、`NavField.cs`、`TileObstacleDirector.cs`、`TilemapAdapter.cs`、`LifeFountainState.cs`、`Fountain.cs`、`TileGridLightmap.cs`、`EnemyTuning.asset`、`VisualFeelTuning.asset`、`ConfigWorkspace/Data/*.xlsx` 与 Luban 生成目录）。
2. 本批**不修改**任何批 3 文件（`Assets/Scripts/Presentation/UI/**`、`StoryFlow.cs`、`AudioManager.cs`、`SfxPlayback.cs`、`slot_dialogue.xlsx`）。
3. **不做**“无敌结束帧若与怪重合时的防叠弹开兜底”（CEO 已明确确认无需复杂兜底；且 `PlayerStats.CanTakeDamage` 自然防并发重入）；**不修改** `PlayerConfig.asset` 与 `player.xlsx` 里的 `invulnerable_duration`（沿用现有配表值 `0.8s`）。
4. **不顺手修改** `CombatGroup.cs` 的瞄准高亮判据，**不新增** `.asmdef`、依赖包或修改 `ProjectSettings/`。
5. **不清理**既有红基线注释文件（`TimerWheel.cs`、`AudioManager.cs`、`SaveSlot.cs`、`Fountain.cs`）。

## 3. Current State
- `[VERIFIED]` **无敌帧只拦扣血不穿怪**：`Assets/Scripts/Logic/Player/PlayerStats.cs:21,60-72` 在受击扣血时写入 `_invulnerableUntil = now + _spec.InvulnerableDuration`（`Assets/StreamingAssets/Luban/dso_tbplayer.json` 中为 `0.8s`），`ResetToFull()` 与 `ImportSave()` 将其复位为 `float.NegativeInfinity`。注意：**致死一击也会写入 `_invulnerableUntil`**（`_current` 归 0 后仍执行赋值）。目前 `PlayerStats` 未暴露 `IsInvulnerable(now)` 只读查询接口。
- `[VERIFIED]` **物理层碰撞设置**：`Assets/Scripts/Presentation/Root/CombatRoot.cs:49,544` 仅声明 `EnemyLayer = 8` 并调用 `Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true)`（只关闭怪与怪碰撞），玩家与敌人仍发生刚体物理挤撞。注意：**`Player.prefab` 与静态障碍/`TileObstacleDirector` 的层号是否同为 `Default(0)` 当前 `[UNKNOWN]`**——若玩家在 `Default` 层，直接全局调用 `Physics2D.IgnoreLayerCollision(playerLayer, EnemyLayer, true)` 会导致**全图所有敌人穿透墙壁与陶砖**！
- `[VERIFIED]` **Unity 2022.3 API 可用性**：本工程使用 `Unity 2022.3.62f3c1`，`Collider2D.excludeLayers`（逐碰撞体层过滤）自 Unity 2022.2 起原生可用，仅屏蔽指定碰撞体自身对目标层的物理接触，不影响其他同层静态墙体。
- `[VERIFIED]` **玩家死后滑行根因**：`Assets/Scripts/Presentation/Adapters/ActorMotor.cs` 注释与实现均不设物理阻尼（`Rigidbody2D` 靠逻辑账本 `BrakeTowards` 减速）；`Assets/Scripts/Presentation/Root/PlayerController.cs:201-204` 在 `FixedTick` 开头写 `if (!Logic.IsAlive) return;`，且 `OnDeathTriggered()`（第 111 行）仅调用 `_animView?.TriggerDie()`，未像 `EnemyActor.Die()` 那样清零/刹停刚体速度，导致死亡瞬间带着既有移动/冲刺速度无阻尼滑行。
- `[VERIFIED]` **Bug 2（配给覆盖旧种）**：`Assets/Scripts/Presentation/Root/CombatRoot.cs:718-731` 的 `GrantWaveSeed()` 在消耗 `WaveLogic.ConsumeSeedGrant()` 后，无条件先执行 `stats.TryDiscardSeed(out _, out _)` 再执行 `stats.GrantSeed(seed)`，强行销毁玩家既有的种子。
- `[VERIFIED]` **掉落物单槽护栏已存在**：`Assets/Scripts/Presentation/Drop/WaterBallDrop.cs:105` 的 `IsPickupBlocked` 已检查 `!logic.Stats.CanPickupSeed()`；因此将新配给种子生成为地面掉落物时，若玩家满槽不会误吸入、也不会覆盖旧种，且能由 `DropDirector.ExportDrops` 正常进出存档。
- `[UNKNOWN]` `Assets/Resources/scane_prebs/Player.prefab` 挂载的碰撞体数量、`isTrigger` 分布及 `SpriteRenderer` 受击驱动细节（`ActorTintDriver.cs` 是否会与直接写 `SpriteRenderer.color` 冲突），需 Worker 在 Step 1 实读确认。

## 4. Constraints
1. **程序集与分层边界**：`PlayerStats.cs` 位于 `DeepseaOil.Logic`，严禁引入 `UnityEngine.Collider2D`、`Time.*` 或场景对象引用；物理层过滤与闪烁表现必须落在 `DeepseaOil.Presentation`（`PlayerController.cs`）。
2. **绝不污染全局碰撞矩阵**：严禁在玩家可能处于 `Default(0)` 层的前提下使用 `Physics2D.IgnoreLayerCollision` 全局屏蔽玩家所在层与 `EnemyLayer`；必须使用玩家自身碰撞体的 `Collider2D.excludeLayers`（保留预制体既有 `_baseExcludeLayers` 掩码）。
3. **热路径 0 GC**：`PlayerController.FixedTick` 与 `RenderTick` 严禁每帧 `GetComponents`、LINQ、闭包或新集合分配；碰撞体数组须在 `Awake` 缓存一次。
4. **生命周期与复位安全**：任何在受击期间改写的 `excludeLayers` 与渲染可见性/透明度，必须在无敌结束、`OnDeathTriggered`、`RespawnToSpawn` 与 `OnDestroy` 全部可靠还原。
5. **注释预算守卫**：本批修改的 `PlayerStats.cs`（上限 234）、`PlayerController.cs`（上限 1394）、`CombatRoot.cs`（上限 1830）均为既有绿线/已封印文件，新增注释须压在原预算内（不额外拉高预算，不触碰红基线文件）；新增测试文件若独立建档须登记进 `Tools/comment-budget.json`。
6. **Worktree 并行边界**：严禁触碰第 6 章 `FORBIDDEN` 所列批 1/批 3 独占文件。

## 5. Proposed Design

### 5.1 逻辑层无敌只读查询（`PlayerStats.cs`）
- 在 `Assets/Scripts/Logic/Player/PlayerStats.cs` 新增纯查询接口：
  - `public bool IsInvulnerable(float now) => IsAlive && !CanTakeDamage(now, _invulnerableUntil);`
  - 语义：`IsAlive` 为真且 `now < _invulnerableUntil` 时返回 true；致死一击（`!IsAlive`）、`NaN` 或已过无敌时刻一律返回 false。

### 5.2 受击穿怪幽灵化 + 闪烁 + 死亡刹停（`PlayerController.cs`）
1. **受击幽灵化（物理穿怪）**：
   - 常量 `private const int EnemyLayerMask = 1 << 8;`（与 `CombatRoot.EnemyLayer = 8` 保持同一层号，须在 Step 1 核对 `ProjectSettings/TagManager.asset` 与 `Enemy_1.prefab`）。
   - 在 `Awake()` 一次性缓存非 Trigger 的玩家碰撞体及各自初始 `excludeLayers`（使用成员结构体或平铺数组 `_solidColliders` + `_baseExcludeLayers`，0 GC）：
     - 只收集 `!col.isTrigger` 的 `Collider2D`（避免影响拾取/泉眼触发判定，虽然它们也不在层 8）。
   - 在 `FixedTick`（以及状态复位路径）同步幽灵状态 `bool ghosting = Logic.Stats.IsInvulnerable(Time.fixedTime);`：
     - 仅当 `ghosting != _isGhosting` 状态翻转时写入各碰撞体的 `excludeLayers`（开启时 `baseMask | EnemyLayerMask`，关闭时还原为 `baseMask`），不在未变化帧重复赋值。
2. **受击闪烁表现（不与 `EffectId.Flash` 打架）**：
   - 在 `Step 1` 阅读 `ActorTintDriver.cs` 与 `ActorAnimationView.cs` 核实 `EffectId.Flash` 行为（通常在受击后前 `0.12s` 独占材质闪白/着色）。
   - 在 `RenderTick` 中：
     - 若 `_isGhosting` 为真，使用周期频率（如 `14f` Hz，`Mathf.FloorToInt((Time.time - _ghostStartTime) * FlickerHz) % 2 == 1` 或在 `0.12s` Flash 结束后切亮灭/半透交替，具体依据 `ActorTintDriver` 查证结果优先选用切换 `_animView.Renderer.enabled` 开关或安全 Alpha 脉冲，推荐在 `0.12s` 闪白之后用 `enabled` 或材质安全 alpha 交替，确保视觉极清晰且不与 `Flash` 的 `Color.white` 互相覆盖）；
     - `_isGhosting` 为假时立即确保渲染器恢复可见与原色。
3. **死亡刹停（修死后滑出屏幕）**：
   - 在 `OnDeathTriggered()` 中立即调用 `SetGhosting(false)` 与 `SetFlicker(false)`，并执行停步/刹车初始化（`motor?.StopMove()` 或在 `FixedTick` `!Logic.IsAlive` 分支继续驱动短促刹车直到停稳）：
     - 推荐方案：在 `OnDeathTriggered()` 先清除外力/滑行速度（或由 `FixedTick` 在 `!Logic.IsAlive` 时若 `motor.EngineVelocity.sqrMagnitude > 0.01f` 调用 `motor.BeginStep(Time.fixedTime, deltaTime); motor.BrakeTowards(); motor.Commit();`，否则 `motor.StopMove()`），`PlayerConfig` `moveAcceleration = 60` 下满速 `8` 会在 `0.13s` 内自然稳停，既无“瞬定木桩”僵硬感，也彻底消灭无限滑出屏幕现象。
   - 在 `RespawnToSpawn()` 与 `OnDestroy()` 均显式调用 `SetGhosting(false)` 与 `SetFlicker(false)`，保证状态零残留。

### 5.3 Bug 2：波前配给种子不覆盖现有种子（`CombatRoot.cs`）
- 修改 `Assets/Scripts/Presentation/Root/CombatRoot.cs` 的 `GrantWaveSeed()`：
  ```csharp
  private void GrantWaveSeed()
  {
      if (_levelLogic == null || player == null || player.Logic == null) return;

      SeedType seed = _levelLogic.Waves.ConsumeSeedGrant();

      if (seed == SeedType.None) return;

      PlayerStats stats = player.Logic.Stats;

      if (stats.CanPickupSeed())
      {
          stats.GrantSeed(seed);
          return;
      }

      DropSupplySeedAtPlayerFeet(seed);
  }
  ```
- `DropSupplySeedAtPlayerFeet(SeedType seed)` 实现细节：
  - 若 `_drops == null`，记录一次 Warning 并直接返回（不销毁旧种）。
  - 计算落点：`origin = player.Position`，取玩家朝向偏移 `0.55f`（朝向为零取 `Vector2.right`），再经 `player.World.Bounds.TryClamp(landing, out Vector2 clamped)` 钳回地图合法范围内（避免靠墙时把补给丢出边界）。
  - 调用 `_drops.TrySpawn(new DropSpawnRequest(DropType.Seed, origin, clamped, seed))`；若生成成功可设置较短拾取冷却（如 `0.4f` 秒，保证弹落弧线清晰可见），且 `WaterBallDrop.IsPickupBlocked` 会自动阻止在玩家满槽时吸走该掉落物，待玩家消耗完或按 `Q` 丢弃现有种子后即可随时拾取。
- 在 `CombatRoot.OnDisable` / `ClearAll` 保持与现有 `Drops` 清理一致，无需额外跨系统事件变更（`WavePhaseChanged` 本身已携带 `GrantSeed`，供批 3 HUD 提示订阅消费）。

## 6. File Scope

### READ
- `AGENTS.md`、`AI_Context/BUILD_AND_TEST.md`
- `Assets/Scripts/Logic/Player/PlayerLogic.cs`
- `Assets/Scripts/Presentation/Adapters/ActorMotor.cs`、`Assets/Scripts/Presentation/Adapters/PlayerMotor.cs`
- `Assets/Scripts/Presentation/Drop/DropDirector.cs`、`Assets/Scripts/Presentation/Drop/DropActor.cs`、`Assets/Scripts/Presentation/Drop/WaterBallDrop.cs`
- `Assets/Scripts/Presentation/Visual/ActorAnimationView.cs`、`Assets/Scripts/Presentation/Effects/Drivers/ActorTintDriver.cs`、`Assets/Scripts/Presentation/Effects/EffectId.cs`
- `Assets/Resources/config/PlayerConfig.asset`、`Assets/StreamingAssets/Luban/dso_tbplayer.json`
- `Assets/Resources/scane_prebs/Player.prefab`、`ProjectSettings/TagManager.asset`（仅核实玩家碰撞体层与 Enemy 层定义）
- `Assets/Tests/Runtime/Editor/测试夹具.cs`、`Assets/Tests/Runtime/Editor/移动Tests.cs`、`Assets/Tests/Runtime/Editor/WaveLogicTests.cs`、`Assets/Tests/Runtime/Editor/HUD受击反馈Tests.cs`

### CREATE
- `Assets/Tests/Runtime/Editor/玩家受击与配给Tests.cs`（覆盖 `PlayerStats.IsInvulnerable` 边界、幽灵化物理 `excludeLayers` 开关及不穿透墙体、`!IsAlive` 死亡刹停、满槽配给掉脚边与空槽直接配给用例；须同步登记预算）

### MODIFY
- `Assets/Scripts/Logic/Player/PlayerStats.cs`
- `Assets/Scripts/Presentation/Root/PlayerController.cs`
- `Assets/Scripts/Presentation/Root/CombatRoot.cs`
- `Tools/comment-budget.json` *(仅追加注册新测试文件 `玩家受击与配给Tests.cs`，不得更改其他批或既定文件额度；与批 1 合入时需合并该 JSON 键值对)*

### FORBIDDEN（与批 1/批 3 并行冲突防线）
- **严禁修改**：批 1 独占文件（`SceneObstacleBaker.cs`、`NavField.cs`、`TileObstacleDirector.cs`、`TilemapAdapter.cs`、`LifeFountainState.cs`、`Fountain.cs`、`TileGridLightmap.cs`、`EnemyTuning.asset`、`VisualFeelTuning.asset`、`ConfigWorkspace/Data/**`、Luban 生成区 `Generated/Config/**`、`StreamingAssets/Luban/**`）
- **严禁修改**：批 3 独占文件（`Assets/Scripts/Presentation/UI/**`、`Assets/Scripts/Presentation/Story/**`、`Assets/Scripts/Presentation/Audio/**`）
- **严禁修改**：`TimerWheel.cs`、`SaveSlot.cs`、`InputSys.cs`、`Packages/manifest.json`、`ProjectSettings/**`、`Player.prefab`、`PlayerConfig.asset`

## 7. Execution Steps
1. **Preflight & 只读核查**：
   - 检查 `git status --short --branch` 与独立 Worktree `ai/GD-2026-0002` 基线是否为 `df60015`。
   - 读取 `Player.prefab`、`TagManager.asset`、`ActorTintDriver.cs`、`ActorAnimationView.cs` 与 `测试夹具.cs`：确认 `EnemyLayer = 8`、`Player` 层号、玩家碰撞体是否多于 1 个，以及 `EffectId.Flash` 对 `SpriteRenderer` 颜色/材质的修改方式。
2. **扩展 `PlayerStats.IsInvulnerable`（`PlayerStats.cs`）**：
   - 实现 `IsInvulnerable(float now)`（含 `IsAlive` 守卫与 `CanTakeDamage` 一致口径）。
   - 验证：保持注释字符数 `<= 234`。
3. **实现受击幽灵化、闪烁与死亡刹停（`PlayerController.cs`）**：
   - 前置：Step 1 & 2 完成。
   - 动作：在 `Awake` 缓存非触发 `Collider2D` 与基础 `excludeLayers`；在 `FixedTick` 按 `IsInvulnerable(Time.fixedTime)` 开关 `EnemyLayerMask`；在 `RenderTick` 驱动闪烁（避开或衔接前 `0.12s` `Flash`）并在结束/死亡/重生/销毁时复位；在 `OnDeathTriggered` 与 `!Logic.IsAlive` 的 `FixedTick` 分支执行快速刹停。
   - 验证：保持注释字符数 `<= 1394`，热路径无任何堆分配。
4. **修复 Bug 2（`CombatRoot.cs`）**：
   - 动作：更新 `GrantWaveSeed()`，空槽直接 `GrantSeed(seed)`，满槽调用 `DropSupplySeedAtPlayerFeet(seed)`（经 `player.World.Bounds` 钳位并交由 `_drops.TrySpawn` 生成带短冷却的 `DropType.Seed` 掉落物）。
   - 验证：保持注释字符数 `<= 1830`。
5. **编写 EditMode 回归测试并登记预算**：
   - 在 `Assets/Tests/Runtime/Editor/玩家受击与配给Tests.cs` 覆盖逻辑与物理边界用例，并向 `Tools/comment-budget.json` 新增该测试文件单条登记。
6. **运行静态与编译门禁 + 测试与 Play Mode 验证**：
   - 执行 `pwsh Tools/compile-gate.ps1 -Topology`、`pwsh Tools/comment-lint.ps1`、Unity Test Runner EditMode 全跑，以及 `Range.unity` / `Level_3.unity` Play Mode 实测受击穿怪、死亡停住、满槽波前配给。

## 8. Engineering Rules
1. 热路径（`PlayerController.FixedTick` / `RenderTick`）0 GC：禁止每帧 `GetComponent`、数组新建、LINQ 与匿名 lambda。
2. 严禁修改玩家碰撞体的触发器行为（不得错误过滤泉眼/掉落物 Trigger），严禁使用会殃及同层墙体的全局 `IgnoreLayerCollision`。
3. 严禁删除或弱化既有测试断言；对本批修改的既有代码文件保持注释预算严格不涨。
4. 若实施中发现必须触碰 `Player.prefab` 或 `CombatGroup.cs` 等超出第 6 章范围的文件，必须立即停止并报告 Planner 升版重审。

## 9. Acceptance Criteria
- `AC-01`: `pwsh Tools/compile-gate.ps1 -Topology` 输出 `0 Error(s)`、`0 Warning(s)`，拓扑守卫全部通过。（证据：CLI 日志 `AI_Context/EVIDENCE/GD-2026-0002/`）
- `AC-02`: 无敌穿怪验证：玩家受击扣血进入 `InvulnerableDuration` 期间，玩家碰撞体对 `EnemyLayer(8)` 物理排斥失效（可穿出怪群），同时仍正常受静态墙体与陶砖阻挡；无敌期结束、死亡或重生后排斥恢复。（证据：`玩家受击与配给Tests` + `Range.unity` Play 录屏/截图）
- `AC-03`: 死亡无敌边界：`PlayerStats.IsInvulnerable(now)` 在致死一击后返回 `false`，死亡角色不开启幽灵闪烁。（证据：EditMode 单元测试）
- `AC-04`: 闪烁反馈与复位：玩家受击存活后在无敌窗口内呈现可见闪烁，且结束后、重生后渲染器可见性与颜色完全恢复正常。（证据：Play Mode 录屏/截图）
- `AC-05`: 死亡刹停：玩家满速移动或冲刺时被击杀，死亡后快速刹停在原地附近，不再出现无阻尼持续滑出屏幕现象。（证据：`玩家受击与配给Tests` + `Range.unity` Play 录屏）
- `AC-06`: Bug 2 配给保护：玩家已有种子时，波前 `Prep` 触发 `GrantWaveSeed` 不销毁旧种，新配给种子作为 `DropType.Seed` 掉落在玩家脚边合法边界内，空槽或按 `Q` 丢弃后可正常拾取；玩家空槽时仍直接发放入槽。（证据：`玩家受击与配给Tests` + `Level_3.unity` 实机截图）
- `AC-07`: 注释预算守卫：`PlayerStats.cs`、`PlayerController.cs`、`CombatRoot.cs` 与新测试文件均在 `Tools/comment-budget.json` 预算内（只记既有基线未修改红文件差异）。（证据：`pwsh Tools/comment-lint.ps1` 输出）
- `AC-08`: 改动路径 100% 落在第 6 章 `MODIFY/CREATE` 内，批 1/批 3 独占文件零改动。（证据：`git diff --name-only`）

## 10. Test Plan
> 依据 `AI_Context/BUILD_AND_TEST.md`；无统一 CLI EditMode 测试入口，测试于 Unity Test Runner GUI 运行并截图/留档，未运行标 `NOT RUN`。

1. **V1 — 静态与注释门**（工作目录：工程根）：
   - `pwsh Tools/comment-lint.ps1` -> 核对本批改动文件（`PlayerStats.cs`、`PlayerController.cs`、`CombatRoot.cs`）及新增测试文件全绿无新增违规。
2. **V2 — 编译与拓扑门**（工作目录：工程根）：
   - `pwsh Tools/compile-gate.ps1 -Topology` -> `0 Error(s)`、`0 Warning(s)`、四类守卫全部通过。
3. **V3 — EditMode 自动测试**（Unity Editor GUI）：
   - `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`（重点关注：`玩家受击与配给Tests`、`移动Tests`、`WaveLogicTests`、`HUD受击反馈Tests`、`存档Tests`、`Data层Tests`）。
4. **V4 — 实机场景冒烟验收**（Unity Play Mode）：
   - `Assets/Scenes/Range.unity`：
     - 被多只 Chase 敌人围住受击：验证受击后可穿过怪群脱困且屏幕可见闪烁、仍撞墙/撞陶砖、无敌结束恢复碰撞；
     - 移动/冲刺中被最后 1 血击杀：验证角色很快刹停在现场而不无限滑出屏幕；
     - 在场景拾取或持有一颗种子后（或切换至 `Level_3.unity`），推进至带配给波次 `Prep`：验证手中原种子保留，新配给掉落在脚边且丢弃旧种后可捡起。

## 11. Risks
1. **R2 — 物理层掩码误伤墙体**：误用全局 `IgnoreLayerCollision` 且 `Player` 在 `Default` 层会让全图敌人穿墙。*缓解*：强制采用逐碰撞体 `Collider2D.excludeLayers` 仅排除 `EnemyLayerMask`，并用测试同时验证“穿怪 + 不穿静态墙”。
2. **R1 — `ActorTintDriver`（Flash）与闪烁效果互相覆盖**：受击前 `0.12s` `EffectId.Flash` 可能改写材质或 `SpriteRenderer.color`。*缓解*：Step 1 先读 `ActorTintDriver.cs` 确认其写入属性；闪烁优先使用 `Renderer.enabled` 开关脉冲或等待 `flashDuration` 结束后再切换，并在结束时保底复位 `enabled = true`。
3. **R1 — `Level_3` 进波边界掉落卡墙外**：玩家贴地图边缘迎战波次切换时脚边生成掉落物可能落在 `Bounds` 外。*缓解*：`player.World.Bounds.TryClamp` 钳制落点坐标。
4. **R1 — Worktree 合入冲突**：本批需在 `Tools/comment-budget.json` 新增 1 条新测试文件登记，若批 1 同步修改该文件，合入时按两条不同键名手动/自动并入即可。*缓解*：严格只追加 `玩家受击与配给Tests.cs` 一行，不改其它条目；若想彻底零冲突，也可把新增单测直接追加进尚未被批 1 修改的既有文件 `移动Tests.cs`（其预算为 `3346`），从而完全不碰 `Tools/comment-budget.json`。

## 12. Rollback
1. 在独立 Worktree 分支 `ai/GD-2026-0002` 施工；主分支 `VReason/Update`（`df60015`）零受影响。
2. 如需回退，直接 `git restore` 修改的三份 C# 文件与移除新增测试文件，或直接销毁该 Worktree 分支；严禁覆盖工作区已有未提交文档。

## 13. Open Questions
> 非阻塞，Worker Step 1 必查验证项。
1. `Assets/Resources/scane_prebs/Player.prefab` 的 Layer 编号与 `Collider2D` 列表为何？（核实物理过滤对象列表与基线 `excludeLayers`。）
2. `Assets/Scripts/Presentation/Effects/Drivers/ActorTintDriver.cs` 是写 `SpriteRenderer.color` 还是 `MaterialPropertyBlock`？（决定无敌闪烁采用 `Renderer.enabled` 开关交替还是透明度交替。）
3. 若希望彻底消除与批 1 在 `Tools/comment-budget.json` 上哪怕 1 行的交集，CEO/Secretary 也可指示 Worker 直接将新增单测并入既有的 `Assets/Tests/Runtime/Editor/移动Tests.cs`（不碰 `comment-budget.json`）。

## 14. Approval
*(由 CEO 审阅并填写至 `APPROVAL_RECORD.md`，Planner 不得预先代填。)*
- 批准 Plan 版本：
- 批准人 / 时间：
- 专项授权确认（多 Git Worktree 并行、`Tools/comment-budget.json` 新增测试登记）：
- 附加条件或拒绝项：
--- END OF TASK ---
