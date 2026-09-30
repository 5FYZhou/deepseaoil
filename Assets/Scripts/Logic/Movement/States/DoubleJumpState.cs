using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 二段跳：进入时给一次比起跳更小的上升初速，之后只做空中横向控制；上升到顶即结束。
    /// </summary>
    /// <remarks>与 <see cref="JumpState"/> 结构相同、只差初速与状态标签——分开是为了让 M4 动画能区分两者。</remarks>
    public sealed class DoubleJumpState : StateBase<MovementStateTag>
    {
        public DoubleJumpState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.DoubleJump;

        public override void Enter(LogicContext ctx)
        {
            Logic.SetVelocityY(Config.doubleJumpSpeed);
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