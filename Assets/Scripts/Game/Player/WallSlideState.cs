using DeepSeaOil.Config;
using DeepSeaOil.Logic.State;

namespace DeepSeaOil.Logic.Movement.States
{
    /// <summary>
    /// 贴墙：钳住下落速度（按住抓墙键则完全停住），并在按下跳跃时走一次蹬墙跳。
    /// </summary>
    /// <remarks>
    /// 蹬墙跳并入本状态：不切状态，只给一次斜向初速并上移动锁（对齐 REF-B 的 <c>WallState</c>）。
    /// 抓墙不另立状态，只是"把落速钳到 0"的一种模式。
    /// </remarks>
    public sealed class WallSlideState : StateBase<MovementStateTag>
    {
        public WallSlideState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.WallSlide;

        public override void Enter(LogicContext ctx)
        {
            Logic.FaceTowards(ctx.worldInfo.WallSide);
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            // 蹬墙跳当帧必须直接返回，否则下面的水平分支会吃掉斜向初速。
            if (Logic.TryWallJump(in ctx)) return;

            int wallSide = ctx.worldInfo.WallSide;
            if (ctx.inputSnapshot.Move.x * wallSide > 0f)
            {
                Logic.MoveHorizontal(ctx.inputSnapshot.Move.x, Config.moveSpeed, Config.moveAcceleration);
            }
            else
            {
                Logic.StopHorizontal();
            }

            Logic.ClampFallSpeed(ctx.inputSnapshot.GrabHeld ? 0f : Config.wallSlideSpeed);
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.worldInfo.Grounded || !ctx.worldInfo.TouchingWall;
        }
    }
}
