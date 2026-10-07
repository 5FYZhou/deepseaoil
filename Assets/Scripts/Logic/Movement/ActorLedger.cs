using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 速度账本 ＋ 角色控制律：<b>一帧的速度累加区、帧末一次写出，以及"怎么逼近目标速度"的全部数学</b>。
    /// </summary>
    /// <remarks>
    /// <b>它不吃任何引擎对象</b>（位置由持有者读写），所以整套控制律可以在 EditMode 里直接喂 Δt 复现 ——
    /// 这是"把 Motor 开放出来"这条裁定的主要收益：控制律不再需要先造一个 <c>ActorLogic</c> 才能测。
    /// <para><b>它的两个宿主</b>：表现层的 <c>ActorMotor</c>（把速度写进 <c>Rigidbody2D</c>）与
    /// 测试探针（速度只存在一个字段里）。两处共用同一份账本 ⇒ 测试里验的账本与线上跑的是同一份代码。</para>
    /// <para><b>帧的边界只有一条：</b><see cref="BeginStep"/> 清空累加区、<see cref="Commit"/> 在帧末
    /// 写出恰好一次。调用方只有一个 —— 驱动状态机的地方。</para>
    /// </remarks>
    public sealed class ActorLedger : IActorLedger
    {
        private readonly System.Func<Vector2> _readVelocity;
        private readonly System.Action<Vector2> _writeVelocity;

        private Vector2 _frameStart;
        private Vector2 _delta;
        private Vector2 _accel;
        private float _speedLimit = float.PositiveInfinity;
        private float _speedScale = 1f;
        private float _extraForceScale = 1f;
        private float _moveLockUntil = float.NegativeInfinity;
        private float _now;
        private float _dt = 0.02f;

        /// <param name="readVelocity">读引擎当前速度（帧首真值）。</param>
        /// <param name="writeVelocity">写引擎速度（帧末唯一一次写出）。</param>
        public ActorLedger(System.Func<Vector2> readVelocity, System.Action<Vector2> writeVelocity)
        {
            _readVelocity = readVelocity;
            _writeVelocity = writeVelocity;
        }

        /// <summary>角色共用运动参数。</summary>
        public CharacterConfig Config { get; private set; }

        /// <summary>本步的逻辑时刻（秒）。</summary>
        public float Now => _now;

        /// <summary>本步时长（秒）。</summary>
        public float DeltaTime => _dt;

        /// <inheritdoc cref="IActorMotor.Configure" />
        public void Configure(CharacterConfig config)
        {
            Config = config;
        }

        // ─────────────────────────────────────────────
        // IActorLedger：读
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        public Vector2 FrameStartVelocity => _frameStart;

        /// <inheritdoc />
        public Vector2 SubmittedDelta => _delta + _accel * _dt;

        /// <inheritdoc />
        public Vector2 Velocity => _frameStart + SubmittedDelta;

        /// <inheritdoc />
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

        /// <summary>本帧速度上限是否被设过（诊断用）。</summary>
        public bool HasSpeedLimit => !float.IsInfinity(_speedLimit);

        // ─────────────────────────────────────────────
        // IActorLedger：帧
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        /// <remarks>时刻与 Δt 都由驱动方给出：同一帧里"状态机算出来的"与"账本乘的"必须是同一个数。</remarks>
        public void BeginStep(float now, float deltaTime)
        {
            _now = now;
            _dt = deltaTime;
            _frameStart = _readVelocity();
            _delta = Vector2.zero;
            _accel = Vector2.zero;

            // 上限与乘数都是**一次性**的：上一帧声明的到这一帧开头就失效。不这样做的话
            // "这一帧被推了一下"会变成"从此一直被限速"——玩家永久失去一部分速度，而没有任何东西会报错。
            _speedLimit = float.PositiveInfinity;
            _speedScale = 1f;
        }

        /// <inheritdoc />
        public void Commit()
        {
            // 零提交帧不写速度：没有变更就不必覆盖引擎，第二个写者因此不会被清掉。
            if (!HasSubmission) return;

            _writeVelocity(Clamped(_frameStart + SubmittedDelta));
        }

        // ─────────────────────────────────────────────
        // IActorLedger：写
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        public void AddImpulse(Vector2 deltaVelocity) => _delta += deltaVelocity;

        /// <inheritdoc />
        public void AddForce(Vector2 acceleration) => _accel += acceleration;

        /// <summary>接管两个分量：本帧工作速度即为给定值，覆盖已提交的全部变更；同时按水平分量更新朝向。</summary>
        /// <param name="velocity">目标速度（单位/秒）。</param>
        /// <param name="facing">朝向出口：函数的唯一副作用是它（与速度无关的朝向更新走调用方自己）。</param>
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
        public void SetSpeedLimit(float maxSpeed)
        {
            if (float.IsNaN(maxSpeed) || float.IsInfinity(maxSpeed) || maxSpeed <= 0f) return;

            _speedLimit = maxSpeed;
        }

        /// <inheritdoc />
        public void SetExtraForceScale(float scale) => _extraForceScale = scale;

        /// <inheritdoc />
        public void StartMoveLock(float now, float duration)
        {
            _moveLockUntil = now + duration;
        }

        /// <summary>移动锁定期内不响应移动提交（朝向照更新）。</summary>
        public bool IsMoveLocked => _now < _moveLockUntil;

        /// <summary>
        /// 提交一帧外力：把 <paramref name="force"/>（单位/秒²）按 Δt 与强度缩放累进账本。
        /// </summary>
        /// <param name="force">本帧外力加速度；零向量表示无外力（当帧成立即返回，不产生提交）。</param>
        /// <remarks>
        /// <b>参数由调用方给出，本类不持有任何具体力的语义</b>——重力、浮力、水流、风、吸附、击退滑行
        /// 都只是 <paramref name="force"/> 的一种取值。
        /// <para><see cref="SetExtraForceScale"/> / <see cref="StartMoveLock"/> 与它目前<b>都没有生产消费者</b>
        /// （首推者预计是结冰打滑 / 水流推挤 / 特殊状态）。刻意保留：它们是控制律的组成部分，
        /// 且已有测试钉住语义（同帧两者并用必须先累加外力、后钳制）。</para>
        /// </remarks>
        public void ApplyExtraForce(Vector2 force)
        {
            if (force.sqrMagnitude <= 0f || _extraForceScale == 0f) return;

            AddForce(force * _extraForceScale);
        }

        // ─────────────────────────────────────────────
        // 控制律
        // ─────────────────────────────────────────────

        /// <summary>
        /// 俯视角移动：把速度整体接管为 <paramref name="direction"/> × <paramref name="speed"/>（零惯性直达）。
        /// </summary>
        /// <param name="direction">目标方向。<b>可以是未归一化向量</b>（内部归一化）。</param>
        /// <param name="speed">目标速度（单位/秒）。</param>
        /// <returns>是否真的提交了速度（移动锁定期内不提交）。</returns>
        public bool MoveDirection(Vector2 direction, float speed)
        {
            if (IsMoveLocked) return false;

            SetVelocity(direction.normalized * speed);

            return true;
        }

        /// <summary>急停：速度当帧归零；移动锁定期内不响应。</summary>
        /// <returns>是否真的提交了速度。</returns>
        public bool StopMove()
        {
            if (IsMoveLocked) return false;

            SetVelocity(Vector2.zero);

            return true;
        }

        /// <summary>
        /// 移动层的"走"：<b>有惯性按加速度逼近，零惯性当帧直达</b>。
        /// </summary>
        /// <param name="direction">目标方向（可未归一化；零向量表示没有期望方向）。</param>
        /// <param name="speed">该方向上的目标速度（<b>会乘上本帧的速度乘数</b>）。</param>
        /// <remarks>
        /// 判据是 <see cref="CharacterConfig.moveAcceleration"/>（<c>≤ 0</c> = 零惯性配置）：
        /// 于是"要不要惯性"是一个配置问题而不是一次代码改动。
        /// <para><b>速度乘数在这里落地</b>：乘的是<b>目标速度</b>，于是"泥浆里走得慢"对零惯性角色当帧生效，
        /// 对有惯性角色的稳态也精确等于"配置速度 × 乘数"。</para>
        /// </remarks>
        public void MoveTowards(Vector2 direction, float speed)
        {
            float scaled = speed * _speedScale;

            if (Config == null || Config.moveAcceleration <= 0f)
            {
                MoveDirection(direction, scaled);
                return;
            }

            SteerTowards(direction, scaled, Config.moveAcceleration, Config.turnDecayRate);
        }

        /// <summary>移动层的"停"：<b>有惯性滑停，零惯性当帧停</b>。</summary>
        public void BrakeTowards()
        {
            if (Config == null || Config.moveAcceleration <= 0f)
            {
                StopMove();
                return;
            }

            SteerTowards(Vector2.zero, 0f, Config.moveAcceleration, Config.turnDecayRate);
        }

        /// <summary>
        /// 二维渐进逼近目标速度；<b>控制律的唯一实现点</b>。
        /// </summary>
        /// <param name="direction">期望方向。<b>可以是未归一化向量</b>（本方法内归一化），零向量表示"没有期望方向"。</param>
        /// <param name="targetSpeed">该方向上的目标速度（单位/秒）。</param>
        /// <param name="acceleration">加速度上限（单位/秒²）。</param>
        /// <param name="decayPerSecond">反向/归零时的指数衰减率（1/秒）。</param>
        /// <remarks>
        /// <b>零方向分支是指数衰减，且只有这一支。</b>方向为零时只做
        /// <c>Δ -= 当前速度 × (1 − e^(−衰减率·Δt))</c>：符号保持、模长单调收缩，所以不会振荡、
        /// 不会反向、也不会越过零。
        /// <para><b>方向变号时取两支中较快的一支</b>：纯指数衰减永不反向，只看它会把角色停在原地不动。</para>
        /// <para><b>目标速度为零且当前速度也为零时不写速度</b>：零提交帧在 <see cref="Commit"/> 里会被跳过，
        /// "没有变更就不覆盖引擎"这条不变量因此仍然成立。</para>
        /// </remarks>
        public void SteerTowards(Vector2 direction, float targetSpeed, float acceleration, float decayPerSecond)
        {
            Vector2 current = Velocity;
            Vector2 desired;

            if (direction.sqrMagnitude <= 0f || targetSpeed <= 0f)
            {
                // 没有期望方向：把速度指数收缩到零。
                desired = Vector2.zero;
            }
            else
            {
                // 归一化在这里做，调用方可以给未归一化的方向（契约不靠调用方守）。
                desired = direction.normalized * targetSpeed;
            }

            Vector2 delta = desired - current;

            if (desired.sqrMagnitude > 0f && current.sqrMagnitude > 0f && Vector2.Dot(desired, current) < 0f)
            {
                // 反向：改用指数衰减，与线性逼近取较快的一支。
                Vector2 decay = -current * (1f - Mathf.Exp(-decayPerSecond * _dt));

                if (decay.sqrMagnitude > delta.sqrMagnitude) delta = decay;
            }
            else
            {
                // 同向（或已停止）：按加速度限幅地逼近。
                float maxStep = acceleration * _dt;

                if (delta.sqrMagnitude > maxStep * maxStep) delta = delta.normalized * maxStep;
            }

            _delta += delta;
        }

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        private void SetVelocityX(float vx)
        {
            _accel.x = 0f;
            _delta.x = Clamped(new Vector2(vx, 0f)).x - _frameStart.x;
        }

        private bool HasSubmission => _delta.x != 0f || _delta.y != 0f || _accel.x != 0f || _accel.y != 0f;

        /// <summary>本帧上限只在这里被用一次：<see cref="SetSpeedLimit"/> 不动速度，只压写出。</summary>
        private Vector2 Clamped(Vector2 velocity)
        {
            return float.IsInfinity(_speedLimit) ? velocity : Vector2.ClampMagnitude(velocity, _speedLimit);
        }
    }
}
