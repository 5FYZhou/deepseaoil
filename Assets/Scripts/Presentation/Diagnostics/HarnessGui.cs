using UnityEngine;

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <summary>诊断面板共用的 GUI 口径：放大倍数按屏幕收口 ＋ 面板命中矩形</summary>
    /// <remarks>
    /// 1920×1080 下 IMGUI 默认 12px 字号太小，故整体放大；但面板占的是固定像素、世界内容按分辨率缩放，
    /// 放大后的左上角面板会盖住被测对象（木桩 / 棋盘），点下去还会被判成点面板，故倍数必须按屏幕收口。
    /// 不包 `#if`：不进正式构建的 Harness 与仍会进构建的 MovementDebugPanel 要用同一份算法。
    /// </remarks>
    internal static class HarnessGui
    {
        /// <summary>面板最宽占屏幕比例。必须小于 0.5：被测对象在正中，越过中线就被盖住</summary>
        private const float MaxWidthRatio = 0.47f;

        /// <summary>面板最高占屏幕比例：最长的面板（地块反应切片 520 设计像素）放 2 倍是 1040，留出 24px 边距</summary>
        private const float MaxHeightRatio = 0.97f;

        /// <summary>实际放大倍数：guiScale 读成 0（旧场景没有这个序列化字段）时按 2 倍兜底，下限 1 倍</summary>
        internal static float Scale(float guiScale, Vector2 panelSize)
        {
            float want = guiScale > 0f ? guiScale : 2f;

            float byWidth = Screen.width * MaxWidthRatio / Mathf.Max(panelSize.x, 1f);
            float byHeight = Screen.height * MaxHeightRatio / Mathf.Max(panelSize.y, 1f);

            return Mathf.Max(1f, Mathf.Min(want, Mathf.Min(byWidth, byHeight)));
        }

        /// <summary>面板在屏幕上的实际矩形（IMGUI 坐标，左上原点）：内容按设计值写、放大交给 GUI.matrix，命中判定得乘回倍数</summary>
        internal static Rect ScreenRect(Vector2 panelOrigin, Vector2 panelSize, float scale)
        {
            return new Rect(panelOrigin.x * scale, panelOrigin.y * scale, panelSize.x * scale, panelSize.y * scale);
        }
    }
}
