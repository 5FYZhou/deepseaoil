using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;

namespace DeepseaOil.Logic
{
    /// <summary>敌人的移动状态组：唯一写速度的地方，与玩家的 <c>MoveGroup</c> 同一套骨架。</summary>
    /// <remarks>门禁语义与玩家一致：状态先写速度、门禁最后统一施加（顺序反了，"挨打了却纹丝不动"会以另一种形式回来）。</remarks>
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

        /// <summary>推进一个物理帧；帧序见 <c>ActorLogic</c>。</summary>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
            // 乘数落在"目标速度"上，所以必须赶在状态算速度之前交给账本（见 IActorMotor.SpeedScale）。
            _motor.SpeedScale = gates.SpeedScale;

            TickStates(in ctx);

            ApplyGates(in gates);
        }

        private void ApplyGates(in MoveGates gates)
        {
            // 强制速度不叠加速度乘数：受击滑停是外力，叠上地面减速会把它拖短。
            if (gates.HasForcedVelocity) _motor.SetVelocity(gates.ForcedVelocity);
        }
    }
}
