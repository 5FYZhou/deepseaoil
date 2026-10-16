#if UNITY_EDITOR
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.EditorTools.Lighting
{
    /// <summary>动态点光源系统一键装配工具</summary>
    /// <remarks>点光必须落成预制体：Light2D 的照射层是私有序列化字段、运行期无公共访问器，只有这里能可靠写对。</remarks>
    public static class PointLightSetupTool
    {
        private const string MenuPath = "DeepseaOil/光照与后处理/一键装配动态点光源系统";
        private const string DebugMenuPath = "DeepseaOil/光照与后处理/诊断/统计当前点光源";
        private const string PrefabPath = "Assets/Resources/lighting/TilePointLight.prefab";
        private const string SortingLayersField = "m_ApplyToSortingLayers";
        private const string EnvironmentRootName = "[Environment]";

        [MenuItem(MenuPath)]
        public static void SetupTileLightDirector()
        {
            if (!ValidateSortingLayers()) return;

            if (!EnsureLightPrefab()) return;

            WireSceneDirector();

            AssetDatabase.SaveAssets();

            Debug.Log($"[Light] 动态点光源系统已就位：{PrefabPath} 已就绪，[Environment] 上已挂载并接线 TileLightDirector。");
        }

        /// <summary>诊断：打印板上点光数，判断"没效果"是没借出还是被渲染器上限吃掉</summary>
        [MenuItem(DebugMenuPath)]
        public static void ReportPointLightCount()
        {
            TileLightDirector[] directors = Object.FindObjectsOfType<TileLightDirector>();

            if (directors.Length == 0)
            {
                Debug.Log("[Light] 当前场景没有 TileLightDirector：环境基座未装配（PlayMode 下 CombatRoot 会自建一个）。");
                return;
            }

            for (int i = 0; i < directors.Length; i++)
            {
                TileLightDirector director = directors[i];

                Debug.Log(
                    $"[Light] {director.gameObject.name}：发光格 {director.LitCellCount} 个 → 借出点光 {director.ActiveLightCount} 盏" +
                    $"（其中连片火海占 {director.FireGroupCount} 盏氛围灯；渲染器一次只混合 16 盏，超出的灯不贡献光且不报错）。", director);
            }

            Light2D[] lights = Object.FindObjectsOfType<Light2D>();
            int pointLights = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Point) pointLights++;
            }

            Debug.Log($"[Light] 场景内 Light2D 共 {lights.Length} 盏，其中 Point {pointLights} 盏（含池中未借出的）。");
        }

        /// <summary>Sorting Layer 缺失则中止，绝不代写 ProjectSettings/TagManager.asset</summary>
        private static bool ValidateSortingLayers()
        {
            int groundId = SortingLayer.NameToID(SortingLayerConsts.Ground);
            int actorId = SortingLayer.NameToID(SortingLayerConsts.Actor);

            if (groundId >= 0 && actorId >= 0) return true;

            Debug.LogError("[Light] Ground / Actor 未在 Tags and Layers 登记：点光照射层无法写入，已中止。");

            return false;
        }

        private static bool EnsureLightPrefab()
        {
            int groundId = SortingLayer.NameToID(SortingLayerConsts.Ground);
            int actorId = SortingLayer.NameToID(SortingLayerConsts.Actor);

            GameObject root = new GameObject("TilePointLight");

            try
            {
                Light2D light = root.AddComponent<Light2D>();

                light.lightType = Light2D.LightType.Point;
                light.color = Color.white;
                light.intensity = 1f;
                light.pointLightOuterRadius = 1.8f;
                light.pointLightInnerRadius = 0.36f;

                if (!ApplySortingLayers(light, groundId, actorId)) return false;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return true;
        }

        /// <summary>照射层只能经私有序列化字段写入；配错会让点光漏照 Effect/UI 且不报错</summary>
        private static bool ApplySortingLayers(Light2D light, int firstId, int secondId)
        {
            SerializedObject serialized = new SerializedObject(light);
            SerializedProperty layers = serialized.FindProperty(SortingLayersField);

            if (layers == null || !layers.isArray)
            {
                Debug.LogError($"[Light] Light2D 上找不到 {SortingLayersField}（URP 版本可能已改字段名），照射层未写入。");

                return false;
            }

            layers.arraySize = 2;
            layers.GetArrayElementAtIndex(0).intValue = firstId;
            layers.GetArrayElementAtIndex(1).intValue = secondId;

            serialized.ApplyModifiedProperties();

            return true;
        }

        private static void WireSceneDirector()
        {
            GameObject root = GameObject.Find(EnvironmentRootName);

            if (root == null)
            {
                root = new GameObject(EnvironmentRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create [Environment] Root");
            }

            TileLightDirector director = root.GetComponent<TileLightDirector>();

            if (director == null)
            {
                director = root.AddComponent<TileLightDirector>();
                Undo.RegisterCreatedObjectUndo(director, "Add TileLightDirector");
            }

            TilemapAdapter adapter = Object.FindObjectOfType<TilemapAdapter>();

            if (adapter == null)
            {
                Debug.LogWarning("[Light] 场景里找不到 TilemapAdapter，TileLightDirector 的几何接线留空，运行期会自愈抓取。");
            }
            else
            {
                SerializedObject serialized = new SerializedObject(director);
                SerializedProperty property = serialized.FindProperty("tilemapAdapter");

                if (property == null)
                {
                    Debug.LogError("[Light] TileLightDirector 上找不到 tilemapAdapter 字段，接线未完成。");
                }
                else
                {
                    property.objectReferenceValue = adapter;
                    serialized.ApplyModifiedProperties();
                }
            }

            EditorUtility.SetDirty(root);
        }
    }
}
#endif
