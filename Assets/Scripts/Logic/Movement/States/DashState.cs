using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>冲刺：进入时给一次定时恒速（时长 <c>Motion.DashDuration</c> / 速度 <c>Motion.DashSpeed</c>），到时即结束。</summary>
    /// <remarks>沿朝向 8 向冲刺，不是只沿 x 轴；方向由状态组在切换前喂入（有输入用输入方向，无输入用最近朝向），进入时 <c>SnapVelocity</c> 一次接管全部分量。</remarks>
    public sealed class DashState : StateBase<MovementStateTag, LogicContext>
    {
        private Vector2 _direction = Vector2.up;

        private float _enteredAt;
        private float _duration;

        public DashState(IStateHost host) : base(host)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Dash;

        /// <summary>由状态组在切换前喂入方向（世界方向；零向量表示保持上次方向）。</summary>
        /// <remarks>归一化在本方法里做，<c>Enter</c> 因此不必防 √2 倍：传未归一化的斜向 (1,1) 得到的是正确的 <c>dashSpeed</c>，而不是快 41% 的冲刺。</remarks>
        public void Configure(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            _direction = direction.normalized;
        }

        /// <summary>冲刺方向（已归一化）；唯一的读者是测试，调试面板读的是 <c>MoveGroup.Dash</c>。</summary>
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
