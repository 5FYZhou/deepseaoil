#if UNITY_EDITOR
using System.Collections.Generic;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.EditorTools.Lighting
{
    /// <summary>场景光照装配：环境基座、网格光场、受光材质与后处理封印一次装齐</summary>
    /// <remarks>光照只有这一套管线：Global Light 2D 给底色，TileGridLightmap 逐格注入，后处理压暗部。旧点光池在装配时被物理移除——残留组件会让人误以为点光源仍是生效配置。</remarks>
    public static class LightingSetupTool
    {
        private const string MenuPath = "DeepseaOil/光照/装配当前场景";
        private const string DiagnoseMenuPath = "DeepseaOil/光照/诊断";
        private const string EnvironmentRootName = "[Environment]";
        private const string GlobalLightName = "Global Light 2D";
        private const string GlobalVolumeName = "Global Volume";
        private const string ProfileAssetPath = "Assets/Settings/DefaultGlobalVolumeProfile.asset";
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string TileFlowMatPath = "Assets/Resources/materials/M_TileFlow_Water.mat";
        private const string UnlitMatPath = "Assets/Resources/materials/M_SpriteFlash.mat";
        private const string TilemapAdapterPrefabPath = "Assets/Resources/scane_prebs/TilemapAdapter.prefab";
        private const string CelLitShaderName = "DeepseaOil/2D/CelLit";
        private const string TileFlowShaderName = "DeepseaOil/2D/TileFlow";
        private const string SortingLayersField = "m_ApplyToSortingLayers";

        [MenuItem(MenuPath)]
        public static void AssembleScene()
        {
            if (!ValidateSortingLayers()) return;

            GameObject root = EnsureEnvironmentRoot();
            Light2D globalLight = EnsureGlobalLight(root);

            if (globalLight == null) return;

            EnsureGlobalVolume(root);
            EnsureEnvironmentDirector(root, globalLight);
            EnableCameraPostProcessing();
            RemoveLegacyPool(root);
            EnsureGridLightmap(root);
            WireOverlayMaterial(EnsureMaterials());

            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[Lighting] 场景光照装配完成：[Environment] 上已就位 Global Light 2D / Global Volume / EnvironmentDirector / TileGridLightmap，" +
                "受光材质已建齐，后处理封印值已写入。");
        }

        /// <summary>诊断：网格光场规模、Light2D 计数、残留旧组件与两处静默失效点</summary>
        [MenuItem(DiagnoseMenuPath)]
        public static void Diagnose()
        {
            TileGridLightmap[] lightmaps = Object.FindObjectsOfType<TileGridLightmap>();

            if (lightmaps.Length == 0)
            {
                Debug.Log("[Lighting] 当前场景没有 TileGridLightmap：PlayMode 下 CombatRoot 会自建一个，编辑态需跑装配菜单。");
            }

            for (int i = 0; i < lightmaps.Length; i++)
            {
                TileGridLightmap lightmap = lightmaps[i];

                Debug.Log(
                    $"[Lighting] {lightmap.gameObject.name}：光照图 {lightmap.TexelWidth}×{lightmap.TexelHeight} 纹素，" +
                    $"发光格 {lightmap.EmitterCellCount} 个，光阻格 {lightmap.BlockedCellCount} 个（连片火海共用一个光区，无灯具数量上限）。", lightmap);
            }

            Light2D[] lights = Object.FindObjectsOfType<Light2D>();
            int globalLights = 0;
            int otherLights = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Global) globalLights++;
                else otherLights++;
            }

            Debug.Log($"[Lighting] Light2D 统计：全局 {globalLights} 盏（应为 1），点光/其它 {otherLights} 盏（应为 0——网格场不产生 Light2D）。");

            EnvironmentDirector director = Object.FindObjectOfType<EnvironmentDirector>();

            if (director == null)
            {
                Debug.LogWarning("[Lighting] 场景里没有 EnvironmentDirector：时段不会推进，Global Light 2D 会一直停在场景里手写的颜色。");
            }

            Camera camera = Camera.main;
            bool postProcessing = camera != null && camera.GetUniversalAdditionalCameraData().renderPostProcessing;

            Debug.Log($"[Lighting] 相机后处理：{(postProcessing ? "已开" : "未开——Global Volume 完全不生效且不报错")}。");

            TileLightDirector[] legacy = Object.FindObjectsOfType<TileLightDirector>();

            if (legacy.Length > 0)
            {
                Debug.LogWarning($"[Lighting] 场景里还有 {legacy.Length} 个 TileLightDirector：点光池已停用，跑一次装配菜单会物理移除。");
            }
        }

        /// <summary>Sorting Layer 缺失则中止整个装配，绝不代写 ProjectSettings/TagManager.asset</summary>
        private static bool ValidateSortingLayers()
        {
            string[] names =
            {
                SortingLayerConsts.Ground,
                SortingLayerConsts.Actor,
                SortingLayerConsts.Aim,
                SortingLayerConsts.Effect,
                SortingLayerConsts.UI,
            };

            string missing = string.Empty;

            for (int i = 0; i < names.Length; i++)
            {
                if (SortingLayer.NameToID(names[i]) >= 0) continue;

                missing += missing.Length == 0 ? names[i] : ", " + names[i];
            }

            if (missing.Length == 0) return true;

            Debug.LogError(
                $"[Lighting] 未登记的 Sorting Layer：{missing}。请在 Edit ▸ Project Settings ▸ Tags and Layers ▸ Sorting Layers " +
                "按 Default/Ground/Actor/Aim/Effect/UI 的顺序补齐后重跑本菜单（本工具不代写 TagManager）。");

            return false;
        }

        private static GameObject EnsureEnvironmentRoot()
        {
            GameObject root = GameObject.Find(EnvironmentRootName);

            if (root != null) return root;

            root = new GameObject(EnvironmentRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create [Environment] Root");

            return root;
        }

        private static Light2D EnsureGlobalLight(GameObject root)
        {
            Transform existing = root.transform.Find(GlobalLightName);

            GameObject lightGo;

            if (existing != null)
            {
                lightGo = existing.gameObject;
            }
            else
            {
                lightGo = new GameObject(GlobalLightName);
                lightGo.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Global Light 2D");
            }

            Light2D light2d = lightGo.GetComponent<Light2D>();

            if (light2d == null) light2d = lightGo.AddComponent<Light2D>();

            light2d.lightType = Light2D.LightType.Global;

            TimeOfDaySetting day = TimeOfDaySetting.GetDefault(TimeOfDayKind.Day);
            light2d.color = day.LightColor;
            light2d.intensity = day.Intensity;

            if (!ApplyLightMask(light2d)) return null;

            EditorUtility.SetDirty(light2d);

            return light2d;
        }

        /// <summary>向 Light2D 的私有序列化字段写照射层；落到失效 ID 会让光照静默全层生效</summary>
        private static bool ApplyLightMask(Light2D light2d)
        {
            int firstId = SortingLayer.NameToID(SortingLayerConsts.Ground);
            int secondId = SortingLayer.NameToID(SortingLayerConsts.Actor);

            if (firstId < 0 || secondId < 0)
            {
                Debug.LogError($"[Lighting] {SortingLayerConsts.Ground} / {SortingLayerConsts.Actor} 的 Sorting Layer ID 无效，光照层未写入。");

                return false;
            }

            SerializedObject serialized = new SerializedObject(light2d);
            SerializedProperty layers = serialized.FindProperty(SortingLayersField);

            if (layers == null || !layers.isArray)
            {
                Debug.LogError($"[Lighting] Light2D 上找不到 {SortingLayersField}（URP 版本可能已改字段名），照射层未写入。");

                return false;
            }

            layers.arraySize = 2;
            layers.GetArrayElementAtIndex(0).intValue = firstId;
            layers.GetArrayElementAtIndex(1).intValue = secondId;

            serialized.ApplyModifiedProperties();

            return true;
        }

        private static void EnsureGlobalVolume(GameObject root)
        {
            Transform existing = root.transform.Find(GlobalVolumeName);

            GameObject volumeGo;

            if (existing != null)
            {
                volumeGo = existing.gameObject;
            }
            else
            {
                volumeGo = new GameObject(GlobalVolumeName);
                volumeGo.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(volumeGo, "Create Global Volume");
            }

            // 相机 Volume Mask 只认 Default 层，放进别的层后处理会完全不生效
            volumeGo.layer = 0;

            Volume volume = volumeGo.GetComponent<Volume>();

            if (volume == null) volume = volumeGo.AddComponent<Volume>();

            volume.isGlobal = true;

            VolumeProfile profile = LoadOrRebuildProfile();

            ApplySealedValues(profile);

            volume.sharedProfile = profile;

            // 组件没落成 SubAsset 时 URP 后处理栈会静默失败并把 Game 视图涂黑
            if (profile.components.Count == 0)
            {
                Debug.LogError("[Lighting] Volume Profile 组件未落盘，后处理会静默失效。检查 AssetDatabase.AddObjectToAsset 是否被跳过。");
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(volumeGo);
        }

        /// <summary>取已有 profile；历史坏资产（components 全为 null 占位）直接删除重建</summary>
        private static VolumeProfile LoadOrRebuildProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileAssetPath);

            if (profile != null)
            {
                List<VolumeComponent> registered = profile.components;

                for (int i = registered.Count - 1; i >= 0; i--)
                {
                    if (registered[i] == null) registered.RemoveAt(i);
                }

                if (registered.Count > 0) return profile;

                AssetDatabase.DeleteAsset(ProfileAssetPath);
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfileAssetPath);
            AssetDatabase.SaveAssets();

            return profile;
        }

        /// <summary>Add 只造内存对象；不挂成 SubAsset 则 profile 落盘后组件全是 null</summary>
        private static T EnsureComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;

            T component = profile.Add<T>(true);

            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            return component;
        }

        /// <summary>从 profile 与其资产文件里一并抹掉某个覆盖项：只置 active=false 会把它继续留在磁盘上</summary>
        private static void RemoveComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) return;

            profile.components.Remove(component);
            AssetDatabase.RemoveObjectFromAsset(component);

            UnityEngine.Object.DestroyImmediate(component, true);
            EditorUtility.SetDirty(profile);
        }

        private static void ApplySealedValues(VolumeProfile profile)
        {
            // 2D 手绘色块不用 3D HDR 曲线：Neutral 会把纯色块的饱和度与对比度二次压缩
            RemoveComponent<Tonemapping>(profile);

            ColorAdjustments colorAdjustments = EnsureComponent<ColorAdjustments>(profile);

            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = 12f;

            Bloom bloom = EnsureComponent<Bloom>(profile);

            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.45f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.60f;

            Vignette vignette = EnsureComponent<Vignette>(profile);

            vignette.active = true;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.118f, 0.118f, 0.114f, 1f);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.22f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.40f;

            EditorUtility.SetDirty(profile);
        }

        /// <summary>相机后处理默认关闭；不打开则 Global Volume 完全不起作用且不报任何错</summary>
        private static void EnableCameraPostProcessing()
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                Debug.LogError("[Lighting] 场景里没有 MainCamera 标签的相机，后处理未开启，Global Volume 不会生效。");

                return;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;

            EditorUtility.SetDirty(cameraData);
        }

        private static void EnsureEnvironmentDirector(GameObject root, Light2D globalLight)
        {
            EnvironmentDirector director = root.GetComponent<EnvironmentDirector>();

            if (director == null) director = root.AddComponent<EnvironmentDirector>();

            SerializedObject serialized = new SerializedObject(director);
            SerializedProperty lightProperty = serialized.FindProperty("globalLight");

            if (lightProperty == null)
            {
                Debug.LogError("[Lighting] EnvironmentDirector 上找不到 globalLight 字段，接线未完成。");

                return;
            }

            lightProperty.objectReferenceValue = globalLight;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(director);
        }

        /// <summary>清掉旧点光池与它残留的灯具子对象；全局灯保留</summary>
        private static void RemoveLegacyPool(GameObject root)
        {
            TileLightDirector legacy = root.GetComponent<TileLightDirector>();

            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);

                Debug.Log("[Lighting] 已卸载旧 TileLightDirector（点光源池）。");
            }

            Light2D[] lights = root.GetComponentsInChildren<Light2D>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];

                if (light == null || light.lightType == Light2D.LightType.Global) continue;

                Undo.DestroyObjectImmediate(light.gameObject);
            }
        }

        private static void EnsureGridLightmap(GameObject root)
        {
            if (root.GetComponent<TileGridLightmap>() != null) return;

            TileGridLightmap lightmap = root.AddComponent<TileGridLightmap>();

            Undo.RegisterCreatedObjectUndo(lightmap, "Add TileGridLightmap");
        }

        /// <summary>建/修受光材质并返回 CelLit 供立面件接线</summary>
        private static Material EnsureMaterials()
        {
            Material celLit = EnsureMaterial(CelLitMatPath, CelLitShaderName);

            EnsureMaterial(TileFlowMatPath, TileFlowShaderName);

            // 隔离层上的影子件若还挂受光材质，会因该层零光照贡献被整块乘成黑
            if (AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath) == null)
            {
                Debug.LogError($"[Lighting] 找不到无光照材质：{UnlitMatPath}，隔离层渲染件会发黑。");
            }

            return celLit;
        }

        private static Material EnsureMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError($"[Lighting] 未找到 Shader：{shaderName}");

                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // 暗部保底是保险丝：取大值一高就把夜色三通道抬平、色相抹成中性灰，故压在 0.02
            if (material.HasProperty("_AmbientFloor")) material.SetFloat("_AmbientFloor", 0.02f);

            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>立面件池化对象取该材质；留空会退化成默认材质，夜里不受光</summary>
        private static void WireOverlayMaterial(Material celLit)
        {
            if (celLit == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(TilemapAdapterPrefabPath);

            if (contents == null)
            {
                Debug.LogError($"[Lighting] 预制体加载失败：{TilemapAdapterPrefabPath}");

                return;
            }

            try
            {
                TileOverlayDirector director = contents.GetComponentInChildren<TileOverlayDirector>(true);

                if (director == null)
                {
                    Debug.LogWarning($"[Lighting] {TilemapAdapterPrefabPath} 上找不到 TileOverlayDirector，立面件材质未接线。");

                    return;
                }

                SerializedObject serialized = new SerializedObject(director);
                SerializedProperty material = serialized.FindProperty("defaultMaterial");

                if (material == null)
                {
                    Debug.LogError("[Lighting] TileOverlayDirector 上找不到 defaultMaterial 字段。");

                    return;
                }

                material.objectReferenceValue = celLit;
                serialized.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(contents, TilemapAdapterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
