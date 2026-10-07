using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动执行器：把逻辑算出的速度落到实际物理上。</summary>
    /// <remarks>
    /// 速度单位为"单位/秒"，全部由逻辑层账本给出；实现位于表现层，引擎重力由实现侧整体关掉
    /// （<c>rigidbody2D.gravityScale = 0</c>）。
    /// 无环境检测：阻挡由刚体碰撞解算。位置读写走物理体而非 <c>transform</c>。
    /// </remarks>
    public interface IMovementMotor
    {
        /// <summary>当前速度（单位/秒）。</summary>
        Vector2 Velocity { get; }

        /// <summary>物理体当前位置。</summary>
        Vector2 Position { get; }

        /// <summary>朝向：世界方向向量；<see cref="Vector2.zero"/> 表示"不改朝向"。</summary>
        Vector2 Facing { get; set; }

        /// <summary>以给定速度驱动一次移动；<see cref="Vector2.zero"/> 即当帧停住。</summary>
        void Move(Vector2 velocity);

        /// <summary>瞬移物理体（边界钳位用）；只在越界时调用。</summary>
        void SetPosition(Vector2 position);
    }
}
