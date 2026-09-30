using DeepseaOil.Config;
using DeepseaOil.Logic.State;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 移动：按输入方向把水平速度加速到配置速度。
    /// </summary>
    public sealed class MoveState : StateBase<MovementStateTag>
    {
        public MoveState(ActorLogic logic, CharacterConfig config) : base(logic, config)
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
            Logic.MoveHorizontal(ctx.inputSnapshot.Move.x, Config.moveSpeed, Config.moveAcceleration);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return !ctx.worldInfo.Grounded || ctx.inputSnapshot.Move.x == 0f;
        }
    }
}
