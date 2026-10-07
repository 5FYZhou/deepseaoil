using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation.Actor;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Ball;
using DeepseaOil.Presentation.Drop;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Visual;
using DeepseaOil.Presentation.World;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation
{
    /// <summary>战斗切片的组合根：<b>装配一次，然后每帧被驱动</b>。</summary>
    /// <remarks>它自己没有 <c>Update</c> / <c>FixedUpdate</c>：由 <c>GameRoot</c> 的两个通道分别调 <see cref="RenderTick"/> 与 <see cref="FixedTick"/>；帧内顺序因此是可预测的：渲染帧 = 格子 → 球（飞行 ＋ 落地改格）→ 喷泉，物理帧 = 落地冲量 → 敌人 → 玩家受击。
    /// 装配放在 <c>Start</c> 而不是 <c>Awake</c>：要读 <c>ConfigModule</c> 与场景里其他组件的 <c>Awake</c> 结果。没接线时是显式降级：报一条 Error 并停用，而不是静默留一个"按了没反应"的场景。</remarks>
    public sealed class CombatRoot : MonoBehaviour, ISceneRoot, IRenderTicked, IPhysicsTicked, IThrowSink
    {
        /// <summary>驱动顺序：世界侧排在玩家侧（<see cref="SceneOrder.Player"/>）之后。</summary>
        public int Order => SceneOrder.World;

        [Header("必需接线")]
        [Tooltip("玩家组合根（场景里的 PlayerController）。")]
        [SerializeField] private PlayerController player = default;

        [Tooltip("格子视图（提供格子几何与地板）。")]
        [SerializeField] private TilemapAdapter gridView = default;

        [Header("可选接线")]
        [Tooltip("球与瞄准件的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform ballRoot = default;

        [Tooltip("敌人的父物体。留空则建在场景根下。")]
        [SerializeField] private Transform actorRoot = default;

        [Tooltip("场景里的喷泉。每帧由本类驱动（水球不是自驱的）。留空则资源系统不生效。")]
        [SerializeField] private Fountain[] fountains = new Fountain[0];

        [Tooltip("是否刷敌人。关掉可以只验投掷链路。")]
        [SerializeField] private bool enableWaves = true;

        private GameRoot _root;

        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        /// <summary>元素层：合成 ＋ 规则匹配 ＋ 每格元素。由本组合根装配后<b>构造注入</b>给 <see cref="GridLogic"/>（§8）。</summary>
        private TileElementReactor _element;

        private BallDirector _balls;

        /// <summary>落地冲量的物理帧执行者。</summary>
        private ImpulseExecutor _impulses;

        private DropDirector _drops;

        private CombatDirector _combat;
        private TileHighlightView _highlight;

        /// <summary>接触判定复用的格缓冲（每个物理帧都要跑，不能每帧分配）。</summary>
        private readonly List<Vector3Int> _contactCells = new List<Vector3Int>(9);

        /// <summary>打空之后允许重来的时刻；没打空时是正无穷。</summary>
        private float _retryAt = float.PositiveInfinity;

        public bool IsReady { get; private set; }

        public GridLogic Grid => _grid;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        /// <summary>装配战斗切片。<b>由 <c>GameRoot</c> 在第一个被驱动的帧按 <see cref="Order"/> 调</b>。</summary>
        public void Attach()
        {
            if (IsReady) return;

            Assemble();
        }

        private void OnDestroy()
        {
            _highlight?.Detach();
            gridView?.Detach();

            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的出来
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        private void OnEnable()
        {
            EventBus<DropCollected>.Subscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Subscribe(OnRequestHudRefresh);
        }

        private void OnDisable()
        {
            EventBus<DropCollected>.Unsubscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Unsubscribe(OnRequestHudRefresh);
        }

        public void RenderTick(float deltaTime)
        {
            if (!IsReady) return;

            // 顺序：格子先跑（泥浆可能在这一帧到期并结算），再推进球（落地那一帧就改格 ＋ 排冲量），最后是喷泉（它只负责产出节拍）。
            _grid.Tick(Time.time, deltaTime);

            _balls.Tick(deltaTime);

            _drops.Tick(deltaTime);

            for (int i = 0; i < fountains.Length; i++)
            {
                Fountain fountain = fountains[i];

                if (fountain != null) fountain.Tick(deltaTime);
            }
        }

        public void FixedTick(float deltaTime)
        {
            if (!IsReady) return;

            float now = Time.fixedTime;

            // ① 落地冲量：必须在物理帧施加（渲染帧施加会不报错地漂，见 ImpulseExecutor 的类注释）。
            _impulses.FixedTick();

            // ② 敌人：先让它们按本帧的位置追一步，再让格子按新位置结算（顺序固定 = 可复现）。
            if (_combat != null) _combat.FixedTick(now, deltaTime);

            // ③ 玩家受击：接触检测读物理体位置，放在敌人移动之后才是"这一帧的真实站位"。
            UpdatePlayerContact(now);
        }

        /// <summary>世界侧的两件"玩家相关"裁决：<b>谁打到了玩家</b>、<b>打空了怎么重来</b>。</summary>
        /// <remarks>判定本身是纯函数（<see cref="ContactProbe.TryFindAttacker"/>），所以它能在 EditMode 里测。世界 → 玩家只有"通知"一条路：本方法组装一次 <see cref="Damage"/> 经 <c>PlayerLogic.TakeDamage</c> 递交，
        /// 扣多少血、进入多久无敌、被推多远都由玩家侧自己决定。</remarks>
        private void UpdatePlayerContact(float now)
        {
            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic == null || _grid == null) return;

            if (!logic.IsAlive)
            {
                if (now < _retryAt) return;

                _retryAt = float.PositiveInfinity;

                player.RespawnToSpawn();
                ClearAll();

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
                    out _))
            {
                return;
            }

            // 方向由 Damage.At 算（"从接触者指向玩家"），与格子伤害共用同一份方向数学
            Damage damage = Damage.At(
                attacker,
                position,
                spec.ContactDamage,
                DamageSource.Contact,
                spec.KnockbackImpulse);

            if (!logic.TakeDamage(in damage, now)) return;

            if (!logic.IsAlive) _retryAt = now + spec.RetryDelay;
        }

        /// <summary>清场：把世界侧一局里的"临时东西"清空（打空重来 / 切场景）。</summary>
        public void ClearAll()
        {
            if (_combat != null) _combat.ClearAll();

            // 球也要清：不清的话玩家复活后会被自己上一局扔出的球砸出一片泥。
            _balls?.ClearAll();

            // 掉落物同理：上一局没捡完的水球不该留到下一局（玩家会以为自己捡过了）。
            _drops?.ClearAll();
        }

        /// <summary>裁决一次投掷请求（<see cref="IThrowSink"/>）：<b>落点合法性是世界信息</b>，所以这一问必须由世界侧回答，而不是玩家侧猜。</summary>
        /// <remarks>否决的判据当前只有一条：<b>落点那一格没有地板</b>（<c>GridLogic.HasCell</c>）。将来的阻挡 / 占位物会加在这里，
        /// 加的时候玩家侧一行都不用改。</remarks>
        public bool RequestThrow(in ThrowIntent intent)
        {
            if (!IsReady) return false;

            if (_grid == null || !_grid.HasCell(intent.Cell)) return false;

            return _balls != null && _balls.Throw(in intent);
        }

        /// <summary>组装整个战斗切片。依赖全部来自参数与 Data 层：<b>没有 <c>FindObjectOfType</c>，也没有从 Inspector 拖进来的数值</b>；射程（取水球那一行）由 <c>PlayerSpec.MaxThrowDistance</c> 统一给出。</summary>
        private void Assemble()
        {
            if (player == null || gridView == null)
            {
                Debug.LogError(
                    "CombatRoot 引用未接线（player / gridView 至少缺一个），战斗内容已停用。",
                    this);
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

            // 元素层的三份表数据：反应规则（顺序即优先级）＋ 地块效果（DoT 的数值与节奏来源）。
            IReadOnlyList<ElementRuleSpec> elementRules = ConfigModule.GetElementRules();
            IReadOnlyList<TileEffectSpec> tileEffects = ConfigModule.GetTileEffects();

            _registry = new EnemyCellRegistry();

            GridGeometry geometry = gridView.ReadGeometry();

            // 元素层由组合根装配（§13 D8）：GridLogic 只收一个端口，不认识规则表也不认识 ConfigModule。
            _element = new TileElementReactor(elementRules);

            _grid = new GridLogic(
                geometry,
                ConfigModule.GetAllTileStates(),
                CreateTileState,
                _element,
                tileEffects,
                _registry);

            // 先开始听"格子状态变了"，再灌初始状态：订阅晚了那一批泥浆就不会被画出来。
            gridView.Attach();

            int cells = gridView.RegisterCells(_grid);
            int initialStates = _grid.LoadInitialStates(ConfigModule.GetTileInitials());

            _highlight = CreateHighlightView(geometry);
            _highlight.Attach();

            _impulses = new ImpulseExecutor();

            _balls = new BallDirector();
            _balls.Attach(_grid, balls, _impulses, ballRoot);

            _drops = new DropDirector();
            _drops.Attach(actorRoot, player.transform);

            for (int i = 0; i < fountains.Length; i++)
            {
                if (fountains[i] != null) fountains[i].Attach(_drops);
            }

            player.Logic.ConfigureAim(in geometry, playerSpec.MaxThrowDistance, this);

            if (enableWaves)
            {
                _combat = CreateCombatDirector();
            }

            IsReady = true;

            Debug.Log(
                $"[Combat] 装配完成：格子 {cells} 个（初始状态 {initialStates} 个），" +
                $"球种 {balls.Count} 个，喷泉 {fountains.Length} 个，" +
                $"反应规则 {elementRules.Count} 条，地块效果 {tileEffects.Count} 个，" +
                (enableWaves
                    ? "敌人 启用"
                    : "敌人 关闭（CombatRoot 的「是否刷敌人」未勾选：想要刷怪请在 Inspector 上勾上它）"));
        }

        /// <summary>状态工厂：给 ID 造一个新实例。返回 <c>null</c> 表示"这个 ID 没有实现"。</summary>
        /// <remarks>每次进入状态都造新实例（而不是共享一个原型）：状态把"已经持续了多久"放在自己的字段里 —— 共享会让全场格子共用一个计时器，现象是"两片泥浆一起消失"，不报错。</remarks>
        private static ITileState CreateTileState(TileStateType id)
        {
            switch (id)
            {
                case TileStateType.Mud:
                    return new MudTileState(ConfigModule.GetTileState(id));

                default:
                    return null;
            }
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

        private CombatDirector CreateCombatDirector()
        {
            var go = new GameObject("EnemyDirector");

            if (actorRoot != null) go.transform.SetParent(actorRoot, true);

            var director = go.AddComponent<CombatDirector>();

            director.Initialize(
                player,
                ConfigModule.GetWave(),
                ConfigModule.GetEnemy(),
                _grid,
                _registry,
                actorRoot);

            return director;
        }

        /// <summary>掉落物被领取：<b>世界 → 玩家的通知</b>，按种类裁决给玩家什么。</summary>
        /// <remarks>裁决在这里，不在掉落物里：掉落物只发事实。加一种掉落物时在这里加一个 <c>case</c> —— 玩家侧一行都不用改。</remarks>
        private void OnDropCollected(DropCollected evt)
        {
            switch (evt.Type)
            {
                case DropType.Water:
                    player?.Logic?.Stats.AddWaterBall(evt.Amount);
                    return;

                default:
                    Debug.LogWarning(
                        $"[Combat] 领取了没有接线效果的掉落物 {evt.Type}（{evt.Amount} 个），本次不产生任何变化。");
                    return;
            }
        }

        /// <summary>HUD 面板加载完成时的重播：把三块读数各播一次当前值。</summary>
        private void OnRequestHudRefresh(RequestHudRefresh evt)
        {
            player?.Logic?.Stats.Announce();
            _combat?.Announce();
        }
    }
}
