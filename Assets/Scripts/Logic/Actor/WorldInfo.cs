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
        /// <summary>本帧输入方向：<b>已归一化</b>的单位向量；零输入时为 <c>Vector2.zero</c>。</summary>
        /// <remarks>
        /// 归一化由组合根在装配 <see cref="WorldInfo"/> 之前完成（见 <c>PlayerController.FixedUpdate</c>）。
        /// <b>契约：非零时模长恒为 1</b>——逻辑层据此可以直接用「方向 × 速度」，不必再防 √2 倍的斜向。
        /// 宿主若传进未归一化的值（如键盘斜向的 (1,1)），斜向速度会快 √2 倍：不报错，只是手感不对。
        /// </remarks>
        public readonly Vector2 MoveDirection;

        /// <summary>活动区域；未接线时 <see cref="BoundsArea.IsValid"/> 为 false。</summary>
        public readonly BoundsArea Bounds;

        /// <param name="moveDirection">本帧输入方向；非零时必须已归一化。</param>
        /// <param name="bounds">地图活动区域。</param>
        public WorldInfo(Vector2 moveDirection, in BoundsArea bounds)
        {
            MoveDirection = moveDirection;
            Bounds = bounds;
        }
    }
}
