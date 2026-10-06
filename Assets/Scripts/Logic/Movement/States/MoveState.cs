using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 移动：按输入方向朝配置速度推进（<b>有没有惯性由角色的加速度配置决定</b>）。
    /// </summary>
    /// <remarks>
    /// <c>MoveTowards</c> 是"惯性感知"的入口：加速度填 0 ⇒ 当帧直达（俯视角零惯性），
    /// 填正数 ⇒ 按加速度逼近、反向时走转向衰减。状态本身不判断惯性，判据只有一处。
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
            Logic.MoveTowards(ctx.inputSnapshot.Move, Config.moveSpeed);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude == 0f;
        }
    }
}
