using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>受击：外力接管，输入失效，滑停到零</summary>
    /// <remarks>速度归零即退出，减速度取 hurtDecay，0 落回 MoveAccel</remarks>
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

        /// <summary>喂入一次性冲量（速度向量/秒），参数唯一写入口</summary>
        public void Configure(Vector2 impulse)
        {
            if (impulse.sqrMagnitude <= 0f) return;

            _direction = impulse.normalized;
            _speed = impulse.magnitude;
        }

        public override void Enter(LogicContext ctx)
        {
            // 冲量是瞬时量，进入那帧速度即冲量
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

        /// <summary>本帧减速：hurtDecay 优先，0 落回 MoveAcceleration</summary>
        /// <remarks>非数当没填：NaN 比较恒 false，会静默产出非数坐标</remarks>
        private float Deceleration()
        {
            float decay = Motion.HurtDecay;

            return decay > 0f ? decay : Motion.MoveAcceleration;
        }
    }
}
