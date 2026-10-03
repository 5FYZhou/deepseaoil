using System;
using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 白模的组合根：读鼠标、建出指示器与球、结算落地冲量，并把敌人、泥浆与玩家血量装起来。
    /// <b>整个白模只需要在场景里挂这一个组件。</b>
    /// </summary>
    /// <remarks>
    /// <b>为什么只有一个组件：</b>白模的失败模式绝大多数是"忘了接线"（<c>移动Tests.cs</c> 头部记录过这一课：
    /// 曾有一个自动接线检查菜单，报了 31 项全部误报，因为假红会训练人忽略它）。本类的做法是
    /// <b>把接线从"人工拖引用"改成"运行期建对象"</b>：指示器、球、阴影、瞬闪、敌人、泥浆、玩家血量
    /// 全部由这里 <c>new GameObject</c> ＋ <c>AddComponent</c> 建出，依赖由构造函数传入。
    /// 于是场景里只剩一个引用要拖（玩家）。
    /// <para><b>输入为什么不进 <c>InputSys.inputactions</c>：</b>那个资产会被重新导入并整份覆盖
    /// <c>Generated/Input/InputSys.cs</c>（95KB），白模改一次会让真实改动淹没在生成物 diff 里；
    /// 而且需求书第六节已定白模"不污染三层"，却要动三层共用的输入资产，自相矛盾。
    /// 所以本类直读 <c>Mouse.current</c>，代价是白模不支持手柄与键位重绑 —— 白模不需要。</para>
    /// <para><b>冲量为什么放在 <c>FixedUpdate</c>：</b>见 <see cref="FixedUpdate"/>。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ThrowSpawner : MonoBehaviour
    {
        [Tooltip("玩家。留空则自动找场景里的 PlayerController；两者都没有时本组件停用并报错。")]
        [SerializeField] private Transform player = default;

        [Tooltip("球与阴影在层次里的父物体。留空则直接在根下建。")]
        [SerializeField] private Transform ballRoot = default;

        [Tooltip("敌人、泥浆与玩家血量的父物体。留空则直接在根下建。")]
        [SerializeField] private Transform enemyRoot = default;

        private AimIndicator _aim;
        private Camera _camera;
        private PlayerController _controller;
        private EnemyDirector _director;
        private bool _paused;

        /// <summary>
        /// 落地冲量队列：<c>Update</c> 入队、<c>FixedUpdate</c> 出队。
        /// </summary>
        /// <remarks>
        /// 一个固定步里可能结算多颗球，所以是队列而不是单个字段 —— 用单字段会静默丢掉一颗球的冲量。
        /// <para>带上球编号：结算方要靠它判断"这颗球是不是已经打中过这个目标"，
        /// 见 <see cref="Damage.BallId"/>。</para>
        /// </remarks>
        private readonly Queue<(Vector2 Point, BallType Type, int BallId)> _pendingKicks =
            new Queue<(Vector2, BallType, int)>();

        /// <summary>下一颗球的编号。只增不减，所以"这颗球打过了吗"是一个无状态比较。</summary>
        /// <remarks>
        /// 从 1 起：0 留给"没有球"这种默认值，虽然这里用不到，但让"编号 0"只代表一种东西更省事。
        /// </remarks>
        private int _nextBallId = 1;

        /// <summary>
        /// 把目标点夹到离出手点 <paramref name="maxDistance"/> 以内。<b>静态纯函数</b>。
        /// </summary>
        /// <param name="origin">出手点（玩家位置）。</param>
        /// <param name="target">原始目标点（鼠标世界位置）。</param>
        /// <param name="maxDistance">最大投掷距离。非法值（<c>NaN</c> / 无穷 / <c>≤ 0</c>）会被换成
        /// <see cref="ThrowConstants.MAX_THROW_DISTANCE"/>，见下。</param>
        /// <param name="distance">输出<b>实际飞行距离</b>（已夹到上限以内）。
        /// <see cref="BallData"/> 的时长要靠它，见其构造函数注释。</param>
        /// <returns>夹好之后的落点（<b>贴地坐标，不含</b>出手抬高量）。</returns>
        /// <remarks>
        /// 做成静态纯函数是为了让"指示器画的位置"与"球真正落的位置"可以共用一次 clamp：
        /// 两处各写一份数学必然会漂移，而这种漂移的表现是"看着能扔到、其实扔不到"，是最难查的一类手感缺陷。
        /// 顺带它也就成了一个能吃 EditMode 测试的对象（<c>白模抛球Tests</c> W3/W4/W6/W7）。
        /// <para><b><paramref name="distance"/> 必须是夹过的值，不能是原始距离。</b>它同时充当
        /// <see cref="BallData"/> 的时长缩放因子（飞得远就飞得久），一旦这里回的是"鼠标到玩家的原始距离"，
        /// 最远一投的飞行时长会放大到 <c>原始距离 / 最大距离</c> 倍 —— 鼠标在屏幕边缘时球要飞十几秒，
        /// 而落点看着完全正常，只有"球飘在半空不走"这一个症状。</para>
        /// <para><b>为什么要挡非法上限：</b><c>NaN</c> 参与任何比较都是 <c>false</c>，所以
        /// <c>distance &lt;= NaN</c> 为假、函数一路走到乘法，把 <c>NaN</c> 传染给整个落点。
        /// 那会生成一颗坐标是 NaN 的球 —— 它的 <c>t</c> 仍会推进到 1 并正常销毁，但位置计算全是 NaN，
        /// 表现为球凭空消失。上限是常量，出现非法值只可能是调用方写错；
        /// 这里<b>不抛异常</b>（白模不该因为一个数字崩掉、也不该把 Play 打断），而是退回默认值，
        /// 让现象变成"能看出不对但不崩"。</para>
        /// </remarks>
        public static Vector2 ClampThrowPoint(Vector2 origin, Vector2 target, float maxDistance, out float distance)
        {
            // 非法上限的保底：<c>NaN</c> 参与任何比较都是 false，所以 <c>distance &gt; NaN</c> 与
            // <c>distance &lt;= NaN</c> 同时为假，函数会一路走到乘法，把 NaN 传染给整个落点 ——
            // 球会带着 NaN 坐标生成、然后永远不落地（t 永远不会 >= 1 之外的行为都失效）。
            // 上限本来是常量，出现 NaN 只可能是调用方写错；这里不抛异常（白模不该因为一个数字崩掉），
            // 而是退回默认最远距离，让现象变成"能看出不对但不崩"。
            if (float.IsNaN(maxDistance) || float.IsInfinity(maxDistance) || maxDistance <= 0f)
                maxDistance = ThrowConstants.MAX_THROW_DISTANCE;

            Vector2 delta = target - origin;
            distance = delta.magnitude;

            if (distance <= maxDistance) return target;

            distance = maxDistance;

            return origin + delta / delta.magnitude * maxDistance;
        }

        private void Start()
        {
            if (player == null)
            {
                // 与 Logic 层及 Singleton 一致的写法；找不到就报错停用，不静默留一个不会投球的场景。
                PlayerController controller = FindObjectOfType<PlayerController>();
                if (controller != null) player = controller.transform;
            }

            if (player == null)
            {
                Debug.LogError("ThrowSpawner.player 未接线，且场景里找不到 PlayerController，已停用。", this);
                enabled = false;
                return;
            }

            _camera = Camera.main;

            if (_camera == null)
            {
                // 不静默降级：没有相机就没法把鼠标位置换成世界落点，静默的后果是"鼠标怎么动都没反应"。
                Debug.LogError("场景里没有 tag 为 MainCamera 的相机，ThrowSpawner 已停用。", this);
                enabled = false;
                return;
            }

            // 血量与击退都必须拿到玩家的逻辑层入口：拿不到就整套敌人不刷（让它变成"看得见的缺"，而不是静默不出血）。
            _controller = player.GetComponent<PlayerController>();

            if (_controller == null || _controller.Logic == null)
            {
                Debug.LogError(
                    "ThrowSpawner.player 指向的物体上没有可用的 PlayerController（或它的逻辑层还没组装），" +
                    "玩家血量与敌人都不会生效。",
                    this
                    );
            }

            CreateAimIndicator();

            EventBus<GamePaused>.Subscribe(OnPaused);
            EventBus<GameResumed>.Subscribe(OnResumed);

            _director = CreateEnemyDirector();
            CreatePlayerHealth();
        }

        /// <summary>
        /// 建出敌人调度器。**敌人、泥浆、玩家血量都由本类建出，场景里不需要多挂组件。**
        /// </summary>
        /// <remarks>
        /// 返回 <c>null</c> 而不是抛异常：玩家逻辑层缺失时"没有敌人"是可解释的降级，
        /// 白模仍然能验投掷链路（那才是第一阶段的东西）—— 不该因为第二阶段的新功能把整个白模废掉。
        /// </remarks>
        private EnemyDirector CreateEnemyDirector()
        {
            var go = new GameObject("敌人调度");

            go.transform.SetParent(enemyRoot, true);

            var director = go.AddComponent<EnemyDirector>();

            director.Initialize(player, new EnemyConfig(), ThrowConstants.CHASE_INITIAL_DELAY);

            return director;
        }

        private void CreatePlayerHealth()
        {
            if (_controller == null || _controller.Logic == null) return;

            Vector3 p = player.position;

            var go = new GameObject("玩家血量");

            go.transform.SetParent(enemyRoot, true);

            var health = go.AddComponent<PlayerHealth>();

            health.Initialize(_controller, new Vector2(p.x, p.y), _director);
        }

        private void OnDestroy()
        {
            EventBus<GamePaused>.Unsubscribe(OnPaused);
            EventBus<GameResumed>.Unsubscribe(OnResumed);
        }

        private void OnPaused(GamePaused evt)
        {
            _paused = true;

            if (_aim != null) _aim.SetVisible(false);
        }

        private void OnResumed(GameResumed evt)
        {
            _paused = false;

            if (_aim != null) _aim.SetVisible(true);
        }

        private void Update()
        {
            // 暂停时连输入一起停：PauseService 把 timeScale 设成 0，在飞的球自己会冻结，
            // 但 WasPressedThisFrame 与 timeScale 无关 —— 不显式挡掉就会出现"暂停时还能投球"。
            if (_paused) return;

            Vector2 playerPos = PlayerPosition();

            if (_aim != null) _aim.FollowMouse(playerPos);

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                if (_aim != null) _aim.SetVisible(false);
                return;
            }

            if (_aim != null) _aim.SetVisible(true);

            // 两个 if 而不是 else if：同一帧左右键各按一次是两次独立操作，不该吞掉其中一次。
            if (mouse.leftButton.wasPressedThisFrame) Throw(PlayerPosition(), BallType.Water);
            if (mouse.rightButton.wasPressedThisFrame) Throw(PlayerPosition(), BallType.Earth);
        }

        /// <summary>
        /// 结算落地冲量。<b>必须在 <c>FixedUpdate</c> 里做，不能在 <c>Update</c> 里做。</b>
        /// </summary>
        /// <remarks>
        /// 理由不是"提前量"这类模糊的说法，而是三条可验证的物理时序事实：
        /// <list type="number">
        /// <item><c>Rigidbody2D.velocity</c> 与 <c>AddForce(ForceMode2D.Impulse)</c> 都只在
        /// <c>FixedUpdate</c> 与下一个 <c>Physics2D.Simulate</c> 之间生效；在 <c>Update</c> 里写速度会在
        /// 多个渲染帧后的大固定步里被一次性消费掉，表现是"推得莫名其妙地远"。</item>
        /// <item><c>PlayerController.FixedUpdate</c> 每帧帧首重新读引擎速度当基准（速度账本），
        /// 所以冲量无论算在账本取基准之前还是之后，都只是"下一帧的起步速度里已经有它" ——
        /// 不产生第二个速度写者，账本不会自相矛盾。</item>
        /// <item>冲量是<b>一次</b>施加（不是持续力），所以只影响紧接的那一个物理步。</item>
        /// </list>
        /// </remarks>
        private void FixedUpdate()
        {
            while (_pendingKicks.Count > 0)
            {
                var (point, type, ballId) = _pendingKicks.Dequeue();

                ApplyKick(point, ColorFor(type), type, ballId);
            }
        }

        /// <summary>结算一次落地：给附近的角色结算伤害、给物理体冲量、水球留泥、地面闪一圈。</summary>
        /// <param name="point">落点。</param>
        /// <param name="color">该球种的颜色（瞬闪用）。</param>
        /// <param name="type">球种。</param>
        /// <param name="ballId">造成这次落地的球的编号，透传给伤害结算做去重。</param>
        /// <remarks>
        /// <b>顺序是有意的：先角色伤害、后通用刚体冲量。</b>两者查的是同一个半径，
        /// 但"角色"走的是各自的<b>速度账本</b>（<c>EnemyActor.TakeDamage</c> /
        /// <c>PlayerController</c> 的受击口），而"通用刚体"是无生命物体。
        /// 不分这两条路的话，<c>Rigidbody2D.AddForce</c> 会在账本写出速度之后<b>又写一次</b>速度 ——
        /// 每帧两个速度写者，正是文档里（<c>Docs/toAgent/白模接入.md</c>）记的那笔技术债。
        /// 这一次把它还掉了：白模里只剩下无生命物体走直接写刚体那条路。
        /// </remarks>
        private void ApplyKick(Vector2 point, Color color, BallType type, int ballId)
        {
            // 注意：Physics2DSettings 里 m_QueriesHitTriggers = 1，所以 trigger 也会被查出来。
            // 白模阶段场景里没有任何 trigger 区域，因此不叠 ContactFilter2D；将来加了触发区（采集区、毒圈），
            // 这里必须补 layerMask，否则球会隔着老远把触发区"推一下"。
            //
            // 查询半径**故意比作用半径大**（+ PUSH_QUERY_MARGIN）：OverlapCircleAll 查的是碰撞体，
            // 而真实判定是按"到碰撞体最近点的距离"逐个做的（见 DamageEnemies / PushBodies）。
            // 查询只负责"别漏掉候选" —— 先过滤后判定，不能反过来，否则"圆心在半径内但碰撞体很大"
            // 的目标会被漏掉。放宽查询不会让作用范围变大，判定不看它。
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, ThrowConstants.IMPULSE_RADIUS + ThrowConstants.PUSH_QUERY_MARGIN);

            bool touchedEnemy = DamageEnemies(point, type, ballId, hits);

            if (type == BallType.Water) SpawnMud(point);

            PushBodies(point, hits);

            SpawnLandingFlash(point, color, type);

            // 只有"没打到任何敌人"时才记这条日志：打到敌人时 EnemyActor 自己会记一条更具体的
            // （剩余耐久），两条都打会让 Console 里一次落地出现两行，反而看不出打了几个。
            if (!touchedEnemy)
            {
                Debug.Log($"[白模] 落地：坐标 ({point.x:F2}, {point.y:F2})，种类 {type}，半径内没有角色");
            }
        }

        /// <summary>给半径内的敌人各结算一次命中。</summary>
        /// <param name="point">落点。</param>
        /// <param name="type">球种。</param>
        /// <param name="ballId">球的编号：同一个敌人被同一颗球查到多次时，只有第一次算（见 <c>EnemyActor.TakeDamage</c>）。</param>
        /// <param name="hits">已经查好的候选碰撞体（放宽过的查询，复用以免同一帧查两次）。</param>
        /// <returns>是否至少打到了一个敌人。</returns>
        /// <remarks>
        /// <b>"打到"与"打退多少"是两件事，由 <see cref="Damage.For"/> 分开算：</b>
        /// 那颗球只有 1 点耐久可打，但该给多少冲量取决于球种与受害者站没站在泥里。
        /// <para><b>判定按"敌人<b>圆心</b>到落点的距离"</b>，不按"碰撞体有没有和圈相交"。
        /// 后者是 <c>OverlapCircleAll</c> 的天然语义，而它比画出来的圈**大**一个敌人半径
        /// （0.45）—— 圈外半米也会被打到，那正是"指示范围与实际范围对不上"的一半原因。
        /// 改成点距之后，"画出来的圈"就是"打得到的圈"。</para>
        /// <para>去重仍然交给 <c>EnemyActor</c>（同一个敌人可能有多个碰撞体，这里过滤不了）：
        /// 判断"这颗球是否已经打中过它"是受害者自己的状态，不是施害方该猜的。</para>
        /// </remarks>
        private bool DamageEnemies(Vector2 point, BallType type, int ballId, Collider2D[] hits)
        {
            bool touched = false;

            // 泥浆列表查一次、给所有敌人共用：每个敌人各查一次的话，四只敌人就有四次
            // FindObjectsOfType，而它的开销与场上对象总数成正比。
            MudPatch[] patches = FindObjectsOfType<MudPatch>();

            for (int i = 0; i < hits.Length; i++)
            {
                EnemyActor enemy = hits[i].GetComponentInParent<EnemyActor>();

                if (enemy == null || enemy.IsDead) continue;

                var victim = new Vector2(enemy.transform.position.x, enemy.transform.position.y);

                // 精确判定：超出作用半径就不算打到（查询放宽过，候选里必然有圈外的）。
                if (Vector2.Distance(point, victim) > ThrowConstants.IMPULSE_RADIUS) continue;

                // 泥浆减速系数按"这一脚踩没踩在泥里"取：站在旧泥浆里的敌人被砸，击退也会按泥浆折减。
                float slow = MudPatch.StrongestSlow(victim, patches);

                enemy.TakeDamage(Damage.For(point, victim, type, slow, ballId));

                touched = true;
            }

            return touched;
        }

        /// <summary>给半径内的无生命刚体一次冲量。</summary>
        /// <param name="point">落点。</param>
        /// <param name="hits">放宽过的候选碰撞体。</param>
        /// <remarks>
        /// <b>跳过角色：</b>它们的速度由自己的账本写（见 <see cref="ApplyKick"/> 的注释）。
        /// 判据是"挂不挂 <c>EnemyActor</c>"，而玩家不是 <c>EnemyActor</c> ——
        /// 所以玩家照旧走这条路，行为与第一阶段一致（第一阶段手测过 1–8 条验收项，不能改）。
        /// <para><b>判定按"落点到碰撞体最近点的距离"</b>：用 <c>Collider2D.ClosestPoint</c> 而不是
        /// 刚体圆心 —— 一个大箱子的圆心可能在 2 米外、边缘却贴着落点，按圆心判会把它漏掉。
        /// 这与 <c>OverlapCircleAll</c> 的"相交"语义很接近，但<b>精确</b>：
        /// 相交只要有任何一点进入圈内就算，而本判据是"最近点在圈内"，两者只在退化情形下不同
        /// （比如落点正好在碰撞体内部时最近点就是它自己）。</para>
        /// </remarks>
        private static void PushBodies(Vector2 point, Collider2D[] hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                // 从碰撞体往父级找刚体：碰撞体常挂在子节点上，只查自身会漏掉整片角色。
                Rigidbody2D body = hits[i].GetComponentInParent<Rigidbody2D>();

                if (body == null || body.isKinematic) continue;

                // 敌人有自己的速度账本，这不归物理引擎管。
                if (hits[i].GetComponentInParent<EnemyActor>() != null) continue;

                // 精确判定：取碰撞体上离落点最近的点，它到落点的距离必须落在作用半径内。
                Vector2 nearest = hits[i].ClosestPoint(point);

                if (Vector2.Distance(point, nearest) > ThrowConstants.IMPULSE_RADIUS) continue;

                Vector2 delta = body.position - point;

                // 正中心命中时方向为零，兜底给"上"：不该让站在落点正中的人免疫击退。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                // 冲量是动量而非速度：按质量折算，让"轻的飞得远、重的推不动"自然成立。
                // 质量下限防的是"质量趋近 0 时速度趋于无穷"。
                float momentum = ThrowConstants.IMPULSE_STRENGTH * Mathf.Max(body.mass, ThrowConstants.IMPULSE_MASS_FLOOR);
                body.AddForce(direction * momentum, ForceMode2D.Impulse);

                // 硬上限：同一个物理步里被多颗球叠推时，速度不该累加到穿模。
                body.velocity = Vector2.ClampMagnitude(body.velocity, ThrowConstants.IMPULSE_STRENGTH);
            }
        }

        /// <summary>在落点留一片泥浆（水球落地）。</summary>
        /// <remarks>
        /// 泥浆的时长与强度都不在这里判断：时长由 <see cref="ThrowConstants.MUD_DURATION"/> 给
        /// <c>MudPatch</c>，强度由 <c>MudPatch.StrongestSlow</c> 读常量。
        /// 本方法只负责"建一个物体、摆到落点、挂上组件"。
        /// </remarks>
        private void SpawnMud(Vector2 point)
        {
            var go = new GameObject("泥浆");

            go.transform.SetParent(enemyRoot, true);
            go.transform.position = new Vector3(point.x, point.y, 0f);

            var mud = go.AddComponent<MudPatch>();

            mud.Initialize(ThrowConstants.MUD_RADIUS_METERS, ThrowConstants.MUD_DURATION);
        }

        /// <summary>投一颗球。</summary>
        private void Throw(Vector2 origin, BallType type)
        {
            Vector2 rawTarget = MouseWorldPoint();

            Vector2 target = ClampThrowPoint(origin, rawTarget, ThrowConstants.MAX_THROW_DISTANCE, out float distance);

            // 距离由这里算、并由 BallData 沿用，两处不会各算一遍（见 BallData 构造函数的注释）。
            var data = new BallData(type, origin, target, distance);

            // 编号在这里分配、随球一路带到落地结算：它是"同一颗球只算一次"的判据。
            int ballId = _nextBallId++;

            CreateBall(ballId, data);

            Debug.Log($"[白模] 投出 {type} #{ballId}，落点 ({target.x:F2}, {target.y:F2})，距离 {distance:F2}，飞行 {data.Duration:F3}s");
        }

        /// <summary>建球根物体，并按固定顺序建阴影与球本体。</summary>
        private void CreateBall(int ballId, BallData data)
        {
            Color color = ColorFor(data.Type);

            var go = new GameObject($"球_{data.Type}");

            // 保持世界坐标：即使 ballRoot 带着位移，球也不会跟着偏。
            go.transform.SetParent(ballRoot, true);

            // 根物体是**悬空**的：贴地位置再抬一个出手高度。
            // 球的视觉子物体用 localPosition 叠加高度，所以会继承这个抬高量（正是我们要的）；
            // 而阴影自己写世界坐标，因此不会被它带上去（见 BallShadow.Apply 的注释）。
            go.transform.position = new Vector3(
                data.Start.x,
                data.Start.y + ThrowConstants.THROW_ORIGIN_HEIGHT,
                0f
                );

            var view = go.AddComponent<BallView>();
            view.Initialize(ThrowConstants.BALL_SORTING_ORDER, ThrowConstants.BALL_RADIUS_METERS * 2f, color);

            // 先 view 再 shadow：AddComponent 会立刻跑子物体的 Awake，顺序写死才不会让两帧的顺序飘。
            var shadowGo = new GameObject("阴影");
            shadowGo.transform.SetParent(go.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(ThrowConstants.BALL_SORTING_ORDER - 1, ThrowConstants.SHADOW_RADIUS_METERS * 2f, new Color(0f, 0f, 0f, 0.35f));

            var driver = go.AddComponent<BallDriver>();
            driver.Initialize(ballId, data, view, shadow, OnBallLanded);
        }

        /// <summary>
        /// 在落点画范围圈：内圈 = 击退生效半径，外圈 = 该球种的效果范围。
        /// </summary>
        /// <param name="point">落点。</param>
        /// <param name="color">该球种的颜色（内圈用）。</param>
        /// <param name="type">球种 —— 决定外圈多大。</param>
        /// <remarks>
        /// <b>两个环的半径各自精确等于一件事的生效半径，这不是装饰。</b>
        /// 只有一颗球的时候"圈是 1.2 但泥浆铺到 1.8"看起来就是指示与实际对不上；
        /// 画两个圈之后，玩家能直接读出"这一下打退到哪、泥铺到哪"。
        /// <para>外圈的半径按球种取：土球与击退同半径（都只有击退这一件事），
        /// 水球取 <see cref="ThrowConstants.MUD_RADIUS_METERS"/>。两个圈全部由常量算出，
        /// 没有一处硬编码的数字。</para>
        /// </remarks>
        private void SpawnLandingFlash(Vector2 point, Color color, BallType type)
        {
            var go = new GameObject("落地瞬闪");

            go.transform.SetParent(ballRoot, true);
            go.transform.position = new Vector3(point.x, point.y, 0f);

            float effectRadius = type == BallType.Water
                ? ThrowConstants.MUD_RADIUS_METERS
                : ThrowConstants.IMPULSE_RADIUS;

            var flash = go.AddComponent<LandingFlash>();

            flash.Initialize(
                ThrowConstants.IMPULSE_RADIUS,
                color,
                effectRadius,
                ThrowConstants.LANDING_FLASH_OUTER_COLOR,
                ThrowConstants.LANDING_FLASH_SECONDARY_DELAY,
                ThrowConstants.LANDING_FLASH_DURATION
                );
        }

        /// <summary>
        /// 落地回调：只入队，不在这里碰物理（原因见 <see cref="ApplyKick"/> 与 <see cref="FixedUpdate"/> 的注释）。
        /// </summary>
        /// <param name="point">落点（贴地）。</param>
        /// <param name="type">球种。</param>
        /// <param name="ballId">球编号，原样带进结算队列。</param>
        private void OnBallLanded(Vector2 point, BallType type, int ballId)
        {
            _pendingKicks.Enqueue((point, type, ballId));

            Debug.Log($"[白模] 落地：球 #{ballId}，坐标 ({point.x:F2}, {point.y:F2})，种类 {type}");
        }

        private Vector2 PlayerPosition()
        {
            Vector3 p = player.position;

            return new Vector2(p.x, p.y);
        }

        private Vector2 MouseWorldPoint()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null || _camera == null) return PlayerPosition();

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, ThrowConstants.CAMERA_PLANE_DEPTH));

            return new Vector2(world.x, world.y);
        }

        private void CreateAimIndicator()
        {
            var go = new GameObject("落点指示器");

            go.transform.SetParent(ballRoot, true);

            _aim = go.AddComponent<AimIndicator>();
            _aim.Initialize(_camera);
        }

        /// <summary>球种 → 颜色。这里是硬编码，理由见 <see cref="BallType"/> 的注释（需求书第 8 节第 3 题未答复）。</summary>
        private static Color ColorFor(BallType type)
        {
            return type == BallType.Water ? ThrowConstants.WATER_BALL_COLOR : ThrowConstants.EARTH_BALL_COLOR;
        }
    }
}
