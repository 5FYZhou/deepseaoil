using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>冲刺，进入时给一次定时恒速（DashDuration/DashSpeed），到时结束</summary>
    /// <remarks>沿朝向 8 向冲刺，非只沿 x 轴；方向由状态组切换前喂入，进入时 SnapVelocity 一次接管全部分量</remarks>
    public sealed class DashState : StateBase<MovementStateTag, LogicContext>
    {
        private Vector2 _direction = Vector2.up;

        private float _enteredAt;
        private float _duration;

        public DashState(IStateHost host) : base(host)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Dash;

        /// <summary>状态组切换前喂入方向，零=保持上次</summary>
        public void Configure(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            _direction = direction.normalized;
        }

        /// <summary>已归一化，仅测试读</summary>
        public Vector2 Direction => _direction;

        public override void Enter(LogicContext ctx)
        {
            _enteredAt = ctx.now;
            _duration = Motion.DashDuration;

            Host.SnapVelocity(_direction * Motion.DashSpeed);
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.now - _enteredAt >= _duration;
        }
    }
}
