using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Player;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 玩家组合根：组装执行器、配置、活动区域与输入缓冲，并每个物理帧驱动一次 <see cref="PlayerLogic"/>。
    /// </summary>
    /// <remarks>
    /// 全部环境事实只在本类组装一次。同一个 <c>FixedUpdate</c> 内 <c>InputBuffer.Push</c> 必须先于 <c>Tick</c>，
    /// 否则按下沿会滞后一帧（<c>Docs/分层设计/逻辑层.md</c> §5）。
    /// 顺序固定：消费快照 → 吸附到 8 向并归一化 → 以<b>同一份</b>归一化快照推缓冲 → 组装 <c>WorldInfo</c>
    /// → <c>Logic.FixedTick</c> → 边界钳位。
    /// 边界钳位放最后：它是物理步边界上的"保险丝"，防高速冲出地图；正常阻挡由刚体碰撞解算。
    /// <para><b>归一化只在这里做一次</b>：<c>WorldInfo.MoveDirection</c> 与 <c>InputBuffer</c> 里的快照
    /// 必须是同一份已归一化方向。曾出现"一个用处理后的值、一个用原始值"的写法——
    /// 同一物理帧里存在两份方向真值，正是"斜向快 √2 倍"与"冲刺方向不一致"这类
    /// 不报错、只错手感的缺陷的来源。</para>
    /// </remarks>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config = default;
        [SerializeField] private MovementMotor motor = default;
        [SerializeField] private InputProvider inputProvider = default;

        [Tooltip("地图活动区域：拖入覆盖可行走区域的 BoxCollider2D。不接则不钳位（不报错，调试面板会显示未接线）")]
        [SerializeField] private BoxCollider2D boundsArea = default;

        /// <summary>方向判零的容差：摇杆漂移与浮点残渣不该让角色每帧微动。</summary>
        private const float DirectionEpsilon = 1e-6f;

        /// <summary>8 向吸附的一档（弧度）。45° 一档共 8 档。</summary>
        private const float OctantRadians = 2f * Mathf.PI / 8f;

        /// <summary>
        /// 逻辑层与它本帧的速度上限。<b>白模阶段新增</b>的受击推挤口，见
        /// <see cref="OverrideLogicSpeedTargets"/>。
        /// </summary>
        /// <remarks>
        /// 做成一个具名结构体而不是两个并列参数，是为了让"逻辑层引用 + 上限"这一对永远一起传、
        /// 一起漏 —— 漏一个的后果是"推挤静默生效在另一个玩家身上"。
        /// </remarks>
        internal readonly struct LogicSpeedTargets
        {
            /// <summary>玩家的逻辑层入口。</summary>
            public readonly PlayerLogic Logic;

            /// <summary>本帧的速度上限（单位/秒）。</summary>
            public readonly float MaxSpeed;

            public LogicSpeedTargets(PlayerLogic logic, float maxSpeed)
            {
                Logic = logic;
                MaxSpeed = maxSpeed;
            }
        }

        /// <summary>吸附到 8 向时的一族单位方向，键为"档位序号化的弧度"，避免浮点直接比 key。</summary>
        private static readonly Dictionary<int, Vector2> Snapped = BuildSnappedTable();

        private InputBuffer _buffer;
        private WorldInfo _world;
        private BoundsArea _bounds;

        /// <summary>
        /// 本帧被外部要求的速度上限；<c>null</c> 表示不覆盖（逻辑层按自己的满速档位走）。
        /// </summary>
        /// <remarks>
        /// <b>一次性</b>：<c>FixedUpdate</c> 用它算完当帧上限后立刻清空，所以外因必须每帧重新声明。
        /// 这正是我们要的语义 —— "这一帧被推了一下"与"从此一直被限速"是两件事，
        /// 后者会永久改掉玩家的移动能力却不报错。
        /// </remarks>
        private LogicSpeedTargets? _speedLimit;

        /// <summary>逻辑层入口，供调试面板读取。</summary>
        public PlayerLogic Logic { get; private set; }

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
            inputProvider.SetInputEnabled(false); // 内部已 Clear，无需再调一次
            _buffer.Clear();
        }

        private void OnGameResumed(GameResumed evt)
        {
            inputProvider.SetInputEnabled(true);
        }

        /// <summary>
        /// <b>白模阶段新增</b>：本帧把玩家的速度上限换成给定值，并给出逻辑层入口。
        /// </summary>
        /// <remarks>
        /// 用法是"声明式"的：调用方（首个是白模的 <c>PlayerHealth</c>）每帧调用一次表示
        /// "这一帧我还想限速"，下一帧不调即自动恢复满速档位。
        /// <para><b>为什么需要它：</b>玩家被撞开时，本类已经在同一个物理帧里把速度写成
        /// <c>输入方向 × moveSpeed</c> 了。要让"被撞"表现出来，只能在这之后<b>再写一次</b>速度 ——
        /// 而本类是玩家速度的唯一写者，所以这个口子必须开在这里，不能开在受害者自己那边
        /// （那样就成了第二个速度写者，直接违反 <c>PlayerLogic</c> 与 <c>MovementMotor</c> 的核心不变量）。
        /// 逻辑层本身不被改动语义：它只多知道一个"本帧上限"。</para>
        /// <para>参数是结构体而不是两个独立方法，理由见 <see cref="LogicSpeedTargets"/>。</para>
        /// </remarks>
        internal void OverrideLogicSpeedTargets(PlayerLogic logic, float maxSpeed)
        {
            if (logic == null) return;

            // 用结构体把"哪一层"与"上限多少"绑在一起传：两者分开传的时候漏一个，
            // 后果是限速静默生效在另一个玩家身上（现在只有一个玩家，所以谁也发现不了）。
            _speedLimit = new LogicSpeedTargets(logic, maxSpeed);
        }

        /// <summary>
        /// <b>白模阶段新增</b>：本帧往玩家的速度账本里加一次冲量（击退、水流、吸附等外因用）。
        /// </summary>
        /// <param name="deltaVelocity">一次性的速度变化（单位/秒），<b>不</b>乘 Δt。</param>
        /// <remarks>
        /// <b>必须先 <see cref="OverrideLogicSpeedTargets"/> 再调用本方法。</b>理由同那条的注释：
        /// 本方法是"写第二次速度"，而本类在同一个物理帧里已经写过第一次了。
        /// <para>累加在账本上，所以不是"绕过逻辑层写刚体"：帧末仍由本类的一次写出生效，
        /// 玩家速度依然只有一个写者。</para>
        /// </remarks>
        internal void AddLogicImpulse(PlayerLogic logic, Vector2 deltaVelocity)
        {
            if (logic == null) return;

            logic.AddImpulse(deltaVelocity);
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
            Logic = new PlayerLogic(motor, config, _buffer);

            if (!_bounds.IsValid)
            {
                Debug.LogWarning(
                    "PlayerController.boundsArea 未接线、被停用或尺寸为 0：玩家不会被限制在地图边界内。",
                    this
                    );
            }
        }

        private void FixedUpdate()
        {
            InputSnapshot raw = inputProvider.ConsumeSnapshot();

            Vector2 move = SnapMoveToEightDirections(raw.Move, config.snapToEightDirections);

            // 归一化后的方向要写回快照：WorldInfo 与 InputBuffer 必须是同一份方向，
            // 否则 MoveGroup 取冲刺方向、PlayerLogic 记"最近朝向"时会看到另一份（未处理的）值。
            var snapshot = new InputSnapshot(move, raw.JumpPressed, raw.DashPressed, raw.GrabHeld);

            _buffer.Push(in snapshot, Time.fixedTime);

            _world = new WorldInfo(move, in _bounds);

            // 白模阶段新增：本帧的受击推挤。**必须在 Logic.FixedTick 之前声明**——
            // FixedTick 帧末按这个上限把速度一次写出，声明晚了这一帧就被浪费掉。
            // 声明是"一次性"的：用完立刻清空，所以外因要每帧重新声明（见 _speedLimit 的注释）。
            if (_speedLimit.HasValue)
            {
                LogicSpeedTargets targets = _speedLimit.Value;

                // 只对声明的那个逻辑层生效：将来场景里有第二个"逻辑被驱动的角色"时，
                // 漏判这一条会让限速静默作用在别人身上。
                if (!ReferenceEquals(targets.Logic, Logic))
                {
                    _speedLimit = null;
                }
                else
                {
                    Logic.SetSpeedLimit(targets.MaxSpeed);
                    _speedLimit = null;
                }
            }

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
