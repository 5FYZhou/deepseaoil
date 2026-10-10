using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// Polygon Raycast 的配置数据。
    ///
    /// 单独拆出来是为了避免继承 Image 后，
    /// Unity 内置 ImageEditor 隐藏自定义字段。
    /// </summary>
    [AddComponentMenu("DeepseaOil/UI/Polygon Raycast Settings")]
    public class PolygonRaycastSettings : MonoBehaviour
    {
        [Header("Polygon")]

        [Tooltip("多边形顶点，使用 0~1 的归一化坐标")]
        [SerializeField]
        private List<Vector2> points = new()
        {
            new Vector2(0.15f, 0.85f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0.15f)
        };

        [Header("Scene Preview")]

        [Tooltip("是否在 Scene 视图显示多边形")]
        [SerializeField]
        private bool showPreview = true;

        [Tooltip("多边形边框颜色")]
        [SerializeField]
        private Color previewColor = new(0f, 1f, 0f, 0.8f);

        [Tooltip("顶点显示大小")]
        [SerializeField]
        private float pointRadius = 5f;

        public IReadOnlyList<Vector2> Points => points;

        public bool ShowPreview => showPreview;

        public Color PreviewColor => previewColor;

        public float PointRadius => pointRadius;

        public int PointCount => points.Count;

        public Vector2 GetPoint(int index)
        {
            return points[index];
        }

        public void SetPoint(int index, Vector2 point)
        {
            if (index < 0 || index >= points.Count)
                return;

            points[index] = ClampPoint(point);
        }

        public void AddPoint(Vector2 point)
        {
            points.Add(ClampPoint(point));
        }

        public bool RemoveLastPoint()
        {
            if (points.Count <= 3)
                return false;

            points.RemoveAt(points.Count - 1);
            return true;
        }

        public void ClearPoints()
        {
            points.Clear();
        }

        public void SetPointCount(int count)
        {
            count = Mathf.Max(3, count);

            while (points.Count < count)
            {
                points.Add(GetDefaultPoint(points.Count));
            }

            while (points.Count > count)
            {
                points.RemoveAt(points.Count - 1);
            }
        }

        private static Vector2 ClampPoint(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp01(point.x),
                Mathf.Clamp01(point.y));
        }

        private static Vector2 GetDefaultPoint(int index)
        {
            const float center = 0.5f;
            const float radius = 0.25f;

            float angle =
                index * Mathf.PI * 2f / 8f;

            return new Vector2(
                center + Mathf.Cos(angle) * radius,
                center + Mathf.Sin(angle) * radius);
        }

        private void OnValidate()
        {
            if (points == null)
            {
                points = new List<Vector2>();
            }

            for (int i = 0; i < points.Count; i++)
            {
                points[i] = ClampPoint(points[i]);
            }

            pointRadius = Mathf.Max(0.1f, pointRadius);
        }

        private void OnDrawGizmos()
        {
            if (!showPreview)
                return;

            if (points == null || points.Count == 0)
                return;

            RectTransform rectTransform =
                GetComponent<RectTransform>();

            if (rectTransform == null)
                return;

            Rect rect = rectTransform.rect;

            Vector3[] worldPoints =
                new Vector3[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 localPoint =
                    PolygonRaycastArea.NormalizedToLocal(
                        points[i],
                        rect);

                worldPoints[i] =
                    rectTransform.TransformPoint(
                        localPoint);
            }

            Gizmos.color = previewColor;

            // 边
            for (int i = 0; i < worldPoints.Length; i++)
            {
                int next =
                    (i + 1) % worldPoints.Length;

                Gizmos.DrawLine(
                    worldPoints[i],
                    worldPoints[next]);
            }

            // 点
            float size =
                Mathf.Min(
                    rect.width,
                    rect.height);

            float radius =
                Mathf.Max(
                    size * pointRadius * 0.001f,
                    0.01f);

            for (int i = 0; i < worldPoints.Length; i++)
            {
                Gizmos.DrawSphere(
                    worldPoints[i],
                    radius);
            }
        }
    }
}