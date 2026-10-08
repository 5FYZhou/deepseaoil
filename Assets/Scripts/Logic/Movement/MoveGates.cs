using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动层门禁，上层只提交、写速度只有移动层；ForcedVelocity 整条接管速度（0=硬停），SpeedScale 只缩放（0=定住，方向仍归移动层）夹到0..1缺省1，同时提交强制优先，default=None 空门禁</summary>
    public readonly struct MoveGates
    {
        public readonly bool HasForcedVelocity;

        public readonly Vector2 ForcedVelocity;

        public readonly bool HasSpeedScale;

        private readonly float _speedScale;

        private MoveGates(Vector2 forcedVelocity, bool hasForcedVelocity, bool hasSpeedScale, float speedScale)
        {
            ForcedVelocity = forcedVelocity;
            HasForcedVelocity = hasForcedVelocity;
            HasSpeedScale = hasSpeedScale;
            _speedScale = speedScale;
        }

        public float SpeedScale => HasSpeedScale
            ? (_speedScale <= 0f ? 0f : (_speedScale > 1f ? 1f : _speedScale))
            : 1f;

        public static MoveGates Forced(Vector2 forcedVelocity)
        {
            return new MoveGates(forcedVelocity, true, false, 1f);
        }

        public static MoveGates Scaled(float speedScale)
        {
            return new MoveGates(default, false, true, speedScale);
        }

        public static MoveGates None => default;
    }
}
