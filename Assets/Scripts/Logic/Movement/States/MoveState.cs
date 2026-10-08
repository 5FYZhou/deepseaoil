using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>按输入方向朝配置速度推进；MoveTowards 加速度 0 ⇒ 当帧直达，正数 ⇒ 逼近，反向走转向衰减</summary>
    public sealed class MoveState : StateBase<MovementStateTag, LogicContext>
    {
        public MoveState(IStateHost host) : base(host)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Move;

        public override void Enter(LogicContext ctx)
        {
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            Host.MoveTowards(ctx.inputSnapshot.Move, Motion.MoveSpeed);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude == 0f;
        }
    }
}
