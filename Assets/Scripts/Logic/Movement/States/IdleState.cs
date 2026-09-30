using DeepseaOil.Config;
using DeepseaOil.Logic.State;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 站立：无水平输入，把水平速度减速到 0。
    /// </summary>
    public sealed class IdleState : StateBase<MovementStateTag>
    {
        public IdleState(ActorLogic logic, CharacterConfig config) : base(logic, config)
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
            Logic.BrakeHorizontal(Config.moveAcceleration);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return !ctx.worldInfo.Grounded || ctx.inputSnapshot.Move.x != 0f;
        }
    }
}
