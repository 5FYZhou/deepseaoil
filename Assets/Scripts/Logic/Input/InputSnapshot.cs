using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    /// <summary>
    /// 单个采样帧的输入快照（不可变）。
    /// </summary>
    /// <remarks>
    /// 平台跳跃品类删掉后 <c>JumpPressed</c> 一路没有消费者，已清除；
    /// 键位映射仍留在 <c>InputSys.inputactions</c>（改资产要重新生成 <c>InputSys.cs</c>）。
    /// </remarks>
    public readonly struct InputSnapshot
    {
        /// <summary>移动输入，约定范围 -1..1。</summary>
        public readonly Vector2 Move;

        /// <summary>本采样帧是否按下冲刺。</summary>
        public readonly bool DashPressed;

        /// <summary>抓墙键是否按住。</summary>
        public readonly bool GrabHeld;

        public InputSnapshot(
            Vector2 move,
            bool dashPressed,
            bool grabHeld
            )
        {
            Move = move;
            DashPressed = dashPressed;
            GrabHeld = grabHeld;
        }

        /// <summary>全零快照（无输入）。</summary>
        public static InputSnapshot Empty => new InputSnapshot(Vector2.zero, false, false);
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
