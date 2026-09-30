using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 移动执行器：把逻辑算出的速度落到实际物理上，并提供环境检测。
    /// </summary>
    /// <remarks>实现位于表现层（<c>Dasuus.Presentation</c>）；重力由逻辑层施加，本接口不做重力。</remarks>
    public interface IMovementMotor
    {
        /// <summary>当前速度（单位/秒）。</summary>
        Vector2 Velocity { get; }

        /// <summary>朝向：-1 左，1 右。</summary>
        int Facing { get; set; }

        /// <summary>本物理帧是否站在地面。</summary>
        bool IsGrounded { get; }

        /// <summary>本物理帧是否贴墙。</summary>
        bool IsTouchingWall { get; }

        /// <summary>以给定速度驱动一次移动。</summary>
        void Move(Vector2 velocity);
    }
}
