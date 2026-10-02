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
    public sealed class PlayerController : MonoBehaviour, IFixedTickable
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

        /// <summary>吸附到 8 向时的一族单位方向，键为"档位序号化的弧度"，避免浮点直接比 key。</summary>
        private static readonly Dictionary<int, Vector2> Snapped = BuildSnappedTable();

        private InputBuffer _buffer;
        private WorldInfo _world;
        private BoundsArea _bounds;

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
