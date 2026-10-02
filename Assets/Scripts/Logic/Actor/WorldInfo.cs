using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 单个物理帧的环境事实，由组合根组装一次后喂进逻辑层。
    /// </summary>
    /// <remarks>
    /// 俯视角只承载两件事：本帧输入方向，与活动区域。
    /// 平台跳跃时代的"是否站地/是否贴墙/墙在哪个方向"已随 <c>MovementMotor</c> 的两条射线一起删除——
    /// 那三件只服务于重力与蹬墙跳，而俯视角的阻挡全部由刚体碰撞解算。
    /// 将来若有角色需要地形探测（悬崖巡逻），按那个角色的需求新增字段，不要预先塞回来。
    /// </remarks>
    public readonly struct WorldInfo
    {
        /// <summary>本帧输入方向，范围 -1..1；零输入时为 <c>Vector2.zero</c>。</summary>
        public readonly Vector2 MoveDirection;

        /// <summary>活动区域；未接线时 <see cref="BoundsArea.IsValid"/> 为 false。</summary>
        public readonly BoundsArea Bounds;

        public WorldInfo(Vector2 moveDirection, in BoundsArea bounds)
        {
            MoveDirection = moveDirection;
            Bounds = bounds;
        }
    }
}
