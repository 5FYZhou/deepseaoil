using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 移动：按输入方向朝配置速度推进。有没有惯性由角色的加速度配置决定。
    /// </summary>
    /// <remarks>
    /// <c>MoveTowards</c>：加速度填 0 ⇒ 当帧直达（俯视角零惯性）；填正数 ⇒ 按加速度逼近、反向时走转向衰减。
    /// </remarks>
    public sealed class MoveState : StateBase<MovementStateTag, LogicContext>
    {
        /// <summary>宿主（移动层账本）。</summary>
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
