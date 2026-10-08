using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动执行器，逻辑算出的速度落到物理体</summary>
    /// <remarks>速度=单位/秒，逻辑层账本产出、表现层实现；位置走物理体非transform；阻挡靠刚体碰撞</remarks>
    public interface IMovementMotor
    {
        Vector2 Velocity { get; }

        Vector2 Position { get; }

        /// <summary>朝向，zero=不改朝向</summary>
        Vector2 Facing { get; set; }

        /// <summary>驱动移动，zero=当帧停住</summary>
        void Move(Vector2 velocity);

        /// <summary>越界瞬移</summary>
        void SetPosition(Vector2 position);
    }
}
