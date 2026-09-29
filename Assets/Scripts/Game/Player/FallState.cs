using DeepSeaOil.Config;
using DeepSeaOil.Logic.State;

namespace DeepSeaOil.Logic.Movement.States
{
    /// <summary>
    /// 下落：空中横向控制；落地或贴墙即结束。
    /// </summary>
    public sealed class FallState : StateBase<MovementStateTag>
    {
        public FallState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Fall;

        public override void Enter(LogicContext ctx)
        {
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
            return ctx.worldInfo.Grounded || ctx.worldInfo.TouchingWall;
        }
    }
}
