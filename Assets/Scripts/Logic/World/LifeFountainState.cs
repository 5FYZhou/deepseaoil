using UnityEngine;

namespace DeepseaOil.Logic.World
{
    // <summary>生命神泉的静止回血规则：</summary>
    public sealed class LifeFountainState
    {
        private const float BaseChargeRate = 1f;
        private const float AccelPerSecond = 1f;
        private const float MaxChargeRate = 2.8f;
        private const float InRangeDecayPerSecond = 0.35f;
        private const float OutRangeDecayPerSecond = 0.6f;

        /// <summary>速度判据（世界单位/秒）：比这更快就算在动</summary>
        public const float SpeedEpsilon = 0.05f;

        // <summary>单帧位移判据（世界单位）：</summary>
        public const float MoveEpsilon = 0.002f;

        private readonly int _healInterval;

        private float _still;

        private float _stillStreak;

        private Vector2 _lastPosition;

        private bool _hasLastPosition;

        public LifeFountainState(float healIntervalSeconds)
        {
            _healInterval = Mathf.Max(1, Mathf.RoundToInt(healIntervalSeconds));
        }

        /// <summary>当前累计的静止秒数，HUD 可用它画进度</summary>
        public float StillSeconds => _still;

        public int HealInterval => _healInterval;

        /// <summary>静止判据（回血那一帧仍为真）</summary>
        public bool IsStill { get; private set; }

        /// <summary>离开神泉：累计立刻清零（不是暂停，回来要从头站）</summary>
        public void Leave()
        {
            _still = 0f;
            _stillStreak = 0f;
            _hasLastPosition = false;
            IsStill = false;
        }

        /// <summary>推进一帧</summary>
        // <remarks>withinRange 由驱动方按九宫格判定；</remarks>
        public bool Tick(
            Vector2 position,
            bool withinRange,
            bool hasMoveInput,
            float speed,
            float deltaTime)
        {
            if (deltaTime <= 0f) return false;

            bool stationary = withinRange && !hasMoveInput && speed <= SpeedEpsilon;

            if (_hasLastPosition)
            {
                stationary &= (position - _lastPosition).sqrMagnitude <= MoveEpsilon * MoveEpsilon;
            }

            _lastPosition = position;
            _hasLastPosition = true;

            if (!withinRange)
            {
                _stillStreak = 0f;
                _still = Mathf.Max(0f, _still - OutRangeDecayPerSecond * deltaTime);
                IsStill = false;

                return false;
            }

            if (!stationary)
            {
                _stillStreak = 0f;
                _still = Mathf.Max(0f, _still - InRangeDecayPerSecond * deltaTime);
                IsStill = false;

                return false;
            }

            _stillStreak += deltaTime;

            float rate = Mathf.Min(MaxChargeRate, BaseChargeRate + AccelPerSecond * _stillStreak);

            _still += rate * deltaTime;
            IsStill = true;

            if (_still < _healInterval) return false;

            _still = 0f;
            _stillStreak = 0f;

            return true;
        }
    }
}
