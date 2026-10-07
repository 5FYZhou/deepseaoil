using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation.Actor;
using DeepseaOil.Presentation.Combat;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Player;
using DeepseaOil.Presentation.World;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Logic.Grid.Effects;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 战斗切片的组合根：<b>装配一次，然后每帧被驱动</b>。
    /// </summary>
    /// <remarks>
    /// <b>它自己没有 <c>Update</c> / <c>FixedUpdate</c>。</b>框架的硬契约是"每帧只有四个驱动入口"
    /// （<c>GameRoot.Update</c> / <c>GameRoot.FixedUpdate</c> / <c>PlayerController.FixedUpdate</c> / <c>InputProvider.Update</c>），
    /// 所以本类只暴露 <see cref="Tick"/> 与 <see cref="FixedTick"/>，由 <c>GameRoot</c> 调。
    /// 这样帧内顺序是可预测的：格子 → 投掷/瞄准 → 敌人 → 落地结算 → 玩家受击。
    /// <para><b>环境事实只在这里组装一次</b>：调参资产、Luban 表值、格子几何、敌人归属表、
    /// 各子系统之间的引用。子系统自己不认识彼此 —— 它们只认识被注入的东西。</para>
    /// <para><b>装配放在 <c>Start</c> 而不是 <c>Awake</c>：</b>它要读 <c>ConfigModule</c>（由
    /// <c>GameRoot.Awake</c> 装配）与场景里其他组件的 <c>Awake</c> 结果（例如 <c>PlayerController</c>
    /// 的逻辑层）。Unity 保证"所有 Awake 先于任何 Start"。</para>
    /// <para><b>没接线时是显式降级</b>：报一条 Error 并停用，而不是静默留一个"按了没反应"的场景。</para>
    /// </remarks>
    public sealed class CombatRoot : MonoBehaviour
    {
        [Header("必需接线")]
        [Tooltip("玩家组合根（场景里的 PlayerController）。")]
        [SerializeField] private PlayerController player = default;

        [Tooltip("格子视图（提供格子几何与地板）。")]
        [SerializeField] private GridView gridView = default;

        [Tooltip("输入采样器（场景里的 InputProvider）。")]
        [SerializeField] private InputProvider inputProvider = default;

        [Header("可选接线")]
        [Tooltip("瞄准用的相机。留空取 Camera.main。")]
        [SerializeField] private Camera aimCamera = default;

        [Tooltip("球与瞄准件的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform ballRoot = default;

        [Tooltip("敌人的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform actorRoot = default;

        [Tooltip("场景里的喷泉。每帧由本类驱动（水球不是自驱的）。留空则资源系统不生效。")]
        [SerializeField] private Fountain[] fountains = new Fountain[0];

        [Tooltip("是否刷敌人。关掉可以只验投掷链路。")]
        [SerializeField] private bool enableWaves = true;

        private GridLogic _grid;
        private EnemyCellRegistry _registry;
        private PlayerResources _resources;
        private ThrowController _throw;
        private PlayerHealthController _health;
        private LandingResolver _resolver;
        private WaveDirector _waves;
        private TileAimView _aim;
        private ReactionResolver _reactionResolver;
        private TileEffectExecutor _tileEffectExecutor;

        /// <summary>装配是否成功（失败时所有 Tick 都是 no-op）。</summary>
        public bool IsReady { get; private set; }

        /// <summary>格子门面（诊断 / 测试用）。</summary>
        public GridLogic Grid => _grid;

        /// <summary>敌人调度器；未启用时为 <c>null</c>。</summary>
        public WaveDirector Waves => _waves;

        private void Start()
        {
            Assemble();
        }

        private void OnEnable()
        {
            EventBus<WaterBallCollected>.Subscribe(OnWaterBallCollected);
            EventBus<RequestHudRefresh>.Subscribe(OnRequestHudRefresh);
        }

        private void OnDisable()
        {
            EventBus<WaterBallCollected>.Unsubscribe(OnWaterBallCollected);
            EventBus<RequestHudRefresh>.Unsubscribe(OnRequestHudRefresh);
        }

        /// <summary>渲染帧驱动（由 <c>GameRoot.Update</c> 调）。</summary>
        /// <param name="deltaTime"><c>Time.deltaTime</c>；暂停时为 0，各子系统因此自然冻结。</param>
        public void Tick(float deltaTime)
        {
            if (!IsReady) return;

            // 顺序：格子先跑（泥浆可能在这一帧到期并结算），再处理投掷（落地只入队）。
            _grid.Tick(Time.time, deltaTime);

            _throw.Tick(deltaTime);

            for (int i = 0; i < fountains.Length; i++)
            {
                Fountain fountain = fountains[i];

                if (fountain != null) fountain.Tick(deltaTime);
            }
        }

        /// <summary>物理帧驱动（由 <c>GameRoot.FixedUpdate</c> 调）。</summary>
        /// <param name="deltaTime"><c>Time.fixedDeltaTime</c>。</param>
        public void FixedTick(float deltaTime)
        {
            if (!IsReady) return;

            float now = Time.fixedTime;

            // ① 落地结算：冲量必须在物理帧施加（见 LandingResolver）。
            _resolver.FixedTick();

            // ② 敌人：先让它们按本帧的位置追一步，再让格子按新位置结算（顺序固定 = 可复现）。
            if (_waves != null) _waves.FixedTick(now, deltaTime);

            // ③ 玩家受击：接触检测读的是物理体位置，放在敌人移动之后才是"这一帧的真实站位"。
            _health.FixedTick(now);
        }

        private void Assemble()
        {
            if (player == null || gridView == null || inputProvider == null)
            {
                Debug.LogError(
                    "CombatRoot 引用未接线（player / gridView / inputProvider 至少缺一个），战斗内容已停用。",
                    this);
                return;
            }

            if (!gridView.IsWired)
            {
                Debug.LogError("CombatRoot 的 GridView 没有接 Tilemap，战斗内容已停用。", this);
                return;
            }

            ThrowTuning tuning = ThrowTuning.LoadOrDefault();

            IReadOnlyList<BallSpec> balls = SpecCatalog.AllBalls();
            PlayerSpec playerSpec = SpecCatalog.Player();

            var rules = SpecCatalog.AllElementRules();
            _reactionResolver = new(rules);

            _registry = new EnemyCellRegistry();

            GridGeometry geometry = gridView.ReadGeometry();

            var effects = SpecCatalog.AllTileEffects();
            _tileEffectExecutor = new(effects);
            _tileEffectExecutor.Register(new SlowEffect());
            _tileEffectExecutor.Register(new DamageInstantEffect(_registry, geometry));
            _tileEffectExecutor.Register(new KnockBackEffect(_registry, geometry));

            _grid = new GridLogic(geometry, SpecCatalog.AllTileStates(), CreateTileState, _reactionResolver, _tileEffectExecutor, _registry);

            int cells = gridView.RegisterCells(_grid);
            int initialStates = _grid.LoadInitialStates(SpecCatalog.TileInitials());

            _aim = CreateAimView(geometry, tuning);

            _resolver = gameObject.AddComponent<LandingResolver>();
            _resolver.Initialize(_grid, tuning, balls);

            _resources = new PlayerResources();

            _throw = gameObject.AddComponent<ThrowController>();
            _throw.Initialize(
                player,
                gridView,
                inputProvider,
                _grid,
                tuning,
                _resolver,
                _aim,
                _resources,
                in playerSpec,
                balls,
                ballRoot,
                aimCamera);

            if (enableWaves)
            {
                _waves = CreateWaveDirector();
            }

            _health = gameObject.AddComponent<PlayerHealthController>();
            _health.Initialize(player, in playerSpec, _waves);

            IsReady = true;

            Debug.Log(
                $"[Combat] 装配完成：格子 {cells} 个（初始状态 {initialStates} 个），" +
                $"球种 {balls.Count} 个，喷泉 {fountains.Length} 个，敌人 {(enableWaves ? "启用" : "关闭")}，" +
                $"反应规则 {rules.Count}条, 效果{effects.Count}个");
        }

        /// <summary>
        /// 状态工厂：给 ID 造一个新实例。返回 <c>null</c> 表示"这个 ID 没有实现"。
        /// </summary>
        /// <remarks>
        /// <b>每次进入状态都造新实例</b>（而不是共享一个原型）：状态把"已经持续了多久"放在自己的字段里，
        /// 共享会让全场格子共用一个计时器 —— 现象是"两片泥浆一起消失"，不报错。
        /// </remarks>
        private static ITileState CreateTileState(TileStateType id)
        {
            switch (id)
            {
                case TileStateType.Mud:
                    return new MudTileState(SpecCatalog.TileState(id));

                default:
                    return null;
            }
        }

        private TileAimView CreateAimView(in GridGeometry geometry, ThrowTuning tuning)
        {
            var go = new GameObject("瞄准格高亮");

            go.layer = RenderOrder.OverlayLayer;

            if (ballRoot != null) go.transform.SetParent(ballRoot, true);

            var view = go.AddComponent<TileAimView>();

            view.Initialize(geometry.IsValid ? geometry.CellSize : 1f, RenderOrder.Aim);

            return view;
        }

        private WaveDirector CreateWaveDirector()
        {
            var go = new GameObject("敌人调度");

            if (actorRoot != null) go.transform.SetParent(actorRoot, true);

            var director = go.AddComponent<WaveDirector>();

            director.Initialize(
                player.transform,
                SpecCatalog.Wave(),
                SpecCatalog.Enemy(),
                _grid,
                _registry);

            return director;
        }

        private void OnWaterBallCollected(WaterBallCollected evt)
        {
            _resources?.Add(1);
        }

        /// <summary>HUD 面板加载完成时的重播：把三块读数各播一次当前值。</summary>
        private void OnRequestHudRefresh(RequestHudRefresh evt)
        {
            _resources?.Announce();
            _health?.Announce();
            _waves?.Announce();
        }
    }
}
