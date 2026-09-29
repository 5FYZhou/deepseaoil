using DeepSeaOil.Config;
using DeepSeaOil.Logic.State;
using UnityEngine;

namespace DeepSeaOil.Logic.Movement.States
{
    /// <summary>
    /// 冲刺：进入时给一次定时恒速并关闭重力，到时即结束。
    /// </summary>
    public sealed class DashState : TimedStateBase<MovementStateTag>
    {
        private int _direction = 1;

        public DashState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Dash;

        /// <summary>由状态组在切换前喂入方向。</summary>
        public void Configure(int direction)
        {
            _direction = direction;
        }

        public override void Enter(LogicContext ctx)
        {
            base.Enter(ctx);

            Logic.SetGravity(false);
            Logic.SnapVelocity(new Vector2(Config.dashSpeed * _direction, 0f));
        }

        public override void Exit()
        {
            Logic.SetGravity(true);
        }

        public override void Tick(LogicContext ctx)
        {
        }

        protected override float GetDuration(LogicContext ctx)
        {
            return Config.dashDuration;
        }
    }
}
