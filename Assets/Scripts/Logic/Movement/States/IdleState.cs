using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 站立：无输入，速度当帧归零。
    /// </summary>
    /// <remarks>
    /// 俯视角零惯性：不存在"减速到 0"的过程，直接停。朝向由 <c>StopMove</c> 保持不变。
    /// </remarks>
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
            Logic.StopMove();
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude != 0f;
        }
    }
}
