using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    /// <summary>单个采样帧的输入快照（不可变），键位映射在 InputSys.inputactions</summary>
    public readonly struct InputSnapshot
    {
        /// <summary>移动输入，约定范围 -1..1。</summary>
        public readonly Vector2 Move;

        /// <summary>本采样帧是否按下冲刺</summary>
        public readonly bool DashPressed;

        public InputSnapshot(
            Vector2 move,
            bool dashPressed
            )
        {
            Move = move;
            DashPressed = dashPressed;
        }

        public static InputSnapshot Empty => new InputSnapshot(Vector2.zero, false);
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
