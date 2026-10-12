using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    /// Button 的多边形 Raycast 区域：继承 Image，可作 Button 的 UI Graphic。Button 负责 Hover/Highlighted/Pressed/Click/Selected/Transition，本组件只决定鼠标是否落在有效区域内并在 Scene 视图画出多边形，故 Hover 与 Click 范围受 points 限制。points 用 0~1 归一化坐标（左下 (0,0)、右上 (1,1)），RectTransform 缩放时多边形同比缩放
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

        /// 重写 UI Raycast：Button 的 Hover / Click 都会受到这里的判断限制
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

        /// 判断本地坐标点是否位于 Polygon 内
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

        /// 0~1 归一化坐标 → RectTransform 本地坐标
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