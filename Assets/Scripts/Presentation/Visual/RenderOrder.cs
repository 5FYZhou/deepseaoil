using DeepseaOil.Foundation;

namespace DeepseaOil.Presentation.Visual
{
    public static class RenderOrder
    {
        public const int Ground = 100;

        public const int TileEffect = 300;

        /// <summary>贴地件的固定层（310）：球的阴影、掉落物的影子、贴地指示物；<b>不参与 Y-Sort</b>（跟着 y 取档会让阴影随高度越过自己的主人）。</summary>
        public const int GroundShadow = 310;

        /// <summary>瞄准反馈层（320）：地面标记 —— 压着格效果，但被站在那一格上的人盖住。</summary>
        public const int Aim = 320;

        public const int YSortBandStart = 500;

        public const int YSortBandEnd = 559;

        /// <summary>每世界单位几档（0.25 米一档）：频带 60 档 ÷ 4 ＝ 覆盖 15 个世界单位，超出部分钳在频带两端（见 <see cref="YSort.OrderFor"/>）。调它必须同时看这个除法。</summary>
        public const float YSortLevelsPerUnit = 4f;

        /// <summary>头顶读数层（耐久数字）；必须高于频带上沿，否则会被邻居的身体盖住。</summary>
        public const int ActorOverlay = 560;

        public const int ShatterPiece = 1200;

        /// <summary>视觉件使用的 Unity layer（0 = Default）。</summary>
        public const int OverlayLayer = 0;

        /// <summary>按世界 y 取档：y 越小档位越大、越晚画（方向口径见 <see cref="YSort"/>）。</summary>
        public static int ActorOrder(float y)
        {
            return YSort.OrderFor(y, YSortBandStart, YSortBandEnd, YSortLevelsPerUnit);
        }

        /// <summary>球的档位：<b>与角色同一频带</b>；取球的<b>贴地位置</b>的 y，不是它在弧线上的视觉高度。</summary>
        public static int BallOrder(float groundY)
        {
            return ActorOrder(groundY);
        }
    }
}
