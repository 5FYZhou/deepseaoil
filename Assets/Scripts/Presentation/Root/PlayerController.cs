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
    /// 顺序固定：消费快照 → 归一化斜向 → 组装 <c>WorldInfo</c> → 推缓冲 → <c>Logic.FixedTick</c> → 边界钳位。
    /// 边界钳位放最后：它是物理步边界上的"保险丝"，防高速冲出地图；正常阻挡由刚体碰撞解算。
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

        private InputBuffer _buffer;
        private WorldInfo _world;
        private BoundsArea _bounds;

        /// <summary>逻辑层入口，供调试面板读取。</summary>
        public PlayerLogic Logic { get; private set; }

        /// <summary>本物理帧喂进逻辑层的环境事实。</summary>
        public WorldInfo World => _world;

        /// <summary>引擎回读速度，与逻辑层的"提交后预期"对照；它滞后一个物理步。</summary>
        public Vector2 EngineVelocity => motor == null ? Vector2.zero : motor.Velocity;

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

            _bounds = new BoundsArea(boundsArea);
            _world = new WorldInfo(Vector2.zero, in _bounds); // 首帧前也不留 default
            Logic = new PlayerLogic(motor, config, _buffer);

            if (!_bounds.IsValid)
            {
                Debug.LogWarning(
                    "PlayerController.boundsArea 未接线或尺寸为 0：玩家不会被限制在地图边界内。",
                    this
                    );
            }
        }

        private void FixedUpdate()
        {
            InputSnapshot snapshot = inputProvider.ConsumeSnapshot();

            Vector2 move = SnapToEightDirections(snapshot.Move);

            _world = new WorldInfo(move, in _bounds);

            _buffer.Push(in snapshot, Time.fixedTime);

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
        /// 把原始输入吸附到 8 向并归一化：键盘同时按两个轴会得到模长 √2，摇杆则是任意角度。
        /// </summary>
        /// <remarks>
        /// 归一化后再 <c>Push</c>，让输入缓冲里存的也是同一份已吸附的方向——
        /// 否则 <c>MoveGroup</c> 取冲刺方向时会拿到未处理的值，两份数据不一致本身就是隐患。
        /// 摇杆轻推（模长 &lt; 1）保持模拟量：吸附是"取方向"，不夺走"推多少"。
        /// </remarks>
        private Vector2 SnapToEightDirections(Vector2 move)
        {
            if (move.sqrMagnitude <= DirectionEpsilon) return Vector2.zero;

            if (!config.snapToEightDirections) return move;

            // 45° 一档共 8 档；Round 天然把 ±22.5° 内的输入归到最近一档，不需要额外死区。
            const float steps = 8f;
            float radians = Mathf.Atan2(move.y, move.x);
            float snapped = Mathf.Round(radians / (2f * Mathf.PI / steps)) * (2f * Mathf.PI / steps);

            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped));
        }
    }
}
