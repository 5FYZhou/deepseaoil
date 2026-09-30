using DeepseaOil.Config;
using DeepseaOil.Logic.State;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 起跳：进入时给一次上升初速，之后只做空中横向控制；上升到顶即结束。
    /// </summary>
    public sealed class JumpState : StateBase<MovementStateTag>
    {
        public JumpState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Jump;

        public override void Enter(LogicContext ctx)
        {
            Logic.SetVelocityY(Config.jumpSpeed);
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            Logic.AirMove(ctx.inputSnapshot);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return Logic.Velocity.y <= 0f;
        }
    }
}
