using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Player;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 玩家组合根：组装执行器、配置与输入缓冲，并每个物理帧驱动一次 <see cref="PlayerLogic"/>。
    /// </summary>
    /// <remarks>
    /// 全部环境事实只在本类组装一次。同一个 <c>FixedUpdate</c> 内 <c>InputBuffer.Push</c> 必须先于 <c>Tick</c>，
    /// 否则松键沿会滞后一帧、可变跳高失效（<c>Docs/M1微规划.md</c> §三 D14）。
    /// </remarks>
    public sealed class PlayerController : MonoBehaviour, IFixedTickable
    {
        [SerializeField] private PlayerConfig config = default;
        [SerializeField] private MovementMotor motor = default;
        [SerializeField] private InputProvider inputProvider = default;

        private InputBuffer _buffer;
        private WorldInfo _world;

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
            inputProvider.Clear(); 
            inputProvider.SetInputEnabled(false);
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

            // 缓冲时长取配置里最大的输入窗口；采样率与实际 Push 频率一致。
            _buffer = new InputBuffer(
                Mathf.Max(config.jumpBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            Logic = new PlayerLogic(motor, config, _buffer);
        }

        private void FixedUpdate()
        {
            InputSnapshot snapshot = inputProvider.ConsumeSnapshot();

            _buffer.Push(in snapshot, Time.fixedTime);

            int wallSide = motor.WallSide;
            _world = new WorldInfo(motor.IsGrounded, wallSide != 0, wallSide);

            Logic.FixedTick(
                new LogicContext(
                    Time.fixedTime,
                    Time.fixedDeltaTime,
                    in _world,
                    in snapshot
                )
            );
        }
    }
}
