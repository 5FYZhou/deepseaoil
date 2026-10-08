using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>移动状态组，唯一写速度处</summary>
    public sealed class MoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        private readonly PlayerLogic _logic;
        private readonly IActorMotor _motor;

        private readonly PlayerSpec _spec;

        private readonly InputBuffer _buffer;
        private readonly DashState _dash;

        /// <summary>冲刺冷却，绝对时刻，不受暂停影响</summary>
        private readonly Cooldown _dashCooldown = new Cooldown();

        /// <summary>最近非零输入方向，已归一化</summary>
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

        /// <summary>冲刺状态实例</summary>
        public DashState Dash => _dash;

        /// <summary>冲刺资格，纯查询不消费</summary>
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

        /// <summary>推进物理帧：先交乘数给账本，再状态写速度，最后强制速度接管</summary>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
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

        /// <summary>抢占提交：喂方向须在消费成功后</summary>
        protected override bool TryCommitPreempt(MovementStateTag target, in LogicContext ctx)
        {
            if (target != MovementStateTag.Dash) return false;

            if (!TryConsumeDash(ctx.now)) return false;

            _dash.Configure(ResolveDashDirection(in ctx, _direction));

            return true;
        }

        /// <summary>门禁落速度，必须放在状态跑完之后，否则挨打了却纹丝不动且不报错；强制速度不叠加速度乘数</summary>
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
