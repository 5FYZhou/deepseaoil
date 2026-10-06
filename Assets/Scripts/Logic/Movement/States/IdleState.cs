using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 站立：无输入，朝零速度收敛（<b>有没有惯性由角色的加速度配置决定</b>）。
    /// </summary>
    /// <remarks>
    /// <c>BrakeTowards</c> 是"惯性感知"的入口：加速度填 0 ⇒ 当帧停（俯视角零惯性），
    /// 填正数 ⇒ 按加速度滑停（用时 = 速度 / 加速度）。
    /// 朝向由速度接管的那一套保持不变（零方向不翻面）。
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
            Logic.BrakeTowards();
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude != 0f;
        }
    }
}
