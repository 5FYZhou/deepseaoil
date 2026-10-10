using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// Button 的多边形 Raycast 区域。
    ///
    /// 本组件继承自 Image，因此可以作为 Button 的 UI Graphic 使用。
    ///
    /// Button 本身负责：
    /// - Hover / Highlighted
    /// - Pressed
    /// - Click
    /// - Selected
    /// - Transition
    ///
    /// PolygonRaycastArea 负责：
    /// - 决定鼠标是否位于 Button 的有效区域
    /// - 在 Scene 视图显示多边形
    ///
    /// 因此 Button 的 Hover 和 Click 范围都会受到 points 的限制。
    ///
    /// points 使用 0~1 的归一化坐标：
    ///
    /// (0,1) ---------------- (1,1)
    ///   |                        |
    ///   |        Polygon         |
    ///   |                        |
    /// (0,0) ---------------- (1,0)
    ///
    /// 例如：
    ///
    /// points:
    ///
    ///     (0.15, 0.85)
    ///     (1.00, 1.00)
    ///     (1.00, 0.00)
    ///     (0.00, 0.15)
    ///
    /// 无论 RectTransform 如何改变大小，
    /// Polygon 都会按照相同比例缩放。
    /// </summary>
    [AddComponentMenu("DeepseaOil/UI/Polygon Raycast Area")]
    [RequireComponent(typeof(PolygonRaycastSettings))]
    public class PolygonRaycastArea : Image
    {
        private PolygonRaycastSettings _settings;

        protected override void Awake()
        {
            base.Awake();

            _settings =
                GetComponent<PolygonRaycastSettings>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _settings =
                GetComponent<PolygonRaycastSettings>();
        }

        /// <summary>
        /// 重写 UI Raycast。
        ///
        /// Button 的 Hover / Click 都会受到这里的判断限制。
        /// </summary>
        public override bool Raycast(
            Vector2 screenPoint,
            Camera eventCamera)
        {
            if (_settings == null)
                return false;

            IReadOnlyList<Vector2> points =
                _settings.Points;

            if (points == null || points.Count < 3)
                return false;

            if (!RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        rectTransform,
                        screenPoint,
                        eventCamera,
                        out Vector2 localPoint))
            {
                return false;
            }

            Rect rect = rectTransform.rect;

            return IsPointInPolygon(
                localPoint,
                points,
                rect);
        }

        /// <summary>
        /// 判断本地坐标点是否位于 Polygon 内。
        /// </summary>
        private static bool IsPointInPolygon(
            Vector2 point,
            IReadOnlyList<Vector2> normalizedPoints,
            Rect rect)
        {
            bool inside = false;

            for (int i = 0, j = normalizedPoints.Count - 1;
                 i < normalizedPoints.Count;
                 j = i++)
            {
                Vector2 a =
                    NormalizedToLocal(
                        normalizedPoints[i],
                        rect);

                Vector2 b =
                    NormalizedToLocal(
                        normalizedPoints[j],
                        rect);

                bool intersect =
                    ((a.y > point.y) != (b.y > point.y)) &&
                    (point.x <
                     (b.x - a.x) *
                     (point.y - a.y) /
                     (b.y - a.y) +
                     a.x);

                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// 0~1 归一化坐标
        /// →
        /// RectTransform 本地坐标。
        /// </summary>
        public static Vector2 NormalizedToLocal(
            Vector2 normalized,
            Rect rect)
        {
            return new Vector2(
                Mathf.Lerp(
                    rect.xMin,
                    rect.xMax,
                    normalized.x),

                Mathf.Lerp(
                    rect.yMin,
                    rect.yMax,
                    normalized.y));
        }
    }
}