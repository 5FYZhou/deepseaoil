using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Level;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Wave;
using DeepseaOil.Logic.World;
using DeepseaOil.Presentation.Actor;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Ball;
using DeepseaOil.Presentation.Drop;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.Level;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.RenderPass;
using DeepseaOil.Presentation.Visual;
using DeepseaOil.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using cfg.dso;

namespace DeepseaOil.Presentation
{
    /// <summary>战斗切片的组合根：装配一次，每帧驱动</summary>
    public sealed class CombatRoot : MonoBehaviour, ISceneRoot, IRenderTicked, IPhysicsTicked, IThrowSink
    {
        public int Order => SceneOrder.World;

        /// <summary>怪所在的 Unity 层号：与预制体根节点层、ProjectSettings 里的层名三处必须一致</summary>
        private const int EnemyLayer = 8;

        // Q 键丢弃后脚下掉落物的不可拾取窗口，秒
        private const float SeedDiscardPickupCooldown = 1.2f;

        [Header("必需接线")]
        [Tooltip("玩家组合根（场景里的 PlayerController）。")]
        [SerializeField] private PlayerController player = default;

        [Tooltip("格子视图（提供格子几何与地板）。")]
        [SerializeField] private TilemapAdapter gridView = default;

        [Tooltip("当前场景的关卡配置读取器。")]
        [SerializeField] private LevelReader levelReader = default;

        [Header("可选接线")]
        [Tooltip("球与瞄准件的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform ballRoot = default;

        [Tooltip("敌人的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform actorRoot = default;

        [Tooltip("场景里的泉眼。每帧由本类驱动（落物不是自驱的）。留空则只靠掉落物补给。")]
        [SerializeField] private Fountain[] fountains = new Fountain[0];

        [Tooltip("是否刷敌人。关掉可以只验投掷链路。")]
        [SerializeField] private bool enableLevel = true;

        [Tooltip("玩家死亡后是否自动回到出生点并清理当前战斗。")]
        [SerializeField] private bool enablePlayerRespawn = false;

        [Tooltip("算屏幕外出生点用的相机。留空则由 EnemyDirector 回落 Camera.main。")]
        [SerializeField] private Camera spawnCamera = default;

        private GameRoot _root;

        private GridLogic _grid; private EnemyCellRegistry _registry;

        private TileGridLightmap _tileLightmap;

        private BallDirector _balls;

        private ImpulseExecutor _impulses;

        private DropDirector _drops;

        private CombatDirector _combat;
        private LevelLogic _levelLogic;
        private TileHighlightView _highlight;

        private LifeFountainState _lifeFountain;

        private Fountain _lifeFountainSource;

        private readonly List<Vector3Int> _contactCells = new List<Vector3Int>(9);

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();

        private float _retryAt = float.PositiveInfinity;

        private bool _autoSaveRequested;

        private bool _restoring;

        /// 场景作用域注册点（非 DontDestroyOnLoad 单例）：供运行时拖入的木桩自我报到
        public static CombatRoot Current { get; private set; }

        public bool IsReady { get; private set; }

        public GridLogic Grid => _grid;

        public int CurrentLevelId => IsReady && _levelLogic != null ? _levelLogic.CurrentLevel.Id : 0;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        public void Attach()
        {
            if (IsReady) return;

            Assemble();
        }

        private void OnDestroy()
        {
            _highlight?.Detach();
            _tileLightmap?.Detach();
            gridView?.Detach();

            // 销毁期再问 GameRoot.Instance 会当场造一个新的，只能用 Start 里抓的引用
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        private void OnEnable()
        {
            Current = this;

            EventBus<DropCollected>.Subscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Subscribe(OnRequestHudRefresh);
            EventBus<GridBlastOccurred>.Subscribe(OnGridBlastOccurred);
            EventBus<GridPointVfxOccurred>.Subscribe(OnGridPointVfxOccurred);
            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
            EventBus<WavePhaseChanged>.Subscribe(OnWavePhaseChanged);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Current, this)) Current = null;

            EventBus<DropCollected>.Unsubscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Unsubscribe(OnRequestHudRefresh);
            EventBus<GridBlastOccurred>.Unsubscribe(OnGridBlastOccurred);
            EventBus<GridPointVfxOccurred>.Unsubscribe(OnGridPointVfxOccurred);
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
            EventBus<WavePhaseChanged>.Unsubscribe(OnWavePhaseChanged);
        }

        public void RenderTick(float deltaTime)
        {
            if (!IsReady) return;

            _grid.Tick(Time.time, deltaTime);

            if (player != null && player.Logic != null && player.Logic.IsAlive) _tileLightmap?.SetCarryLightPosition(player.Position);
            else _tileLightmap?.ClearCarryLight();

            _tileLightmap?.Tick(Time.time, deltaTime);

            _balls.Tick(deltaTime);

            if (player != null && player.Logic != null && !player.Logic.IsAlive) return;

            _drops.Tick(deltaTime);

            for (int i = 0; i < fountains.Length; i++)
            {
                Fountain fountain = fountains[i];

                if (fountain != null) fountain.Tick(deltaTime);
            }

            TickLifeFountain(deltaTime);

            TickSeedInput();

            UpdateSeedHighlight();

            if (!_autoSaveRequested) return;

            _autoSaveRequested = false;

            EventBus<RequestAutoSave>.Publish(new RequestAutoSave(CollectGameState(0, true)));
        }

        public void FixedTick(float deltaTime)
        {
            if (!IsReady) return;

            float now = Time.fixedTime;

            // ① 落地冲量：必须在物理帧施加，渲染帧施加会漂
            _impulses.FixedTick();

            // 导航流场先追玩家本帧所在格：必须早于敌人循环，否则怪读的是上一帧的场
            if (player != null && _grid != null)
            {
                _grid.RefreshNavFields(_grid.WorldToCell(player.Position), ConfigModule.NavSkin);
            }

            // ② 全场敌人先按本帧位置追一步，再让格子按新位置结算（顺序不能反）
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                EnemyActor enemy = _enemies[i];
                if (enemy == null)
                {
                    _enemies.RemoveAt(i);
                    continue;
                }
                if (enemy.IsAlive)
                {
                    enemy.FixedTick(now, deltaTime);
                }
            }

            if (_combat != null)
            {
                _combat.FixedTick(now, deltaTime);
            }

            // ③ 玩家受击：读物理体位置，须在敌人移动之后
            UpdatePlayerContact(now);
        }

        private void TickSeedInput()
        {
            InputProvider input = player != null ? player.Input : null;

            if (input == null) return;

            PlayerLogic logic = player.Logic;

            if (logic == null || !logic.IsAlive) return;

            if (input.PlantPressedThisFrame) TryThrowSeed(logic);

            if (input.DiscardJustPressed) TryDiscardSeed(logic);
        }

        // 拒收必须早于扣次数：CanPlant 为假时既不扣次数也不发射
        private void TryThrowSeed(PlayerLogic logic)
        {
            if (!logic.Combat.HasAim) return;

            SeedType seed = logic.Stats.Seed;

            if (logic.Stats.SeedCount <= 0 || seed == SeedType.None) return;

            Vector3Int cell = logic.Combat.AimCell;

            if (!SeedPlanter.CanPlant(_grid, cell, seed))
            {
                _root?.Audio.EnqueueSfx(cfg.dso.AudioId.PlayerDiscard);

                return;
            }

            if (!logic.Stats.TryConsumeSeed()) return;

            Vector2 origin = player.Position;

            Vector2 target = _grid.Geometry.CellCenter(cell);

            // 发射失败原样退回：种子已扣，静默少一颗是看不出来的
            if (_balls == null || !_balls.ThrowSeed(origin, target, seed, out _))
            {
                logic.Stats.GrantSeed(seed);
            }
        }

        // 落地只走播种：不调 OnBallHit，故无元素冲量与伤害
        private void OnSeedBallLanded(SeedType seed, Vector2 point)
        {
            PlayerStats stats = player?.Logic?.Stats;

            if (_grid == null || !_grid.Geometry.IsValid)
            {
                stats?.GrantSeed(seed);

                return;
            }

            SeedSpec spec = ConfigModule.GetSeed(seed);

            if (spec == null)
            {
                stats?.GrantSeed(seed);

                return;
            }

            // 飞行途中落点格可能已被别的球改掉，失败一律退回
            if (!SeedPlanter.ExecutePlant(_grid, _grid.WorldToCell(point), seed, spec, ConfigModule.GetDuoReactions()))
            {
                stats?.GrantSeed(seed);

                return;
            }

            EffectContext ctx = EffectContext.At(_grid.Geometry.CellCenter(_grid.WorldToCell(point)));

            ctx.Value = _grid.Geometry.CellSize * 0.5f;

            EffectModule.Play(EffectId.Highlight, in ctx);

            PlaySfx(AudioId.PlayerPlant);
        }

        private void UpdateSeedHighlight()
        {
            if (_highlight == null) return;

            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic == null || logic.Stats.SeedCount <= 0 || logic.Stats.Seed == SeedType.None)
            {
                _highlight.SetSeedTint(null);

                return;
            }

            if (!logic.Combat.HasAim)
            {
                _highlight.SetSeedTint(null);

                return;
            }

            _highlight.SetSeedTint(SeedPlanter.CanPlant(_grid, logic.Combat.AimCell, logic.Stats.Seed));
        }

        private void TryDiscardSeed(PlayerLogic logic)
        {
            PlayerStats stats = logic.Stats;

            if (!stats.TryDiscardSeed(out SeedType discarded, out _)) return;

            Vector2 facing = logic.Motor.Facing;

            if (facing.sqrMagnitude < 1e-6f) facing = Vector2.right;

            Vector2 origin = player.Position;

            Vector2 landing = origin + facing.normalized * 0.5f;

            if (_drops != null)
            {
                _drops.TrySpawn(new DropSpawnRequest(DropType.Seed, origin, landing, discarded));

                _drops.SetLastPickupCooldown(SeedDiscardPickupCooldown);
            }

            _root?.Audio.EnqueueSfx(cfg.dso.AudioId.PlayerDiscard);
        }

        // 三重静止判据（无输入＋速度近零＋位移近零）由驱动方给定
        private void TickLifeFountain(float deltaTime)
        {
            if (_lifeFountain == null || _lifeFountainSource == null) return;

            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic == null) return;

            InputProvider input = player.Input;

            Vector2 position = player.Position;

            bool hasMoveInput = input != null && input.MoveInput.sqrMagnitude > 0.0001f;

            bool healed = _lifeFountain.Tick(
                position,
                _lifeFountainSource.PlayerInside,
                hasMoveInput,
                player.EngineVelocity.magnitude,
                deltaTime);

            if (!healed) return;

            if (logic.Stats.TryHeal()) PlaySfx(AudioId.PlayerHeal);
        }

        // 判定是纯函数（ContactProbe.TryFindAttacker），可在 EditMode 测
        private void UpdatePlayerContact(float now)
        {
            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic == null || _grid == null) return;

            if (!logic.IsAlive)
            {
                if (now < _retryAt) return;

                _retryAt = float.PositiveInfinity;

                if (enablePlayerRespawn)
                {
                    player.RespawnToSpawn();
                    ClearAll();
                }
                else
                {
                    _levelLogic?.ChangeState(LevelState.Defeat);
                    GameRoot.Instance.Game.ChangeState(Logic.GameState.Settle);
                }
                return;
            }

            PlayerSpec spec = logic.Stats.Spec;

            Vector2 position = player.Position;
            Vector3Int cell = _grid.WorldToCell(position);

            if (!ContactProbe.TryFindAttacker(
                    cell,
                    position,
                    spec.ContactRadius,
                    _registry,
                    _contactCells,
                    out Vector2 attacker,
                    out float _,
                    out int contactDamage,
                    out _))
            {
                return;
            }

            // 方向由 Damage.At 算，与格子伤害共用同一份方向数学
            Damage damage = Damage.At(
                attacker,
                position,
                contactDamage,
                DamageSource.Contact,
                spec.KnockbackImpulse);

            if (!logic.TakeDamage(in damage, now)) return;

            // 受创反馈：世界侧只报事实，播什么由玩家侧与特效模块决定
            VisualFeelTuning feel = ConfigModule.VisualFeel;

            EffectContext hitShake = EffectContext.At(position);
            hitShake.Value = feel.hitShakeAmplitude;
            hitShake.Duration = feel.hitShakeDuration;

            EffectModule.Play(EffectId.ScreenShake, in hitShake);

            player.OnHurt(in damage);

            if (!logic.IsAlive)
            {
                _retryAt = now + spec.RetryDelay;

                // 只报事实，怎么播由玩家侧翻译
                player.OnDeathTriggered();
            }
        }

        public void ClearAll()
        {
            if (_combat != null) _combat.ClearAll();
            if (_levelLogic != null)
            {
                _levelLogic.Reset();
                _levelLogic.Start();
            }

            // 球也要清，否则复活后会被上一局的球砸出一片泥
            _balls?.ClearAll();

            _drops?.ClearAll();

            TileChainReactor.Clear();

            gridView?.ClearAll();

            // 不清会让复活后上一局的火/电光常驻
            _tileLightmap?.ClearAll();

            // 权属分流：场景木桩满血归位（物体留着），波次怪彻底销毁
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null)
                {
                    _enemies.RemoveAt(i);
                    continue;
                }

                if (enemy.IsScenePlaced)
                {
                    enemy.ResetToSpawn();
                    continue;
                }

                Destroy(enemy.gameObject);

                _enemies.RemoveAt(i);
            }
        }

        /// 场景木桩入口：吃自身打靶配置（enemyId 定行）
        public void AdoptSceneEnemy(EnemyActor actor)
        {
            // 世界未接线就丢弃：Start 可能早于 Assemble
            if (actor == null || actor.IsInitialized || player == null || _grid == null) return;

            EnemySpec spec = ConfigModule.GetEnemy(actor.EnemyId);

            actor.IsScenePlaced = true;

            actor.Initialize(
                actor.Position,
                in spec,
                player.transform,
                Vector2.right,
                _grid,
                _registry,
                actor.transform.parent != null ? actor.transform.parent : actorRoot);

            actor.SfxRequested = PlaySfx;
            actor.Died = OnSceneEnemyDied;

            Track(actor);
        }

        /// 波次怪入口：强制 Chase + 不复活，预制体被误存也毒不到波次
        public void AdoptWaveEnemy(EnemyActor actor, Vector2 position, in EnemySpec spec, Vector2 facing)
        {
            if (actor == null) return;

            actor.IsScenePlaced = false;

            actor.Initialize(position, in spec, player.transform, facing, _grid, _registry, actorRoot);

            actor.SfxRequested = PlaySfx;
            if (actor.IsInitialized) PlaySfx(AudioId.EnemySpawn);

            Track(actor);
        }

        private void Track(EnemyActor enemy)
        {
            if (enemy != null && !_enemies.Contains(enemy))
            {
                _enemies.Add(enemy);
            }
        }

        // 唯一否决判据：落点格没有地板；将来的阻挡/占位物加在这里，玩家侧不用改
        public bool RequestThrow(in ThrowIntent intent)
        {
            if (!IsReady) return false;

            if (_grid == null || !_grid.HasCell(intent.Cell)) return false;

            return _balls != null && _balls.Throw(in intent);
        }

        // 组装战斗切片：依赖全来自参数与 Data 层，无 FindObjectOfType
        private void Assemble()
        {
            if (player == null || gridView == null)
            {
                Debug.LogError(
                    "CombatRoot 引用未接线（player / gridView 至少缺一个），战斗内容已停用。",
                    this);
                return;
            }

            if (enableLevel && levelReader == null)
            {
                Debug.LogError("CombatRoot 未接入 LevelReader；关卡流程与波次刷怪不会装配。", this);
                return;
            }

            if (!gridView.IsWired)
            {
                Debug.LogError("CombatRoot 的 TilemapAdapter 没有接 Tilemap，战斗内容已停用。", this);
                return;
            }

            if (player.Logic == null)
            {
                Debug.LogError(
                    "CombatRoot 拿不到玩家的逻辑层（PlayerController 的 motor / inputProvider 没接好，" +
                    "或它的装配失败），战斗内容已停用。",
                    this);
                return;
            }

            PlayerSpec playerSpec = player.Logic.Stats.Spec;

            IReadOnlyList<ProjectileSpec> balls = ConfigModule.GetAllBalls();

            // 元素层两张表：球砸地面的规则网，以及地面与地面的二级反应
            IReadOnlyList<ElementRuleSpec> elementRules = ConfigModule.GetElementRules();

            DuoReactionCatalog duo = ConfigModule.GetDuoReactions();

            ReactionResolver.Initialize(elementRules);

            TileChainReactor.Clear();

            _registry = new EnemyCellRegistry();

            // 关掉怪与怪的物理推挤：一格宽的缺口塞不下两只，互相顶住会让速度归零、整群卡成一团。
            // 与层级设置是同一件事的两个入口（Layer Collision Matrix），写死一份免得换机器漏配。
            Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);

            GridGeometry geometry = gridView.ReadGeometry();

            _grid = new GridLogic(
                geometry,
                ConfigModule.GetAllTileStates(),
                CreateTileState,
                duo,
                _registry);

            // 先订阅格子状态变化再灌初始状态，否则那批泥浆不会被画出来
            gridView.Attach();

            // 同上：晚订阅一步，开局那批火/电地块一个格都不会亮
            _tileLightmap = GetComponentInChildren<TileGridLightmap>(true);

            // 装配工具挂在 [Environment]（场景根，不是子节点）上，这里要认得，否则会多造一个
            if (_tileLightmap == null) _tileLightmap = FindObjectOfType<TileGridLightmap>();

            if (_tileLightmap == null)
            {
                _tileLightmap = new GameObject("TileGridLightmap").AddComponent<TileGridLightmap>();
            }

            // 尺寸取实画矩形：硬编码 64×36 会裁掉最右一列
            gridView.TryGetCellBounds(out BoundsInt lightmapBounds);

            _tileLightmap.Attach(_grid, in geometry, in lightmapBounds);

            int cells = gridView.RegisterCells(_grid);

            // 初始地块优先读场景的 InitialSetup 笔刷层，没画才退回表驱动的 tile_initial
            int initialStates = gridView.LoadInitialSetupTiles(_grid);

            if (initialStates == 0)
            {
                initialStates = _grid.LoadInitialStates(ConfigModule.GetTileInitials());
            }

            // 必须早于任何敌人生成：否则首帧绕障拿不到净空图，只能直扑场景障碍
            _grid.SetNavMap(SceneObstacleBaker.Bake(_grid, ConfigModule.NavClearanceCap));

            _highlight = CreateHighlightView(geometry);
            _highlight.Attach();

            _impulses = new ImpulseExecutor();

            _balls = new BallDirector();
            _balls.Attach(_grid, balls, _impulses, ballRoot, PlaySfx);
            _balls.SetSeedLandingHandler(OnSeedBallLanded);

            _drops = new DropDirector();
            _drops.Attach(actorRoot, player.transform);

            AttachFountains();

            AssembleSceneEnemies();

            player.Logic.ConfigureAim(in geometry, playerSpec.MaxThrowDistance, this);

            if (enableLevel)
            {
                _levelLogic = new LevelLogic(levelReader);
                _levelLogic.Start();
                _combat = CreateCombatDirector(_levelLogic);
            }

            IsReady = true;

            // 只装配时入队一次，勿挪进状态切换
            if (_root != null) _root.Audio.EnqueueBgm(cfg.dso.AudioId.BgmMain);

            if (_root != null) _root.UI.EnterGameplayUi();

            OnRequestHudRefresh(new RequestHudRefresh());

            // 场景已重载干净，这里盖回填
            if (_root != null && _root.Save != null
                && _root.Save.TryConsumePendingRestore(out GameSaveData pending))
            {
                RestoreGameState(pending);
            }
        }

        private void AttachFountains()
        {
            _tileLightmap?.ClearSteadyEmitters();

            for (int i = 0; i < fountains.Length; i++)
            {
                Fountain fountain = fountains[i];

                if (fountain == null) continue;

                fountain.Attach(_drops);

                _tileLightmap?.AddSteadyEmitter(
                    fountain.transform.position, fountain.SelfLightColor, fountain.SelfLightIntensity, fountain.SelfLightPulseDepth);

                if (fountain.Kind != FountainKind.Life) continue;

                if (_lifeFountain != null)
                {
                    Debug.LogWarning(
                        "[Combat] 场景里有多个 kind=Life 的泉眼：只认第一个，其余不会回血。", fountain);

                    continue;
                }

                _lifeFountainSource = fountain;

                _lifeFountain = new LifeFountainState(player.Logic.Stats.Spec.LifeHealInterval);
            }
        }

        // 状态工厂：给 ID 造新实例，null = 该 ID 没有实现
        // 不共享原型：状态自己记持续时长，否则全场共用一个计时器
        private static ITileState CreateTileState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        private TileHighlightView CreateHighlightView(in GridGeometry geometry)
        {
            var go = new GameObject("AimHighlight");

            go.layer = RenderOrder.OverlayLayer;

            if (ballRoot != null) go.transform.SetParent(ballRoot, true);

            var view = go.AddComponent<TileHighlightView>();

            view.Initialize(in geometry, geometry.IsValid ? geometry.CellSize : 1f);

            return view;
        }

        private CombatDirector CreateCombatDirector(LevelLogic levelLogic)
        {
            var go = new GameObject("EnemyDirector");

            if (actorRoot != null) go.transform.SetParent(actorRoot, true);

            var director = go.AddComponent<CombatDirector>();

            director.Initialize(
                this,
                player,
                levelLogic,
                ConfigModule.GetEnemy,
                actorRoot,
                spawnCamera,
                player.World.Bounds,
                OnEnemyKilled);

            return director;
        }

        private void OnEnemyKilled(EnemySpec spec, Vector2 position)
        {
            if (spec == null || _drops == null) return;

            IReadOnlyList<EnemyDropSpec> dropRules = ConfigModule.GetEnemyDrops(spec.Id);

            if (dropRules == null || dropRules.Count == 0) return;

            if (!TryRollDrop(dropRules, out SeedType selectedSeed)) return;

            Vector2 landing = position + Rng.InsideUnitCircle() * 0.8f;

            _drops.TrySpawn(new DropSpawnRequest(DropType.Seed, position, landing, selectedSeed));
        }

        private void OnSceneEnemyDied(EnemyActor enemy)
        {
            EnemySpec spec = enemy != null && enemy.Stats != null ? enemy.Stats.Spec : null;

            if (spec != null) OnEnemyKilled(spec, enemy.Position);
        }

        private static bool TryRollDrop(IReadOnlyList<EnemyDropSpec> rules, out SeedType selected)
        {
            selected = SeedType.None;

            if (rules == null || rules.Count == 0) return false;

            if (Rng.Value01() > rules[0].Probability) return false;

            int totalWeight = 0;

            for (int i = 0; i < rules.Count; i++) totalWeight += rules[i].Weight;

            if (totalWeight <= 0) return false;

            int roll = Rng.Range(0, totalWeight);
            int current = 0;

            for (int i = 0; i < rules.Count; i++)
            {
                current += rules[i].Weight;

                if (roll < current)
                {
                    selected = rules[i].SeedType;
                    return true;
                }
            }

            return false;
        }

        // 裁决在掉落物之外；加一种掉落物在这里加 case
        private void OnDropCollected(DropCollected evt)
        {
            PlaySfx(AudioId.PlayerPickup);

            PlayerStats stats = player?.Logic?.Stats;

            if (stats == null) return;

            // 水土按次 +1：满额不涨点，但掉落物照样回池
            switch (evt.Type)
            {
                case DropType.Water:
                    stats.TryAddAmmo(ResourceKind.Water, 1);
                    return;

                case DropType.Earth:
                    stats.TryAddAmmo(ResourceKind.Earth, 1);
                    return;

                case DropType.Seed:
                    // 二次防线：持种时上游已拦下
                    stats.GrantSeed(evt.Seed);
                    return;

                default:
                    Debug.LogWarning(
                        $"[Combat] 领取了没有接线效果的掉落物 {evt.Type}（{evt.Amount} 个），本次不产生任何变化。");
                    return;
            }
        }

        // HUD 加载完成时重播：三块读数各播一次当前值
        private void OnRequestHudRefresh(RequestHudRefresh evt)
        {
            player?.Logic?.Stats.Announce();
            _combat?.Announce();
            _levelLogic?.Announce();
        }

        /// <summary>备战期自动存档；只在渲染帧采集落盘</summary>
        private void OnWavePhaseChanged(WavePhaseChanged evt)
        {
            if (_restoring) return;

            if (evt.Phase != WavePhase.Prep) return;

            _autoSaveRequested = true;
        }

        /// <summary>抓当前帧快照；缩略图抓帧在此发起</summary>
        public GameSaveData CollectGameState(int slotIndex, bool isAuto)
        {
            var data = new GameSaveData
            {
                schemaVersion = GameSaveData.CurrentSchemaVersion,
                header = new SaveHeaderDTO
                {
                    schemaVersion = GameSaveData.CurrentSchemaVersion,
                    slotIndex = slotIndex,
                    isAuto = isAuto,
                    currentLevelId = CurrentLevelId,
                    waveIndex = _levelLogic != null ? _levelLogic.Waves.WaveIndex : 0
                },
                wave = _levelLogic != null ? _levelLogic.Waves.ExportSave() : null,
                tiles = _grid != null ? _grid.ExportTiles() : new List<TileSaveData>(),
                enemies = _combat != null ? _combat.ExportEnemies() : new List<EnemySaveData>(),
                drops = _drops != null ? _drops.ExportDrops() : new List<DropSaveData>()
            };

            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic != null)
            {
                Vector2 position = player.Position;

                Vector2 facing = logic.Motor.Facing;

                PlayerSaveData save = logic.Stats.ExportSave();

                save.posX = position.x;
                save.posY = position.y;
                save.faceX = facing.x;
                save.faceY = facing.y;

                data.player = save;
            }

            SaveCaptureFeature.TryRequestCapture(slotIndex);

            return data;
        }

        /// <summary>整档回填；先清场再逐块还原</summary>
        public void RestoreGameState(GameSaveData data)
        {
            if (!IsReady || data == null) return;

            // 清场与回填都会发相位事件，期间掐掉自动存档
            _restoring = true;

            ClearAll();

            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic != null && data.player != null)
            {
                var position = new Vector2(data.player.posX, data.player.posY);

                logic.Motor.StopMove();
                logic.Motor.Move(Vector2.zero);
                logic.Motor.SetPosition(position);
                logic.Motor.Facing = new Vector2(data.player.faceX, data.player.faceY);

                logic.Stats.ImportSave(data.player);
            }

            if (_levelLogic != null) _levelLogic.Waves.ImportSave(data.wave);

            if (_grid != null) _grid.RestoreTiles(data.tiles);

            if (_combat != null) _combat.RestoreEnemies(data.enemies);

            if (_drops != null) _drops.RestoreDrops(data.drops);

            _restoring = false;

            OnRequestHudRefresh(new RequestHudRefresh());
        }

        private void OnGridBlastOccurred(GridBlastOccurred evt)
        {
            if (evt.ResultTile != TileStateType.Steam) return;

            VisualFeelTuning feel = ConfigModule.VisualFeel;

            EffectContext ctx = EffectContext.At(evt.Center);
            ctx.Value = evt.Radius; // ParticleDriver 会将预制体缩放与 Value 相乘，使扩散范围与半径对齐
            ctx.Tint = new Color(0.92f, 0.96f, 1f, 0.85f);
            EffectModule.Play(EffectId.SteamBlast, in ctx);

            // 半径映射到 0.4..1：ScreenShakeDriver 自持 Clamp01，塞原始半径会被拍成恒定满档
            float shake = Mathf.Lerp(feel.blastShakeAmplitudeMin, feel.blastShakeAmplitudeMax, Mathf.InverseLerp(1f, 2.5f, evt.Radius));

            EffectContext shakeCtx = EffectContext.At(evt.Center);
            shakeCtx.Value = shake;
            shakeCtx.Duration = feel.blastShakeDuration;

            EffectModule.Play(EffectId.ScreenShake, in shakeCtx);
        }

        // 逐格报点：整网同帧各播一次
        private void OnGridPointVfxOccurred(GridPointVfxOccurred evt)
        {
            EffectContext ctx = EffectContext.At(evt.Point);

            if (evt.IsFire)
                EffectModule.Play(EffectId.FlameEmbers, in ctx);
            else
                EffectModule.Play(EffectId.ChainShock, in ctx);
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            if (!IsReady || !TryGetTileSfx(evt.Previous, evt.State, out AudioId id)) return;

            PlaySfx(id);
        }

        private static bool TryGetTileSfx(TileStateType previous, TileStateType state, out AudioId id)
        {
            id = default;

            if (state == TileStateType.Normal)
            {
                switch (previous)
                {
                    case TileStateType.BasicFire:
                    case TileStateType.FlameField:
                        id = AudioId.TileAsh;
                        return true;
                    case TileStateType.BasicIce:
                        id = AudioId.TileGlassBreak;
                        return true;
                }

                return false;
            }

            switch (state)
            {
                case TileStateType.Steam:
                    id = AudioId.TileSteam;
                    return true;
                case TileStateType.Freeze:
                    id = AudioId.TileFreeze;
                    return true;
                case TileStateType.FrostSpike:
                    id = AudioId.TileIceShard;
                    return true;
                case TileStateType.BasicFire:
                case TileStateType.FlameField:
                    id = AudioId.TileBurn;
                    return true;
                case TileStateType.Mud:
                case TileStateType.MudSkid:
                    id = AudioId.TileMud;
                    return true;
                case TileStateType.BasicIce:
                    id = AudioId.TileFrozenSoil;
                    return true;
                case TileStateType.TerracottaBrick:
                    id = AudioId.TileBrick;
                    return true;
                case TileStateType.BasicEarth:
                    id = AudioId.Tile;
                    return true;
                case TileStateType.ChargedThorn:
                    id = AudioId.TileThorn;
                    return true;
                case TileStateType.BasicElectricity:
                case TileStateType.ConductZone:
                    id = AudioId.TileConduct;
                    return true;
                default:
                    return false;
            }
        }

        private void PlaySfx(AudioId id)
        {
            if (_root == null || _root.Audio == null)
            {
                Debug.LogError("[Combat] 音效请求时 GameRoot/AudioManager 未装配。", this);
                return;
            }

            _root.Audio.EnqueueSfx(id);
        }

        // 装配期扫一次场景预摆怪（运行时拖入的由 Start 报到）
        private void AssembleSceneEnemies()
        {
            if (actorRoot != null)
            {
                AdoptEnemiesIn(actorRoot);

                return;
            }

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                AdoptEnemiesIn(roots[i].transform);
            }
        }

        private void AdoptEnemiesIn(Transform root)
        {
            EnemyActor[] actors = root.GetComponentsInChildren<EnemyActor>(true);

            for (int i = 0; i < actors.Length; i++)
            {
                AdoptSceneEnemy(actors[i]);
            }
        }
    }
}
