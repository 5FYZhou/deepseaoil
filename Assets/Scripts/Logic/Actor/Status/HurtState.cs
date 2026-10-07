using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>受击：速度被外力接管，输入暂时失效；滑停到零就结束。玩家与敌人共用。</summary>
    /// <remarks>退出条件是"速度归零"而不是"时长到"：冲量大小会变，固定时长要么留一段空转、要么半路收回控制权。
    /// 减速度取 <c>hurtDecay</c>，填 0 时落回 <c>moveAcceleration</c>（两者都填 0 时退化为"一帧的位移"）。</remarks>
    public sealed class HurtState : StateBase<StatusStateTag, LogicContext>
    {
        private Vector2 _direction = Vector2.up;
        private float _speed;

        private bool _justEntered;

        public HurtState(IStateHost host) : base(host)
        {
        }

        public override StatusStateTag StateTag => StatusStateTag.Hurt;

        public Vector2 ForcedVelocity => _direction * _speed;

        public float RemainingSpeed => _speed;

        /// <summary>由状态组在切换前喂入一次性冲量（速度向量，单位/秒）。</summary>
        /// <remarks>零向量（含非数）表示"保持上次方向"；入场参数的写入口只有这一个。</remarks>
        public void Configure(Vector2 impulse)
        {
            if (impulse.sqrMagnitude <= 0f) return;

            _direction = impulse.normalized;
            _speed = impulse.magnitude;
        }

        public override void Enter(LogicContext ctx)
        {
            // 冲量是瞬时量：进入的那一帧速度就是冲量本身，不衰减。少了这一条，"被撞开的第一帧"会小一个 Δt 的量，低减速度下肉眼可见。
            _justEntered = true;
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            if (_justEntered)
            {
                _justEntered = false;
                return;
            }

            if (_speed <= 0f) return;

            float deceleration = Deceleration();

            _speed = deceleration > 0f
                ? Mathf.Max(0f, _speed - deceleration * ctx.deltaTime)
                : 0f;
        }

        public override bool IsDone(LogicContext ctx)
        {
            return _speed <= 0f;
        }

        /// <summary>本帧的滑停减速度：<c>hurtDecay</c> 优先，填 0（或非数）时落回 <c>moveAcceleration</c>。</summary>
        /// <remarks><b>非数按"没填"处理</b>：NaN 参与比较恒为 false，直接拿去算会把速度变成非数、角色带着非数坐标消失 —— 而这一条没有任何报错。</remarks>
        private float Deceleration()
        {
            float decay = Motion.HurtDecay;

            return decay > 0f ? decay : Motion.MoveAcceleration;
        }
    }
}
