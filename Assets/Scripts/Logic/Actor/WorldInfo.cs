using UnityEngine;

namespace DeepseaOil.Logic
{
    // 俯视角只承载两件事：本帧输入方向与活动区域
    public readonly struct WorldInfo
    {
        // 契约：非零时模长恒为 1（归一化由组合根在装配前完成）；传未归一化的值会快 √2 倍，不报错只是手感不对
        public readonly Vector2 MoveDirection;

        public readonly BoundsArea Bounds;

        public WorldInfo(Vector2 moveDirection, in BoundsArea bounds)
        {
            MoveDirection = moveDirection;
            Bounds = bounds;
        }
    }
}
