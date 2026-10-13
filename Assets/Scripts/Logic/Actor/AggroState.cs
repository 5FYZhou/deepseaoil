using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>仇恨状态：视野内获得、维持距离内保持、超时丢失；不做视线遮挡</summary>
    public sealed class AggroState
    {
        // 距离是世界单位，延迟是秒
        private const float VisionRange = 10f;

        private const float SustainRange = 14f;

        private const float LoseDelay = 3f;

        private bool _hasAggro;
        private float _lastSeenTime;

        public bool HasAggro => _hasAggro;

        /// <summary>每物理帧调一次；target 为 null 时保持当前状态不清（无目标不主动丢仇恨）</summary>
        public void Tick(Vector2 self, Vector2? target, float now)
        {
            if (!target.HasValue) return;

            float sqrDist = (target.Value - self).sqrMagnitude;

            if (sqrDist <= VisionRange * VisionRange)
            {
                _hasAggro = true;
                _lastSeenTime = now;
                return;
            }

            if (!_hasAggro) return;

            if (sqrDist <= SustainRange * SustainRange)
            {
                _lastSeenTime = now;
                return;
            }

            if (now - _lastSeenTime >= LoseDelay) _hasAggro = false;
        }

        /// <summary>强制清仇恨，供 IAggroBreakable 调用</summary>
        public void ForceLose()
        {
            _hasAggro = false;
        }
    }
}
