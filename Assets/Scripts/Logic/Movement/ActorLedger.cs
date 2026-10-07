using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>速度账本 ＋ 角色控制律：<b>一帧的速度累加区、帧末一次写出，以及"怎么逼近目标速度"的全部数学</b>。帧边界与唯一调用方见 <see cref="IActorLedger"/>。</summary>
    public sealed class ActorLedger : IActorLedger
    {
        private readonly System.Func<Vector2> _readVelocity;
        private readonly System.Action<Vector2> _writeVelocity;

        private Vector2 _frameStart;
        private Vector2 _delta;
        private Vector2 _accel;
        private float _speedScale = 1f;
        private float _extraForceScale = 1f;
        private float _now;
        private float _dt = 0.02f;

        public ActorLedger(System.Func<Vector2> readVelocity, System.Action<Vector2> writeVelocity)
        {
            _readVelocity = readVelocity;
            _writeVelocity = writeVelocity;
        }

        public CharacterConfig Config { get; private set; }

        public Foundation.MotionParams Motion { get; private set; }

        public float Now => _now;

        public float DeltaTime => _dt;

        /// <remarks>装配期把 <paramref name="config"/> 一次性折算成 <see cref="Motion"/> 快照：<b>事后改 SO 不生效</b>（M19/M20 测试因断言"改 SO 即时生效"被删）。</remarks>
        public void Configure(CharacterConfig config)
        {
            Config = config;

            Motion = config == null
                ? Foundation.MotionParams.None
                : new Foundation.MotionParams(
                    config.moveSpeed,
                    config.moveAcceleration,
                    config.turnDecayRate,
                    config.dashSpeed,
                    config.dashDuration,
                    config.hurtDecay);
        }

        public Vector2 FrameStartVelocity => _frameStart;

        public Vector2 SubmittedDelta => _delta + _accel * _dt;

        /// <inheritdoc />
        public Vector2 Velocity => _frameStart + SubmittedDelta;

        /// <summary>本帧速度乘数（<c>1</c> = 不缩放）；<b>写口夹取</b>：<c>NaN</c> → <c>1</c>、负数 → <c>0</c>、<c>&gt; 1</c> → <c>1</c>（非数进入速度会让角色带着非数坐标消失，且不报错）。</summary>
        public float SpeedScale
        {
            get => _speedScale;
            set
            {
                if (float.IsNaN(value))
                {
                    _speedScale = 1f;
                    return;
                }

                _speedScale = value < 0f ? 0f : (value > 1f ? 1f : value);
            }
        }

        /// <inheritdoc />
        /// <remarks>时刻与 Δt 都由驱动方给出：同一帧里"状态机算出来的"与"账本乘的"必须是同一个数。</remarks>
        public void BeginStep(float now, float deltaTime)
        {
            _now = now;
            _dt = deltaTime;
            _frameStart = _readVelocity();
            _delta = Vector2.zero;
            _accel = Vector2.zero;

            // 乘数是**一次性**的：上一帧声明的到这一帧开头就失效，门禁必须每帧重新提交；不复位的话"这一帧被推了一下"会变成"从此一直被限速"。
            _speedScale = 1f;
        }

        /// <inheritdoc />
        public void Commit()
        {
            // 零提交帧不写速度：没有变更就不必覆盖引擎，第二个写者因此不会被清掉。
            if (!HasSubmission) return;

            _writeVelocity(_frameStart + SubmittedDelta);
        }

        public void AddImpulse(Vector2 deltaVelocity) => _delta += deltaVelocity;

        public void AddForce(Vector2 acceleration) => _accel += acceleration;

        public void SnapVelocity(Vector2 velocity, System.Action<Vector2> facing)
        {
            SetVelocity(velocity);

            if (velocity.x == 0f && velocity.y == 0f) return;

            facing?.Invoke(velocity);
        }

        /// <inheritdoc />
        public void SetVelocity(Vector2 velocity)
        {
            SetVelocityX(velocity.x);
            _accel.y = 0f;
            _delta.y = velocity.y - _frameStart.y;
        }

        /// <inheritdoc />
        public void ClampSpeed(float maxSpeed)
        {
            if (maxSpeed <= 0f) return;

            SetVelocity(Vector2.ClampMagnitude(Velocity, maxSpeed));
        }

        /// <inheritdoc />
        public void SetExtraForceScale(float scale) => _extraForceScale = scale;

        /// <summary>提交一帧外力：把 <paramref name="force"/>（单位/秒²）按 Δt 与强度缩放累进账本；零向量表示无外力（当帧成立即返回，不产生提交）。它与 <see cref="SetExtraForceScale"/> <b>没有生产消费者</b>，但测试里有语义钉子，刻意保留（本类不持有任何具体力的语义，重力 / 浮力 / 水流 / 风 / 吸附 / 击退滑行都只是它的取值）。</summary>
        public void ApplyExtraForce(Vector2 force)
        {
            if (force.sqrMagnitude <= 0f || _extraForceScale == 0f) return;

            AddForce(force * _extraForceScale);
        }

        public void MoveDirection(Vector2 direction, float speed)
        {
            SetVelocity(direction.normalized * speed);
        }

        public void StopMove()
        {
            SetVelocity(Vector2.zero);
        }

        /// <summary>移动层的"走"：<b>有惯性按加速度逼近，零惯性当帧直达</b>；方向可未归一化，零向量表示没有期望方向。</summary>
        /// <remarks>判据是 <see cref="Foundation.MotionParams.MoveAcceleration"/>（<c>≤ 0</c> = 零惯性配置）。<b>速度乘数在这里落地</b>：乘的是目标速度 ⇒ 零惯性角色当帧生效，有惯性角色稳态精确等于"配置速度 × 乘数"。</remarks>
        public void MoveTowards(Vector2 direction, float speed)
        {
            float scaled = speed * _speedScale;

            if (Motion.MoveAcceleration <= 0f)
            {
                MoveDirection(direction, scaled);
                return;
            }

            SteerTowards(direction, scaled, Motion.MoveAcceleration, Motion.TurnDecayRate);
        }

        public void BrakeTowards()
        {
            if (Motion.MoveAcceleration <= 0f)
            {
                StopMove();
                return;
            }

            SteerTowards(Vector2.zero, 0f, Motion.MoveAcceleration, Motion.TurnDecayRate);
        }

        /// <summary>二维渐进逼近目标速度；<b>控制律的唯一实现点</b>。方向可未归一化，零向量表示"没有期望方向"。</summary>
        /// <remarks><b>零方向走指数衰减，且只有这一支</b>（<c>Δ -= 当前速度 × (1 − e^(−衰减率·Δt))</c>）：符号保持、模长单调收缩，不振荡、不反向、不越过零；方向变号时取两支中较快的一支（纯指数衰减永不反向，只看它会把角色停在原地不动）。<b>目标速度与当前速度都为零时不写速度</b>：零提交帧在 <see cref="Commit"/> 里会被跳过，"没有变更就不覆盖引擎"因此仍然成立。</remarks>
        public void SteerTowards(Vector2 direction, float targetSpeed, float acceleration, float decayPerSecond)
        {
            Vector2 current = Velocity;
            Vector2 desired;

            if (direction.sqrMagnitude <= 0f || targetSpeed <= 0f)
            {
                desired = Vector2.zero;
            }
            else
            {
                desired = direction.normalized * targetSpeed;
            }

            Vector2 delta = desired - current;

            if (desired.sqrMagnitude > 0f && current.sqrMagnitude > 0f && Vector2.Dot(desired, current) < 0f)
            {
                Vector2 decay = -current * (1f - Mathf.Exp(-decayPerSecond * _dt));

                if (decay.sqrMagnitude > delta.sqrMagnitude) delta = decay;
            }
            else
            {
                float maxStep = acceleration * _dt;

                if (delta.sqrMagnitude > maxStep * maxStep) delta = delta.normalized * maxStep;
            }

            _delta += delta;
        }

        private void SetVelocityX(float vx)
        {
            _accel.x = 0f;
            _delta.x = vx - _frameStart.x;
        }

        private bool HasSubmission => _delta.x != 0f || _delta.y != 0f || _accel.x != 0f || _accel.y != 0f;
    }
}
