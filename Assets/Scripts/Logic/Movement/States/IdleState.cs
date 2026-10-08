using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>无输入朝零速收敛，惯性由加速度配置决定；BrakeTowards 加速度 0 ⇒ 当帧停，正数 ⇒ 滑停（用时=速度/加速度）</summary>
    public sealed class IdleState : StateBase<MovementStateTag, LogicContext>
    {
        public IdleState(IStateHost host) : base(host)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Idle;

        public override void Enter(LogicContext ctx)
        {
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            Host.BrakeTowards();
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude != 0f;
        }
    }
}
