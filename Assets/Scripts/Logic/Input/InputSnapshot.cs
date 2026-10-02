using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    /// <summary>
    /// 单个采样帧的输入快照（不可变）。
    /// </summary>
    public readonly struct InputSnapshot
    {
        /// <summary>移动输入，约定范围 -1..1。</summary>
        public readonly Vector2 Move;

        /// <summary>本采样帧是否按下跳跃（按下沿，仅采样当帧为真）。</summary>
        public readonly bool JumpPressed;

        /// <summary>跳跃键是否按住（用于可变跳高的松键截断）。</summary>
        public readonly bool JumpHeld;

        /// <summary>本采样帧是否按下冲刺。</summary>
        public readonly bool DashPressed;

        /// <summary>抓墙键是否按住。</summary>
        public readonly bool GrabHeld;

        public InputSnapshot(
            Vector2 move,
            bool jumpPressed,
            bool jumpHeld,
            bool dashPressed,
            bool grabHeld
            )
        {
            Move = move;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            DashPressed = dashPressed;
            GrabHeld = grabHeld;
        }

        /// <summary>全零快照（无输入）。</summary>
        public static InputSnapshot Empty => new InputSnapshot(Vector2.zero, false, false, false, false);
    }

    public readonly struct UIInputSnapshot
    {
        public readonly bool EscPressed;
        public UIInputSnapshot(bool ep)
        {
            EscPressed = ep;
        }
    }
}
