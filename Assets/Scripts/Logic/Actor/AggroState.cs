using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>仇恨状态：视野内获得、维持距离内保持、超时丢失</summary>
    public sealed class AggroState
    {
        private readonly float _visionRange;

        private readonly float _sustainRange;

        private readonly float _loseDelay;

        private bool _hasAggro;
        private float _lastSeenTime;

        /// <summary>清仇恨抑制窗，删了白雾只生效 1 帧</summary>
        private bool _suppressAgro;

        private float _suppressUntil;

        public AggroState(float visionRange, float sustainRange, float loseDelay)
        {
            _visionRange = visionRange;
            _sustainRange = sustainRange;
            _loseDelay = loseDelay;
        }

        public bool HasAggro => _hasAggro;

        /// <summary>每物理帧调；target 为空时保持现状</summary>
        public void Tick(Vector2 self, Vector2? target, float now)
        {
            if (!target.HasValue) return;

            if (_suppressAgro)
            {
                _suppressAgro = false;
                _hasAggro = false;
                _suppressUntil = now + _loseDelay;
            }

            float sqrDist = (target.Value - self).sqrMagnitude;

            if (sqrDist <= _visionRange * _visionRange && now >= _suppressUntil)
            {
                _hasAggro = true;
                _lastSeenTime = now;
                return;
            }

            if (!_hasAggro) return;

            if (sqrDist <= _sustainRange * _sustainRange)
            {
                _lastSeenTime = now;
                return;
            }

            if (now - _lastSeenTime >= _loseDelay) _hasAggro = false;
        }

        /// <summary>强制清仇恨，供 IAggroBreakable 用</summary>
        public void ForceLose()
        {
            _hasAggro = false;
            _suppressAgro = true;
        }
    }
}
