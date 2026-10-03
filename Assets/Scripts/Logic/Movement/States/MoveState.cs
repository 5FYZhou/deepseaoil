using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 移动：按输入方向把速度整体接管为 方向 × 配置速度。
    /// </summary>
    /// <remarks>
    /// 俯视角零惯性：没有加速度、没有阻尼，当帧就是目标速度。
    /// <see cref="IsDone"/> 只按输入判——不再看是否离地/贴墙，因为俯视角没有那些空中态可去。
    /// </remarks>
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
            Logic.MoveDirection(ctx.inputSnapshot.Move, Config.moveSpeed);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude == 0f;
        }
    }
}
