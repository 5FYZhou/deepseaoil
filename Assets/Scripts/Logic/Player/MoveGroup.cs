using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>移动状态组：持有移动层的状态实例与状态机，<b>并且是唯一写速度的地方</b>。</summary>
    /// <remarks>帧序见 <c>ActorLogic</c>；门禁由骨架仲裁，本类只负责把上层门禁落到速度上。</remarks>
    public sealed class MoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        private readonly PlayerLogic _logic;
        private readonly IActorMotor _motor;

        private readonly PlayerSpec _spec;

        private readonly InputBuffer _buffer;
        private readonly DashState _dash;

        /// <summary>冲刺冷却：存绝对时刻（不受暂停影响，也不需要每帧累减）。</summary>
        private readonly Cooldown _dashCooldown = new Cooldown();

        /// <summary>最近一次非零输入方向（<b>已归一化</b>）；零输入时保持不变，供冲刺取向用。</summary>
        private Vector2 _direction = Vector2.right;

        public MoveGroup(PlayerLogic logic, PlayerSpec spec, IActorMotor motor, InputBuffer buffer)
        {
            _logic = logic;
            _spec = spec;
            _motor = motor;
            _buffer = buffer;

            _dash = new DashState(logic);

            AddState(new IdleState(logic));
            AddState(new MoveState(logic));
            AddState(_dash);

            StateChanged += OnStateChanged;
        }

        protected override MovementStateTag EmptyTag => MovementStateTag.Empty;

        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude > 0f
                ? MovementStateTag.Move
                : MovementStateTag.Idle;
        }

        /// <summary>冲刺状态实例（调试与测试读入场方向）；入场参数一律经 <see cref="TryCommitPreempt"/> 喂入。</summary>
        public DashState Dash => _dash;

        /// <summary>冲刺资格：冷却已过，且缓冲里有窗口内的按下。<b>纯查询</b>，不消费。</summary>
        public bool CanDash(float now)
        {
            return _dashCooldown.CanUse(now)
                && _buffer.CanConsume(InputType.Dash, now, _spec.DashBufferSeconds);
        }

        public bool TryConsumeDash(float now)
        {
            if (!_dashCooldown.CanUse(now)) return false;
            if (!_buffer.TryConsume(InputType.Dash, now, _spec.DashBufferSeconds)) return false;

            _dashCooldown.MarkUsed(now, _spec.DashCooldownSeconds);

            return true;
        }

        /// <summary>推进一个物理帧：<b>速度乘数先交给账本、状态再按输入写速度、强制速度最后整条接管</b>。</summary>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
            // 乘数落在"目标速度"上，必须赶在状态算速度之前交给账本（见 IActorMotor.SpeedScale）。
            _motor.SpeedScale = gates.SpeedScale;

            Vector2 move = ctx.inputSnapshot.Move;

            if (move.sqrMagnitude > 0f) _direction = move.normalized;

            TickStates(in ctx);

            ApplyGates(in gates);
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            if (CanDash(ctx.now))
            {
                target = MovementStateTag.Dash;
                return true;
            }

            target = MovementStateTag.Empty;
            return false;
        }

        /// <summary>抢占提交：喂方向必须在消费成功之后，否则提交失败时会留下与上次冲刺不符的脏方向。</summary>
        protected override bool TryCommitPreempt(MovementStateTag target, in LogicContext ctx)
        {
            if (target != MovementStateTag.Dash) return false;

            if (!TryConsumeDash(ctx.now)) return false;

            _dash.Configure(ResolveDashDirection(in ctx, _direction));

            return true;
        }

        /// <summary>把门禁落到速度上 —— <b>全工程唯一"按上层要求改速度"的地方</b>。</summary>
        /// <remarks>必须放在状态跑完之后：状态（<c>MoveState</c> / <c>IdleState</c>）会整体接管速度，门禁写在它们前面等于没写 —— 现象是"挨打了却纹丝不动"，不报错。
        /// 强制速度不叠加速度乘数：受击滑停是外力，叠上地面减速会把它拖短。</remarks>
        private void ApplyGates(in MoveGates gates)
        {
            if (gates.HasForcedVelocity) _motor.SetVelocity(gates.ForcedVelocity);
        }

        private static Vector2 ResolveDashDirection(in LogicContext ctx, Vector2 fallback)
        {
            Vector2 move = ctx.inputSnapshot.Move;

            return move.sqrMagnitude > 0f ? move.normalized : fallback;
        }

        private static void OnStateChanged(MovementStateTag current, MovementStateTag previous)
        {
            EventBus<MovementStateChanged>.Publish(new MovementStateChanged(current, previous));
        }
    }
}
