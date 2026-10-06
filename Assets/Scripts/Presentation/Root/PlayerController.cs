using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Player;
using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 玩家组合根：组装执行器、配置、活动区域与输入缓冲，并由 <c>GameRoot</c> 每个物理帧驱动一次
    /// <see cref="PlayerLogic"/>。
    /// </summary>
    /// <remarks>
    /// 全部环境事实只在本类组装一次。同一个物理帧内 <c>InputBuffer.Push</c> 必须先于 <c>Tick</c>，
    /// 否则按下沿会滞后一帧（<c>Docs/框架设计/分层设计/逻辑层.md</c> §5）。
    /// 顺序固定：消费快照 → 吸附到 8 向并归一化 → 以<b>同一份</b>归一化快照推缓冲 → 组装 <c>WorldInfo</c>
    /// → <c>Logic.FixedTick</c> → 边界钳位。
    /// 边界钳位放最后：它是物理步边界上的"保险丝"，防高速冲出地图；正常阻挡由刚体碰撞解算。
    /// <para><b>归一化只在这里做一次</b>：<c>WorldInfo.MoveDirection</c> 与 <c>InputBuffer</c> 里的快照
    /// 必须是同一份已归一化方向。曾出现"一个用处理后的值、一个用原始值"的写法——
    /// 同一物理帧里存在两份方向真值，正是"斜向快 √2 倍"与"冲刺方向不一致"这类
    /// 不报错、只错手感的缺陷的来源。</para>
    /// <para><b>收口前它是第二个自驱入口</b>（自己的 <c>FixedUpdate</c>）：它和
    /// <c>GameRoot.FixedUpdate</c>（战斗切片的物理帧通道）谁先跑由 Unity 决定，
    /// 而战斗切片的接触结算读的是"玩家这一帧提交后的位置"。现在物理帧只有一个发起者
    /// （<c>GameRoot</c>），顺序由 <see cref="Order"/> 明确写死：玩家侧（<c>-100</c>）先于世界侧（<c>0</c>）。</para>
    /// <para><b>两个帧相位各有一件事</b>：物理帧 = <see cref="FixedTick"/>（输入 → 逻辑 → 提交速度），
    /// 渲染帧 = <see cref="RenderTick"/>（瞄准 ＋ 开火意图）。瞄准是非物理逻辑，
    /// 而"屏幕 → 世界"这一步只有表现层做得了（只有它认识相机）—— 所以换算在这里，
    /// 吸附到哪一格由逻辑层算（<c>PlayerLogic.UpdateAim</c>）。</para>
    /// </remarks>
    public sealed class PlayerController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked
    {
        /// <summary>驱动顺序：玩家侧必须早于世界侧（先提交速度、先读输入）。</summary>
        public int Order => -100;

        [SerializeField] private PlayerConfig config = default;
        [SerializeField] private MovementMotor motor = default;
        [SerializeField] private InputProvider inputProvider = default;

        [Tooltip("瞄准用的相机。留空取 Camera.main（战斗场景里就是主相机，所以通常不用拖）")]
        [SerializeField] private Camera aimCamera = default;

        [Tooltip("地图活动区域：拖入覆盖可行走区域的 BoxCollider2D。不接则不钳位（不报错，调试面板会显示未接线）")]
        [SerializeField] private BoxCollider2D boundsArea = default;

        /// <summary>方向判零的容差：摇杆漂移与浮点残渣不该让角色每帧微动。</summary>
        private const float DirectionEpsilon = 1e-6f;

        /// <summary>8 向吸附的一档（弧度）。45° 一档共 8 档。</summary>
        private const float OctantRadians = 2f * Mathf.PI / 8f;

        /// <summary>吸附到 8 向时的一族单位方向，键为"档位序号化的弧度"，避免浮点直接比 key。</summary>
        private static readonly Dictionary<int, Vector2> Snapped = BuildSnappedTable();

        private InputBuffer _buffer;
        private WorldInfo _world;
        private BoundsArea _bounds;

        /// <summary>观感调参（瞄准平面深度）。<c>Start</c> 里读一次；缺失时用代码默认值。</summary>
        private ThrowTuning _tuning;

        /// <summary>是否已暂停（暂停时不瞄准、不开火 —— 输入被关掉，但指针采样并没有）。</summary>
        private bool _paused;

        /// <summary>出生点（<c>Awake</c> 时记一次）；打空重来时回到这里。</summary>
        private Vector2 _spawnPoint;

        /// <summary>注册时抓住的 GameRoot 引用；销毁期只经它退订（理由见 <see cref="OnDestroy"/>）。</summary>
        private GameRoot _root;

        /// <summary>逻辑层入口，供调试面板读取。</summary>
        public PlayerLogic Logic { get; private set; }

        /// <summary>物理体位置（世界侧判接触、算重生点用物理体而不是 <c>transform</c>）。</summary>
        public Vector2 Position => motor == null ? Vector2.zero : motor.Position;

        /// <summary>本物理帧喂进逻辑层的环境事实。</summary>
        public WorldInfo World => _world;

        /// <summary>引擎回读速度，与逻辑层的"提交后预期"对照；它滞后一个物理步。</summary>
        public Vector2 EngineVelocity => motor == null ? Vector2.zero : motor.Velocity;

        /// <summary>
        /// 把原始输入吸附到 8 向并归一化：键盘同时按两个轴会得到模长 √2，摇杆则是任意角度。
        /// </summary>
        /// <param name="move">原始输入（−1..1，已 <c>ClampMagnitude(1)</c>）。</param>
        /// <param name="snapToEightDirections">是否吸附；关掉时只补归一化。</param>
        /// <returns>零输入 → <c>Vector2.zero</c>；否则 → <b>模长恒为 1</b> 的方向。</returns>
        /// <remarks>
        /// <b>契约：非零输出的模长一定是 1</b>（<c>WorldInfo.MoveDirection</c> 与逻辑层都依赖它）。
        /// 因此"吸附到 45° 的一档"与"抹掉摇杆的模拟幅度"是同一件事——轻推摇杆与推满是同一个速度。
        /// 本条是<b>静态纯函数</b>（不吃实例状态、不吃 <c>Time</c>），所以 EditMode 测试能直接调它，
        /// 把"斜向不得快 √2 倍"这条断言真正钉在代码上，而不是钉在注释上。
        /// </remarks>
        public static Vector2 SnapMoveToEightDirections(Vector2 move, bool snapToEightDirections)
        {
            if (move.sqrMagnitude <= DirectionEpsilon) return Vector2.zero;

            // 关掉吸附时也不能原样放行：斜向的 (1,1) 模长是 √2，会当帧写出快 41% 的速度。
            if (!snapToEightDirections) return move.normalized;

            // 45° 一档共 8 档；Round 天然把 ±22.5° 内的输入归到最近一档，不需要额外死区。
            // 取整 + 按档位查表（而不是直接 Cos/Sin 算值）是为了让"同一个方向"在不同输入下得到<b>逐位相同</b>的结果。
            int octant = Mathf.RoundToInt(Mathf.Atan2(move.y, move.x) / OctantRadians);
            octant %= 8;
            if (octant < 0) octant += 8;

            return Snapped[octant];
        }

        private static Dictionary<int, Vector2> BuildSnappedTable()
        {
            var table = new Dictionary<int, Vector2>(8);

            for (int octant = 0; octant < 8; octant++)
            {
                float radians = octant * OctantRadians;
                table[octant] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }

            return table;
        }

        private void OnEnable()
        {
            EventBus<GamePaused>.Subscribe(OnGamePaused);
            EventBus<GameResumed>.Subscribe(OnGameResumed);
        }

        private void OnDisable()
        {
            EventBus<GamePaused>.Unsubscribe(OnGamePaused);
            EventBus<GameResumed>.Unsubscribe(OnGameResumed);
        }

        private void OnGamePaused(GamePaused evt)
        {
            _paused = true;

            inputProvider.SetInputEnabled(false); // 内部已 Clear，无需再调一次
            _buffer.Clear();
        }

        private void OnGameResumed(GameResumed evt)
        {
            _paused = false;

            inputProvider.SetInputEnabled(true);
        }

        /// <summary>
        /// 回到出生点并满血（打空重来）。
        /// </summary>
        /// <remarks>
        /// <b>为什么这个口子在本类：</b>出生点是<b>本类的环境事实</b>（<c>Awake</c> 时记下的物理体位置），
        /// 而"该不该重来"是世界侧的事（<c>CombatRoot</c> 数重试延时、清场）。
        /// 于是分工是：世界侧决定"什么时候重来"，玩家侧执行"回到哪、满血、停住"。
        /// <para>边界钳位：出生点理论上在图内，但它是运行期读到的位置；越界时钳回来，
        /// 否则玩家会回到一张"看不见自己"的地图外（与 <c>FixedTick</c> 末尾那条保险丝同源）。</para>
        /// </remarks>
        public void RespawnToSpawn()
        {
            if (Logic == null) return;

            Vector2 position = _spawnPoint;

            if (_bounds.TryClamp(position, out Vector2 clamped)) position = clamped;

            Logic.RespawnTo(position);
        }

        private void Awake()
        {
            if (config == null
                || motor == null
                || inputProvider == null)
            {
                Debug.LogError("PlayerController 引用未接线（config / motor / inputProvider），已停用。", this);
                enabled = false;
                return;
            }

            // 缓冲容量取"容量参数"与各输入窗口的较大者：容量小于任何窗口时，窗口内的按下会被挤出历史。
            _buffer = new InputBuffer(
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            _bounds = ReadBounds();
            _world = new WorldInfo(Vector2.zero, in _bounds); // 首帧前也不留 default
            _spawnPoint = motor.Position;

            if (!_bounds.IsValid)
            {
                Debug.LogWarning(
                    "PlayerController.boundsArea 未接线、被停用或尺寸为 0：玩家不会被限制在地图边界内。",
                    this
                    );
            }
        }

        private void Start()
        {
            // 场景根自己报到：GameRoot 按 Order 装配并驱动，不再由 Inspector 拖引用
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的出来
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        /// <summary>
        /// 装配玩家逻辑。<b>由 <c>GameRoot</c> 在第一个被驱动的帧调</b>（见 <see cref="ISceneRoot.Attach"/>）。
        /// </summary>
        /// <remarks>
        /// <b>为什么读表放在这里而不是 <c>Awake</c>：</b><c>SpecCatalog</c> 要经
        /// <c>ConfigModule</c>，而配表由 <c>GameRoot.Awake</c> 装配 —— 组件之间的 <c>Awake</c>
        /// 顺序 Unity 不保证，写在 <c>Awake</c> 里就是一次"看运气"的启动崩溃
        /// （<c>ConfigModule.Tables</c> 在未就绪时会抛）。
        /// <para>同一行表值由本类读一次，世界侧经 <c>Logic.Spec</c> 拿：
        /// 改列名的影响面因此只落在这一处。</para>
        /// </remarks>
        public void Attach()
        {
            if (Logic != null || _buffer == null || motor == null) return;

            PlayerSpec spec = SpecCatalog.Player();

            Logic = new PlayerLogic(motor, config, _buffer, in spec);

            // 观感调参（瞄准平面深度）：配置缺失时 LoadOrDefault 会给一份默认值 ＋ 一条 Warning
            _tuning = ThrowTuning.LoadOrDefault();
        }

        /// <summary>
        /// 物理帧：由 <c>GameRoot.FixedUpdate</c> 按 <see cref="Order"/> 驱动。
        /// </summary>
        /// <remarks>
        /// 收口前这是本类自己的 <c>FixedUpdate</c>（四个驱动入口之一）。改成被驱动之后，
        /// "物理帧里玩家先于战斗结算"从"Unity 抽签"变成代码事实。
        /// </remarks>
        public void FixedTick(float deltaTime)
        {
            // 装配失败（引用未接线）或还没装配时整体 no-op：不读半装配状态
            if (Logic == null) return;

            InputSnapshot raw = inputProvider.ConsumeSnapshot();

            Vector2 move = SnapMoveToEightDirections(raw.Move, config.snapToEightDirections);

            // 归一化后的方向要写回快照：WorldInfo 与 InputBuffer 必须是同一份方向，
            // 否则 MoveGroup 取冲刺方向、PlayerLogic 记"最近朝向"时会看到另一份（未处理的）值。
            var snapshot = new InputSnapshot(move, raw.JumpPressed, raw.DashPressed, raw.GrabHeld);

            _buffer.Push(in snapshot, Time.fixedTime);

            _world = new WorldInfo(move, in _bounds);

            Logic.FixedTick(
                new LogicContext(
                    Time.fixedTime,
                    Time.fixedDeltaTime,
                    in _world,
                    in snapshot
                )
            );

            // 保险丝：只在真的越界时写位置，避免每帧打断刚体的位置积分。
            if (_world.Bounds.TryClamp(motor.Position, out Vector2 clamped))
            {
                motor.SetPosition(clamped);
            }
        }

        /// <summary>
        /// 渲染帧：瞄准 ＋ 开火意图（由 <c>GameRoot.Update</c> 按 <see cref="Order"/> 驱动）。
        /// </summary>
        /// <remarks>
        /// <b>为什么瞄准在这里而不在物理帧：</b>帧相位口径是"物理帧只放角色移动与参与物理的逻辑"，
        /// 而瞄准既不吃物理也不产出物理量。
        /// <para><b>"屏幕 → 世界"这一步只有本类做得了</b>（只有表现层认识相机）：
        /// 换算完把<b>世界点</b>交给逻辑层，吸附到哪一格由 <c>TileAim</c> 算 ——
        /// 于是"看着能扔到、其实扔不到"这条缺陷的根源（两处各算一次）从结构上消失。</para>
        /// <para><b>暂停 / 菜单下不瞄准也不开火</b>：判据取"输入开关"（<c>InputProvider.IsInputEnabled</c>）
        /// 而不是自己记一个暂停标志 —— 暂停事件是一次发布，订阅晚了的组件永远收不到，
        /// 而"这一帧能不能读输入"必须每帧都答得对。</para>
        /// </remarks>
        public void RenderTick(float deltaTime)
        {
            if (Logic == null) return;

            if (_paused || (inputProvider != null && !inputProvider.IsInputEnabled))
            {
                // 收起高亮（发布一条"没有瞄准"的事实，去重后最多发一次）
                Logic.ClearAim();
                return;
            }

            Camera camera = aimCamera != null ? aimCamera : Camera.main;

            if (camera == null || inputProvider == null) return;

            float now = Time.time;

            Logic.UpdateAim(AimWorldPoint(camera), now);

            // 攻击输入：动作表里没有攻击动作，它由 InputProvider 直读指针产出（渲染帧语义）。
            // 主攻击 = 水球（吃弹药），副攻击 = 土球（不吃）。
            if (inputProvider.AttackPressedThisFrame) Logic.RequestThrow(BallType.Water, now);
            if (inputProvider.AltAttackPressedThisFrame) Logic.RequestThrow(BallType.Earth, now);
        }

        /// <summary>
        /// 屏幕点 → 世界点。
        /// </summary>
        /// <remarks>
        /// <b>不读相机的 z：</b><c>Camera.main.transform.position.z</c> 被 Cinemachine 每帧驱动，
        /// 依赖它等于让落点跟着相机插件走。正交相机下给一个足够大的常量深度即可
        /// （见 <c>ThrowTuning.cameraPlaneDepth</c>）。
        /// </remarks>
        private Vector2 AimWorldPoint(Camera camera)
        {
            Vector2 screen = inputProvider.AimScreen;

            float depth = _tuning != null ? _tuning.cameraPlaneDepth : 100f;

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

            return new Vector2(world.x, world.y);
        }

        /// <summary>
        /// 把场景里的活动区域碰撞体折算成纯数据矩形。
        /// </summary>
        /// <remarks>
        /// <b>这一步是逻辑层"零引擎类型"的代价，也是它的收益</b>：<c>BoxCollider2D</c> → <c>min/max</c>
        /// 的折算只发生在表现层这一处，逻辑层拿到的永远是纯数据。
        /// 每次 <c>Awake</c> 读一次即可：<c>BoxCollider2D.bounds</c> 是只读的派生值，
        /// 地图尺寸在运行期不会变（改了尺寸要重进场景）。
        /// </remarks>
        private BoundsArea ReadBounds()
        {
            if (boundsArea == null || !boundsArea.enabled) return default;

            Bounds b = boundsArea.bounds;

            return new BoundsArea(b.min, b.max);
        }
    }
}
