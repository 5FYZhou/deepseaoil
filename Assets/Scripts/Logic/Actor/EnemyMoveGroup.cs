using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;

namespace DeepseaOil.Logic
{
    /// <summary>敌人移动状态组，唯一写速度处</summary>
    /// <remarks>门禁同玩家：状态先写速度、门禁后施加</remarks>
    public sealed class EnemyMoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        private readonly IActorMotor _motor;

        public EnemyMoveGroup(EnemyLogic logic, IActorMotor motor)
        {
            _motor = motor;

            AddState(new IdleState(logic));
            AddState(new EnemyChaseState(logic));
        }

        protected override MovementStateTag EmptyTag => MovementStateTag.Empty;

        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude > 0f
                ? MovementStateTag.Move
                : MovementStateTag.Idle;
        }

        /// <summary>推进一物理帧</summary>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
            // 乘数落在目标速度，须先于状态算速度交账
            _motor.SpeedScale = gates.SpeedScale;

            TickStates(in ctx);

            ApplyGates(in gates);
        }

        private void ApplyGates(in MoveGates gates)
        {
            // 强制速度不叠速度乘数：受击滑停是外力
            if (gates.HasForcedVelocity) _motor.SetVelocity(gates.ForcedVelocity);
        }
    }
}
