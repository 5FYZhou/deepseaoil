using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 站立：无输入，朝零速度收敛。有没有惯性由角色的加速度配置决定。
    /// </summary>
    /// <remarks>
    /// <c>BrakeTowards</c>：加速度填 0 ⇒ 当帧停（俯视角零惯性）；填正数 ⇒ 按加速度滑停（用时 = 速度 / 加速度）。
    /// </remarks>
    public sealed class IdleState : StateBase<MovementStateTag, LogicContext>
    {
        /// <summary>宿主（移动层账本）。</summary>
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
