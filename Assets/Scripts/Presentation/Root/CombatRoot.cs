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
    /// <summary>
    /// 战斗切片的组合根：<b>装配一次，然后每帧被驱动</b>。
    /// </summary>
    /// <remarks>
    /// <b>它自己没有 <c>Update</c> / <c>FixedUpdate</c>。</b>它把自己注册给 <c>GameRoot</c>
    /// （<see cref="ISceneRoot"/>），由 <c>GameRoot</c> 的两个通道分别调
    /// <see cref="RenderTick"/> 与 <see cref="FixedTick"/>。
    /// 帧内顺序因此是可预测的：渲染帧 = 格子 → 球（飞行 ＋ 落地改格）→ 喷泉；
    /// 物理帧 = 落地冲量 → 敌人 → 玩家受击。
    /// <para><b>环境事实只在这里组装一次</b>：Luban 表值、格子几何、敌人归属表、
    /// 各子系统之间的引用。子系统自己不认识彼此 —— 它们只认识被注入的东西。</para>
    /// <para><b>装配放在 <c>Start</c> 而不是 <c>Awake</c>：</b>它要读 <c>ConfigModule</c>（由
    /// <c>GameRoot.Awake</c> 装配）与场景里其他组件的 <c>Awake</c> 结果（例如 <c>PlayerController</c>
    /// 的逻辑层）。Unity 保证"所有 Awake 先于任何 Start"。</para>
    /// <para><b>没接线时是显式降级</b>：报一条 Error 并停用，而不是静默留一个"按了没反应"的场景。</para>
    /// </remarks>
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

        /// <summary>注册时抓住的 GameRoot 引用；销毁期只经它退订（理由见 <see cref="OnDestroy"/>）。</summary>
        private GameRoot _root;

        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        /// <summary>球链路：造 ＋ 持 ＋ 驱（含落地改格）。</summary>
        private BallDirector _balls;

        /// <summary>落地冲量的物理帧执行者。</summary>
        private ImpulseExecutor _impulses;

        /// <summary>掉落物链路：造 ＋ 持 ＋ 驱（含回收）。它同时是产出方的生成口。</summary>
        private DropDirector _drops;

        private CombatDirector _combat;
        private TileHighlightView _highlight;

        /// <summary>接触判定复用的格缓冲（每个物理帧都要跑，不能每帧分配）。</summary>
        private readonly List<Vector3Int> _contactCells = new List<Vector3Int>(9);

        /// <summary>打空之后允许重来的时刻；没打空时是正无穷。</summary>
        private float _retryAt = float.PositiveInfinity;

        /// <summary>装配是否成功（失败时所有 Tick 都是 no-op）。</summary>
        public bool IsReady { get; private set; }

        /// <summary>格子门面（诊断 / 测试用）。</summary>
        public GridLogic Grid => _grid;

        private void Start()
        {
            // 只报到：装配推迟到 GameRoot 的第一个被驱动的帧（见 Attach）
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        /// <summary>
        /// 装配战斗切片。<b>由 <c>GameRoot</c> 在第一个被驱动的帧按 <see cref="Order"/> 调</b>。
        /// </summary>
        /// <remarks>
        /// <b>为什么不再放在 <c>Start</c>：</b>本类的装配要读三样东西 —— 配表（等 <c>GameRoot.Awake</c>）、
        /// 场景里玩家的 <c>Logic</c>（等 <c>PlayerController</c> 装配）、格子几何（等 <c>TilemapAdapter</c> 唤醒）。
        /// 而 Unity 只保证"所有 <c>Awake</c> 先于任何 <c>Start</c>"，<b>不保证两个 <c>Start</c> 的先后</b> ——
        /// 玩家侧 <c>Order = -100</c> 排在前面，于是"谁先装配"从抽签变成一个数字。
        /// </remarks>
        public void Attach()
        {
            if (IsReady) return;

            Assemble();
        }

        private void OnDestroy()
        {
            // 订阅时机与装配对称：谁开始听，谁停止听
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

        /// <summary>渲染帧驱动（由 <c>GameRoot.Update</c> 调）。</summary>
        /// <param name="deltaTime"><c>Time.deltaTime</c>；暂停时为 0，各子系统因此自然冻结。</param>
        public void RenderTick(float deltaTime)
        {
            if (!IsReady) return;

            // 顺序：格子先跑（泥浆可能在这一帧到期并结算），再推进球（落地那一帧就改格 ＋ 排冲量），
            // 最后是喷泉（它只负责产出节拍）。
            _grid.Tick(Time.time, deltaTime);

            _balls.Tick(deltaTime);

            _drops.Tick(deltaTime);

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

            // ① 落地冲量：必须在物理帧施加（见 ImpulseExecutor 的类注释）。
            _impulses.FixedTick();

            // ② 敌人：先让它们按本帧的位置追一步，再让格子按新位置结算（顺序固定 = 可复现）。
            if (_combat != null) _combat.FixedTick(now, deltaTime);

            // ③ 玩家受击：接触检测读的是物理体位置，放在敌人移动之后才是"这一帧的真实站位"。
            UpdatePlayerContact(now);
        }

        /// <summary>
        /// 世界侧的两件"玩家相关"裁决：<b>谁打到了玩家</b>、<b>打空了怎么重来</b>。
        /// </summary>
        /// <remarks>
        /// <b>为什么由组合根做：</b>接触判定要同时看"玩家在哪一格"（<c>GridLogic</c>）、
        /// "这一格上站着谁"（<c>EnemyCellRegistry</c>）与玩家表值 —— 这三样都只在这里齐备。
        /// 判定本身是纯函数（<see cref="ContactProbe.TryFindAttacker"/>），所以它能在 EditMode 里测；
        /// 收口前这条判定住在表现层的 <c>PlayerHealthController</c> 里，靠 <c>Physics2D</c> 才测得了。
        /// <para><b>世界 → 玩家只有"通知"一条路</b>：本方法组装一次 <see cref="Damage"/>，
        /// 经 <c>PlayerLogic.TakeDamage</c> 递交；扣多少血、进入多久无敌、被推多远都由玩家侧自己决定。</para>
        /// <para>打空之后不再判接触（尸体不该继续挨打），等重试延时到点再重来。</para>
        /// </remarks>
        private void UpdatePlayerContact(float now)
        {
            PlayerLogic logic = player != null ? player.Logic : null;

            if (logic == null || _grid == null) return;

            if (!logic.IsAlive)
            {
                if (now < _retryAt) return;

                _retryAt = float.PositiveInfinity;

                // 世界侧决定"重来"：玩家回出生点满血，场上清空
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

        /// <summary>
        /// 清场：把世界侧一局里的"临时东西"清空（打空重来 / 切场景）。
        /// </summary>
        /// <remarks>
        /// 层级规范是"各层清自己的、上层负责下发"：本类只把请求转发给各执行器。
        /// </remarks>
        public void ClearAll()
        {
            if (_combat != null) _combat.ClearAll();

            // 球也要清：不清的话玩家复活后会被自己上一局扔出的球砸出一片泥，
            // 而已经排队的落地冲量会砸到下一局的箱子上。
            _balls?.ClearAll();

            // 掉落物同理：上一局没捡完的水球不该留到下一局（玩家会以为自己捡过了）。
            _drops?.ClearAll();
        }

        /// <summary>
        /// 裁决一次投掷请求（<see cref="IThrowSink"/>）：<b>落点合法性是世界信息</b>，
        /// 所以这一问必须由世界侧回答，而不是玩家侧猜。
        /// </summary>
        /// <param name="intent">意图（球种 ＋ 目标格 ＋ 出手点与落点）。</param>
        /// <returns>采纳（球已经生成）为 <c>true</c>；被拒绝时玩家侧不扣弹药、不进冷却。</returns>
        /// <remarks>否决的判据当前只有一条：<b>落点那一格没有地板</b>（<c>GridLogic.HasCell</c>）。
        /// 将来的阻挡 / 占位物会加在这里，加的时候玩家侧一行都不用改。</remarks>
        public bool RequestThrow(in ThrowIntent intent)
        {
            if (!IsReady) return false;

            if (_grid == null || !_grid.HasCell(intent.Cell)) return false;

            return _balls != null && _balls.Throw(in intent);
        }

        /// <summary>
        /// 组装整个战斗切片。依赖全部来自参数与 Data 层：<b>没有 <c>FindObjectOfType</c>，
        /// 也没有从 Inspector 拖进来的数值</b>。
        /// </summary>
        /// <remarks>
        /// 射程（取水球那一行）由 <c>PlayerSpec.MaxThrowDistance</c> 统一给出，不在本类挑球种。
        /// <para><b>格子效果的执行者不需要注入</b>：<c>GridLogic</c> 自己实现 <c>ITileResolver</c>，
        /// 而"这一格上站着谁"本来就是它自己的知识（敌人归属表）。玩家不登记进那张表
        /// （审查已定：玩家只能被怪打，格子作用不到玩家）。</para>
        /// </remarks>
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

            _registry = new EnemyCellRegistry();

            GridGeometry geometry = gridView.ReadGeometry();

            // 速度修正与伤害的执行者是本类自己（GridLogic 实现 ITileResolver）：
            // "谁站在这一格上"是格子系统自己的知识（敌人归属表），不需要注入执行者。
            _grid = new GridLogic(geometry, ConfigModule.GetAllTileStates(), CreateTileState, _registry);

            // 先开始听"格子状态变了"，再灌初始状态：初始状态走的是同一条转换路径
            // （订阅晚了那一批泥浆就不会被画出来 —— 而它们是最不该漏的一批）。
            gridView.Attach();

            int cells = gridView.RegisterCells(_grid);
            int initialStates = _grid.LoadInitialStates(ConfigModule.GetTileInitials());

            // 瞄准高亮：它是"逻辑层发布 AimChanged → 表现层转成特效调用"的订阅者，
            // 订阅时机由本类收口（Attach/Detach），不自己 OnEnable
            _highlight = CreateHighlightView(geometry);
            _highlight.Attach();

            // 球链路：冲量的物理帧执行者 ＋ 球的调度器（造 / 持 / 驱 / 落地结算）。
            // 顺序无关，但两者都在这里被造 —— "谁造球"因此只有一个答案。
            _impulses = new ImpulseExecutor();

            _balls = new BallDirector();
            _balls.Attach(_grid, balls, _impulses, ballRoot);

            // 掉落物链路：持有者 ＋ 产出方注入。
            // 产出方（喷泉）拿到的是窄接口 IDropSpawner：它不认识 DropDirector，只认识"产出一颗"。
            _drops = new DropDirector();
            _drops.Attach(actorRoot, player.transform);

            for (int i = 0; i < fountains.Length; i++)
            {
                if (fountains[i] != null) fountains[i].Attach(_drops);
            }

            // 玩家侧的瞄准需要三样世界信息：格子几何、射程上限、投掷裁决口（本类）
            player.Logic.ConfigureAim(in geometry, playerSpec.MaxThrowDistance, this);

            if (enableWaves)
            {
                _combat = CreateCombatDirector();
            }

            IsReady = true;

            Debug.Log(
                $"[Combat] 装配完成：格子 {cells} 个（初始状态 {initialStates} 个），" +
                $"球种 {balls.Count} 个，喷泉 {fountains.Length} 个，" +
                (enableWaves
                    ? "敌人 启用"
                    : "敌人 关闭（CombatRoot 的「是否刷敌人」未勾选：想要刷怪请在 Inspector 上勾上它）"));
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

        /// <summary>
        /// 掉落物被领取：<b>世界 → 玩家的通知</b>，按种类裁决给玩家什么。
        /// </summary>
        /// <remarks>
        /// <b>裁决在这里，不在掉落物里</b>（审查定的边界）：掉落物只发事实
        /// （"我被领走了，是什么、几个"），给玩家加什么由世界侧决定。
        /// 加一种掉落物时在这里加一个 <c>case</c> —— 玩家侧一行都不用改。
        /// </remarks>
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
