using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>转向闸门：把"每帧翻面"变成"划弧"，不参与任何决策</summary>
    /// <remarks>门关着时输出原样等于输入（决策怎么给就怎么发，绕障 / 分离 / 停靠的语义都不动）。</remarks>
    public sealed class EnemySteeringGate
    {
        /// <summary>抖动判据（度）：回头量超过它且比上上帧更接近本帧</summary>
        private const float ChatterDegrees = 4f;

        /// <summary>翻面判据（度）：也是输出单帧转角的上限</summary>
        private const float FlipDegrees = 45f;

        /// <summary>闸门退出阈值（度）</summary>
        private const float GateReleaseDegrees = 6f;

        /// <summary>前半平面偏置：躲开 90° 上的浮点抖动</summary>
        private const float HalfPlaneBias = 0.06f;

        /// <summary>单帧最多推进多少秒：卡顿后补一大段会当帧失效</summary>
        private const float MaxSeconds = 0.1f;

        private readonly float _rate;
        private readonly float _seconds;

        private float _now = float.NegativeInfinity;
        private Vector2 _command;
        private Vector2 _rawPrev;
        private Vector2 _rawPrevPrev;
        private bool _open;

        /// <remarks>ratePerSecond / seconds ≤ 0 即关闭闸门（只剩前半平面约束，不会把敌人卡住）</remarks>
        public EnemySteeringGate(float ratePerSecond, float seconds)
        {
            _rate = ratePerSecond > 0f ? ratePerSecond : 0f;
            _seconds = seconds > 0f ? seconds : 0f;
        }

        /// <summary>推进一帧并返回本帧方向；同一帧内重复调用幂等</summary>
        /// <remarks>direction 必须是单位向量。输出恒留在速度的前半平面：账本 SteerTowards 的反向分支（点积 &lt; 0）不做加速度限幅，一帧就能把速度整块翻面，
        /// 越过它单帧 |Δv| 会到配置加速度的几十倍。速度为零时不判（零向量没有前半平面）。</remarks>
        public Vector2 Step(Vector2 direction, Vector2 velocity, float now)
        {
            if (_rate <= 0f || _seconds <= 0f) return HalfPlane(direction, velocity);

            if (now <= _now) return _command;

            float dt = float.IsNegativeInfinity(_now) ? 0f : Mathf.Clamp(now - _now, 0f, MaxSeconds);

            _now = now;

            if (_rawPrev.sqrMagnitude > 0f)
            {
                float back = Vector2.Angle(direction, _rawPrev);
                bool reversed = _rawPrevPrev.sqrMagnitude > 0f
                    && back > ChatterDegrees
                    && Vector2.Angle(direction, _rawPrevPrev) < back;

                if (back > FlipDegrees || reversed) _open = true;
            }

            _rawPrevPrev = _rawPrev;
            _rawPrev = direction;

            if (!_open)
            {
                _command = HalfPlane(direction, velocity);

                return _command;
            }

            float error = Vector2.SignedAngle(_command, direction);
            float limit = _rate * dt;
            float step = Mathf.Clamp(error * (1f - Mathf.Exp(-dt / _seconds)), -limit, limit);

            if (Mathf.Abs(error) <= GateReleaseDegrees)
            {
                _open = false;
                _command = HalfPlane(direction, velocity);

                return _command;
            }

            _command = HalfPlane(Rotate(_command, step), velocity);

            return _command;
        }


        /// <remarks>回退与换步用：这些方向不是流场决策（没有路 / 目标停住 / 离散脱困），滤慢等于"出不去"</remarks>
        public void Reset(Vector2 direction, float now)
        {
            _open = false;
            _now = now;

            if (direction.sqrMagnitude <= 0f) return;

            _command = direction.normalized;
            _rawPrev = _command;
            _rawPrevPrev = _command;
        }

        private static Vector2 HalfPlane(Vector2 direction, Vector2 velocity)
        {
            if (velocity.sqrMagnitude <= 0f) return direction;

            Vector2 hat = velocity.normalized;

            if (Vector2.Dot(direction, hat) >= 0f) return direction;

            Vector2 perp = new Vector2(-hat.y, hat.x);
            float side = Vector2.Dot(direction, perp) < 0f ? -1f : 1f;

            return (perp * side + hat * HalfPlaneBias).normalized;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
