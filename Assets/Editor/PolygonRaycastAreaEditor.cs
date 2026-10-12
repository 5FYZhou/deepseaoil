//using UnityEditor;
//using UnityEngine;

//namespace DeepseaOil.EditorTools
//{
//    [CustomEditor(typeof(PolygonRaycastArea))]
//    public class PolygonRaycastAreaEditor : UnityEditor.Editor
//    {
//        private PolygonRaycastArea area;

//        private SerializedProperty pointsProperty;
//        private SerializedProperty showPreviewProperty;
//        private SerializedProperty previewColorProperty;

//        private void OnEnable()
//        {
//            area = (PolygonRaycastArea)target;

//            pointsProperty =
//                serializedObject.FindProperty("points");

//            showPreviewProperty =
//                serializedObject.FindProperty("showPreview");

//            previewColorProperty =
//                serializedObject.FindProperty("previewColor");

//            SceneView.duringSceneGui += OnSceneGUI;
//        }

//        private void OnDisable()
//        {
//            SceneView.duringSceneGui -= OnSceneGUI;
//        }

//        public override void OnInspectorGUI()
//        {
//            serializedObject.Update();

//            EditorGUILayout.Space();

//            EditorGUILayout.LabelField(
//                "Polygon",
//                EditorStyles.boldLabel);

//            EditorGUILayout.PropertyField(
//                showPreviewProperty,
//                new GUIContent("Show Preview"));

//            if (showPreviewProperty.boolValue)
//            {
//                EditorGUILayout.PropertyField(
//                    previewColorProperty,
//                    new GUIContent("Preview Color"));
//            }

//            EditorGUILayout.Space();

//            DrawPointList();

//            EditorGUILayout.Space();

//            DrawPointButtons();

//            serializedObject.ApplyModifiedProperties();
//        }

//        private void DrawPointList()
//        {
//            EditorGUILayout.LabelField(
//                $"Points ({pointsProperty.arraySize})",
//                EditorStyles.boldLabel);

//            for (int i = 0;
//                 i < pointsProperty.arraySize;
//                 i++)
//            {
//                SerializedProperty point =
//                    pointsProperty.GetArrayElementAtIndex(i);

//                EditorGUILayout.BeginHorizontal();

//                EditorGUILayout.LabelField(
//                    $"Point {i}",
//                    GUILayout.Width(60));

//                EditorGUILayout.PropertyField(
//                    point,
//                    GUIContent.none);

//                if (GUILayout.Button(
//                        "×",
//                        GUILayout.Width(25)))
//                {
//                    if (pointsProperty.arraySize > 3)
//                    {
//                        pointsProperty.DeleteArrayElementAtIndex(i);
//                        break;
//                    }
//                }

//                EditorGUILayout.EndHorizontal();
//            }
//        }

//        private void DrawPointButtons()
//        {
//            EditorGUILayout.BeginHorizontal();

//            if (GUILayout.Button("Add Point"))
//            {
//                AddPoint();
//            }

//            GUI.enabled = area.PointCount > 3;

//            if (GUILayout.Button("Remove Last"))
//            {
//                RemoveLastPoint();
//            }

//            GUI.enabled = true;

//            EditorGUILayout.EndHorizontal();

//            EditorGUILayout.Space();

//            if (GUILayout.Button("Reset To Trapezoid"))
//            {
//                ResetToTrapezoid();
//            }
//        }

//        private void AddPoint()
//        {
//            Undo.RecordObject(
//                area,
//                "Add Polygon Point");

//            Vector2 point;

//            if (area.PointCount == 0)
//            {
//                point = new Vector2(0.5f, 0.5f);
//            }
//            else
//            {
//                Vector2 last =
//                    area.GetPoint(area.PointCount - 1);

//                point = last + new Vector2(0.05f, 0.05f);

//                point.x = Mathf.Clamp01(point.x);
//                point.y = Mathf.Clamp01(point.y);
//            }

//            area.AddPoint(point);

//            EditorUtility.SetDirty(area);

//            SceneView.RepaintAll();
//        }

//        private void RemoveLastPoint()
//        {
//            Undo.RecordObject(
//                area,
//                "Remove Polygon Point");

//            area.RemoveLastPoint();

//            EditorUtility.SetDirty(area);

//            SceneView.RepaintAll();
//        }

//        private void ResetToTrapezoid()
//        {
//            Undo.RecordObject(
//                area,
//                "Reset Polygon");

//            area.ClearPoints();

//            area.AddPoint(
//                new Vector2(0.15f, 0.85f));

//            area.AddPoint(
//                new Vector2(1f, 1f));

//            area.AddPoint(
//                new Vector2(1f, 0f));

//            area.AddPoint(
//                new Vector2(0f, 0.15f));

//            EditorUtility.SetDirty(area);

//            SceneView.RepaintAll();
//        }

//        private void OnSceneGUI(SceneView sceneView)
//        {
//            if (area == null)
//                return;

//            if (!area.ShowPreview)
//                return;

//            if (area.PointCount < 1)
//                return;

//            Transform transform =
//                area.transform;

//            Rect rect =
//                area.rectTransform.rect;

//            int count =
//                area.PointCount;

//            Vector3[] worldPoints =
//                new Vector3[count];

//            for (int i = 0; i < count; i++)
//            {
//                Vector2 localPoint =
//                    PolygonRaycastArea.NormalizedToLocal(
//                        area.GetPoint(i),
//                        rect);

//                worldPoints[i] =
//                    transform.TransformPoint(
//                        localPoint);
//            }

//            DrawPolygonLines(
//                worldPoints,
//                area.PreviewColor);

//            DrawPolygonHandles(
//                worldPoints,
//                transform,
//                rect);

//            DrawPointLabels(
//                worldPoints);
//        }

//        private static void DrawPolygonLines(
//            Vector3[] worldPoints,
//            Color color)
//        {
//            Handles.color = color;

//            for (int i = 0;
//                 i < worldPoints.Length;
//                 i++)
//            {
//                Vector3 current =
//                    worldPoints[i];

//                Vector3 next =
//                    worldPoints[
//                        (i + 1) %
//                        worldPoints.Length];

//                Handles.DrawLine(
//                    current,
//                    next,
//                    2f);
//            }
//        }

//        private void DrawPolygonHandles(
//            Vector3[] worldPoints,
//            Transform transform,
//            Rect rect)
//        {
//            for (int i = 0;
//                 i < worldPoints.Length;
//                 i++)
//            {
//                Vector3 current =
//                    worldPoints[i];

//                float size =
//                    HandleUtility.GetHandleSize(
//                        current) * 0.08f;

//                EditorGUI.BeginChangeCheck();

//                Vector3 newWorldPosition =
//                    Handles.FreeMoveHandle(
//                        current,
//                        size,
//                        Vector3.zero,
//                        Handles.DotHandleCap);

//                if (!EditorGUI.EndChangeCheck())
//                    continue;

//                Undo.RecordObject(
//                    area,
//                    "Move Polygon Point");

//                Vector3 localPosition =
//                    transform.InverseTransformPoint(
//                        newWorldPosition);

//                Vector2 normalized =
//                    new Vector2(

//                        Mathf.InverseLerp(
//                            rect.xMin,
//                            rect.xMax,
//                            localPosition.x),

//                        Mathf.InverseLerp(
//                            rect.yMin,
//                            rect.yMax,
//                            localPosition.y)
//                    );

//                area.SetPoint(
//                    i,
//                    normalized);

//                EditorUtility.SetDirty(area);

//                SceneView.RepaintAll();
//            }
//        }

//        private static void DrawPointLabels(
//            Vector3[] worldPoints)
//        {
//            for (int i = 0;
//                 i < worldPoints.Length;
//                 i++)
//            {
//                Handles.Label(
//                    worldPoints[i],
//                    $" {i}");
//            }
//        }
//    }
//}
