#if UNITY_EDITOR
using System.Collections.Generic;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.EditorTools.Lighting
{
    /// <summary>全局光照与后处理环境一键装配工具</summary>
    /// <remarks>只读校验 Sorting Layer，不写 TagManager；Light2D 的照射层没有公共属性，只能经 m_ApplyToSortingLayers 序列化字段写入。</remarks>
    public static class EnvironmentSetupTool
    {
        private const string MenuPath = "DeepseaOil/光照与后处理/一键装配当前场景环境基座";
        private const string ProfileAssetPath = "Assets/Settings/DefaultGlobalVolumeProfile.asset";
        private const string SortingLayersField = "m_ApplyToSortingLayers";

        [MenuItem(MenuPath)]
        public static void SetupSceneEnvironment()
        {
            if (!ValidateSortingLayers()) return;

            GameObject root = SetupEnvironmentRoot();
            Light2D globalLight = SetupGlobalLight(root);

            if (globalLight == null) return;

            SetupGlobalVolume(root);
            SetupEnvironmentDirector(root, globalLight);
            SetupCameraPostProcessing();

            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();

            Debug.Log("[Lighting] 环境基座装配完成：Tonemapping 已剔除，Color Adjustments 已注入，Global Light 2D、Global Volume 与 EnvironmentDirector 已就位。");
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

        private static GameObject SetupEnvironmentRoot()
        {
            GameObject root = GameObject.Find("[Environment]");

            if (root != null) return root;

            root = new GameObject("[Environment]");
            Undo.RegisterCreatedObjectUndo(root, "Create [Environment] Root");

            return root;
        }

        private static Light2D SetupGlobalLight(GameObject root)
        {
            Transform existing = root.transform.Find("Global Light 2D");

            GameObject lightGo;

            if (existing != null)
            {
                lightGo = existing.gameObject;
            }
            else
            {
                lightGo = new GameObject("Global Light 2D");
                lightGo.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Global Light 2D");
            }

            Light2D light2d = lightGo.GetComponent<Light2D>();

            if (light2d == null) light2d = lightGo.AddComponent<Light2D>();

            light2d.lightType = Light2D.LightType.Global;

            TimeOfDaySetting day = TimeOfDaySetting.GetDefault(TimeOfDayKind.Day);
            light2d.color = day.LightColor;
            light2d.intensity = day.Intensity;

            if (!ApplySortingLayers(light2d, SortingLayerConsts.Ground, SortingLayerConsts.Actor)) return null;

            EditorUtility.SetDirty(light2d);

            return light2d;
        }

        /// <summary>向 Light2D 的私有序列化字段写照射层；落到失效 ID 会让光照静默全层生效</summary>
        private static bool ApplySortingLayers(Light2D light2d, string first, string second)
        {
            int firstId = SortingLayer.NameToID(first);
            int secondId = SortingLayer.NameToID(second);

            if (firstId < 0 || secondId < 0)
            {
                Debug.LogError($"[Lighting] {first} / {second} 的 Sorting Layer ID 无效，光照层未写入。");

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

        private static void SetupGlobalVolume(GameObject root)
        {
            Transform existing = root.transform.Find("Global Volume");

            GameObject volumeGo;

            if (existing != null)
            {
                volumeGo = existing.gameObject;
            }
            else
            {
                volumeGo = new GameObject("Global Volume");
                volumeGo.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(volumeGo, "Create Global Volume");
            }

            // 相机 Volume Mask 只认 Default 层，放进别的层后处理会完全不生效
            volumeGo.layer = 0;

            Volume volume = volumeGo.GetComponent<Volume>();

            if (volume == null) volume = volumeGo.AddComponent<Volume>();

            volume.isGlobal = true;

            VolumeProfile profile = LoadOrRebuildProfile();

            ApplySealedOverrides(profile);

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

        private static void ApplySealedOverrides(VolumeProfile profile)
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
        private static void SetupCameraPostProcessing()
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

        private static void SetupEnvironmentDirector(GameObject root, Light2D globalLight)
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
    }
}
#endif
