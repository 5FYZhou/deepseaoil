using UnityEngine;

namespace DeepseaOil.Logic
{
    // 俯视角只承载两件事：本帧输入方向与活动区域。平台跳跃时代的"是否站地/是否贴墙/墙在哪个方向"
    // 已随玩家执行器的两条射线一起删除；将来若有角色需要地形探测，按那个角色的需求新增字段。
    public readonly struct WorldInfo
    {
        // 契约：非零时模长恒为 1（归一化由组合根在装配之前完成，见 PlayerController.FixedUpdate）；
        // 传进未归一化的值（键盘斜向的 (1,1)）会快 √2 倍：不报错，只是手感不对。
        public readonly Vector2 MoveDirection;

        public readonly BoundsArea Bounds;

        public WorldInfo(Vector2 moveDirection, in BoundsArea bounds)
        {
            MoveDirection = moveDirection;
            Bounds = bounds;
        }
    }
}
