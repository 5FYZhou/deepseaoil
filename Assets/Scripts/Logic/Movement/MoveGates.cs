using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动层的门禁：上层（状态效果层 / 战斗层）只能提交请求，只有移动层有写速度的权限（速度只有一个写者）。</summary>
    /// <remarks>
    /// 两把锁：<see cref="ForcedVelocity"/> 整条接管本帧速度（<c>0</c> = 硬停）；<see cref="SpeedScale"/> 只缩放（<c>0</c> = 定住，方向仍归移动层），越界夹到 <c>0..1</c>、没带乘数读 <c>1</c>。
    /// 两者同时提交时强制速度优先；<c>default</c> 是合法空门禁，<see cref="None"/> 即 <c>default</c>。
    /// </remarks>
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
