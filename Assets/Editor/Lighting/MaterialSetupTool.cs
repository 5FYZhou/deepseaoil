#if UNITY_EDITOR
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DeepseaOil.EditorTools.Lighting
{
    /// <summary>地表受光材质矩阵与图层迁移一键工具</summary>
    /// <remarks>图层与材质必须同批执行：灯只照 Ground/Actor，留在 Default 上的渲染件受光贡献恒为 0 会被乘成黑。所有清单均为显式白名单，不用命名规则推断。</remarks>
    public static class MaterialSetupTool
    {
        private const string MenuPath = "DeepseaOil/光照与后处理/一键装配地表受光材质矩阵";
        private const string CelLitShaderName = "DeepseaOil/2D/CelLit";
        private const string TileFlowShaderName = "DeepseaOil/2D/TileFlow";
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string TileFlowMatPath = "Assets/Resources/materials/M_TileFlow_Water.mat";
        private const string UnlitMatPath = "Assets/Resources/materials/M_SpriteFlash.mat";
        private const string TilemapAdapterPrefabPath = "Assets/Resources/scane_prebs/TilemapAdapter.prefab";
        private const string ExcludedSceneObject = "AimHighlight";

        private static readonly string[] TargetScenes =
        {
            "Assets/Scenes/Range.unity",
            "Assets/Scenes/Maza.unity",
            "Assets/Scenes/Levels/Level_1.unity",
            "Assets/Scenes/Levels/Level_2.unity",
            "Assets/Scenes/Levels/Level_3.unity",
            "Assets/Scenes/Levels/Level_4.unity",
            "Assets/Scenes/Levels/Level_5.unity",
            "Assets/Scenes/Levels/Level_Template.unity",
            "Assets/Scenes/Sandboxes/Level_1.unity",
            "Assets/Scenes/Sandboxes/Level_2.unity",
            "Assets/Scenes/Sandboxes/Slice_CombatSandbox.unity",
            "Assets/Scenes/Sandboxes/Slice_GridReaction.unity",
            "Assets/Scenes/Sandboxes/Slice_PlayerFeel.unity",
        };

        private static readonly string[] ActorPrefabs =
        {
            "Assets/Resources/scane_prebs/Player.prefab",
            "Assets/Resources/enemies/Enemy_1.prefab",
            "Assets/Resources/enemies/Enemy_2.prefab",
            "Assets/Resources/scane_prebs/Fence_Horizontal.prefab",
            "Assets/Resources/scane_prebs/Fence_Vertical.prefab",
            "Assets/Resources/scane_prebs/Tree.prefab",
            "Assets/Resources/scane_prebs/Signpost.prefab",
            "Assets/Resources/scane_prebs/Fountain/EarthMound.prefab",
            "Assets/Resources/scane_prebs/Fountain/LifeTotem.prefab",
            "Assets/Resources/scane_prebs/Fountain/WaterWell.prefab",
            "Assets/Resources/drops/Drop_Earth.prefab",
            "Assets/Resources/drops/Drop_Water.prefab",
            "Assets/Resources/drops/Drop_Seed_Elec.prefab",
            "Assets/Resources/drops/Drop_Seed_Fire.prefab",
            "Assets/Resources/drops/Drop_Seed_Ice.prefab",
            "Assets/Resources/drops/Drop_Seed_Plant.prefab",
        };

        private static readonly string[] EffectPrefabs =
        {
            "Assets/Resources/effects/BurstSparks.prefab",
            "Assets/Resources/effects/ChainShock.prefab",
            "Assets/Resources/effects/Dust.prefab",
            "Assets/Resources/effects/FlameEmbers.prefab",
            "Assets/Resources/effects/MudSplash.prefab",
            "Assets/Resources/effects/SteamBlast.prefab",
        };

        // 伤害飘字没有 SpriteRenderer，只有 TMP 的 MeshRenderer，故单列一份
        private static readonly string[] TextPrefabs =
        {
            "Assets/Resources/effects/DamagePopup.prefab",
        };

        // 隔离层上挂受光材质会整块被乘成黑；这两件是实测发黑的，其余特效材质虽开 Lights 开关但不受影响
        private static readonly string[] UnlitSpritePrefabs =
        {
            "Assets/Resources/effects/BurstSparks.prefab",
            "Assets/Resources/effects/MudSplash.prefab",
        };

        [MenuItem(MenuPath)]
        public static void SetupGroundMaterials()
        {
            if (!ValidateSortingLayers()) return;

            // 逐场景 Single 打开会顶掉当前场景：先拿到用户已保存的确认，取消则整体中止
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Material celLit = EnsureMaterial(CelLitMatPath, CelLitShaderName);
            Material tileFlow = EnsureMaterial(TileFlowMatPath, TileFlowShaderName);

            if (celLit == null || tileFlow == null) return;

            string activeScene = SceneManager.GetActiveScene().path;

            int tilemaps = 0;
            int sprites = 0;
            int failed = 0;

            for (int i = 0; i < TargetScenes.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenes[i]) == null)
                {
                    failed++;
                    Debug.LogError($"[Material] 场景不存在，已跳过：{TargetScenes[i]}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(TargetScenes[i], OpenSceneMode.Single);

                int sceneTilemaps = MigrateSceneTilemaps(scene, celLit, tileFlow, out int sceneSprites);

                tilemaps += sceneTilemaps;
                sprites += sceneSprites;

                if (sceneTilemaps > 0 || sceneSprites > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);

                    if (EditorSceneManager.SaveScene(scene))
                    {
                        ReportSceneChange(scene.name, sceneTilemaps, sceneSprites);
                    }
                    else
                    {
                        failed++;
                        Debug.LogError($"[Material] 场景保存失败，迁移未落盘：{scene.path}");
                    }
                }
            }

            int actorNodes = MigrateSprites(ActorPrefabs, SortingLayerConsts.Actor);
            int effectNodes = MigrateEffectRenderers(EffectPrefabs, SortingLayerConsts.Effect);
            int textNodes = MigrateRenderers(TextPrefabs, SortingLayerConsts.Effect);
            int unlitNodes = ApplyUnlitToSprites(UnlitSpritePrefabs, EnsureUnlitMaterial());

            WireOverlayMaterial(celLit);

            if (!string.IsNullOrEmpty(activeScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScene) != null)
            {
                EditorSceneManager.OpenScene(activeScene, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Material] 受光材质与图层迁移完成：Tilemap {tilemaps} 个、场景 Sprite {sprites} 个、" +
                $"世界预制体渲染器 {actorNodes} 个 → Actor、特效预制体渲染器 {effectNodes} 个 → Effect、" +
                $"文字渲染器 {textNodes} 个 → Effect、隔离层改无光照材质 {unlitNodes} 个，失败场景 {failed} 个。");
        }

        private static Material EnsureUnlitMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);

            if (material != null) return material;

            Debug.LogError($"[Material] 找不到无光照材质：{UnlitMatPath}，隔离层影子渲染件会发黑。");

            return null;
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

            Debug.LogError($"[Material] 未登记的 Sorting Layer：{missing}。请先在 Tags and Layers 补齐后重跑本菜单。");

            return false;
        }

        private static Material EnsureMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError($"[Material] 未找到 Shader：{shaderName}");
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

            if (material.HasProperty("_AmbientFloor")) material.SetFloat("_AmbientFloor", 0.35f);
            if (material.HasProperty("_CelBands")) material.SetFloat("_CelBands", 3f);

            EditorUtility.SetDirty(material);

            return material;
        }

        /// <returns>迁移的 Tilemap 数量</returns>
        private static int MigrateSceneTilemaps(Scene scene, Material celLit, Material tileFlow, out int migratedSprites)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int tilemaps = 0;
            migratedSprites = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                TilemapRenderer[] renderers = roots[i].GetComponentsInChildren<TilemapRenderer>(true);

                for (int j = 0; j < renderers.Length; j++)
                {
                    TilemapRenderer renderer = renderers[j];

                    // 排序号不动：Y-Sort 遮挡关系由同一层内的 order 决定
                    renderer.sortingLayerName = SortingLayerConsts.Ground;
                    renderer.sharedMaterial = renderer.gameObject.name.Contains("Water") ? tileFlow : celLit;

                    EditorUtility.SetDirty(renderer);
                    tilemaps++;
                }

                SpriteRenderer[] sprites = roots[i].GetComponentsInChildren<SpriteRenderer>(true);

                for (int j = 0; j < sprites.Length; j++)
                {
                    if (ShouldSkipSprite(sprites[j])) continue;

                    sprites[j].sortingLayerName = SortingLayerConsts.Actor;
                    EditorUtility.SetDirty(sprites[j]);
                    migratedSprites++;
                }
            }

            return tilemaps;
        }

        /// <summary>只迁移仍留在 Default 的世界件：UI 与显式排除项一概不动</summary>
        private static bool ShouldSkipSprite(SpriteRenderer sprite)
        {
            if (sprite.sortingLayerID != 0) return true;

            Transform current = sprite.transform;

            while (current != null)
            {
                if (current.name == ExcludedSceneObject) return true;
                if (current.GetComponent<Canvas>() != null) return true;

                current = current.parent;
            }

            return false;
        }

        private static int MigrateSprites(string[] paths, string layerName)
        {
            int migrated = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);

                if (contents == null)
                {
                    Debug.LogError($"[Material] 预制体加载失败，已跳过：{paths[i]}");
                    continue;
                }

                try
                {
                    SpriteRenderer[] sprites = contents.GetComponentsInChildren<SpriteRenderer>(true);

                    for (int j = 0; j < sprites.Length; j++)
                    {
                        sprites[j].sortingLayerName = layerName;
                        migrated++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, paths[i]);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return migrated;
        }

        /// <summary>特效件必须同时迁 SpriteRenderer 与 ParticleSystemRenderer：粒子渲染器的层不在精灵字段上</summary>
        private static int MigrateEffectRenderers(string[] paths, string layerName)
        {
            int migrated = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);

                if (contents == null)
                {
                    Debug.LogError($"[Material] 预制体加载失败，已跳过：{paths[i]}");
                    continue;
                }

                try
                {
                    SpriteRenderer[] sprites = contents.GetComponentsInChildren<SpriteRenderer>(true);

                    for (int j = 0; j < sprites.Length; j++)
                    {
                        sprites[j].sortingLayerName = layerName;
                        migrated++;
                    }

                    ParticleSystemRenderer[] particles = contents.GetComponentsInChildren<ParticleSystemRenderer>(true);

                    for (int j = 0; j < particles.Length; j++)
                    {
                        particles[j].sortingLayerName = layerName;
                        migrated++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, paths[i]);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return migrated;
        }

        private static int MigrateRenderers(string[] paths, string layerName)
        {
            int migrated = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);

                if (contents == null)
                {
                    Debug.LogError($"[Material] 预制体加载失败，已跳过：{paths[i]}");
                    continue;
                }

                try
                {
                    MeshRenderer[] meshes = contents.GetComponentsInChildren<MeshRenderer>(true);

                    for (int j = 0; j < meshes.Length; j++)
                    {
                        meshes[j].sortingLayerName = layerName;
                        migrated++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, paths[i]);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return migrated;
        }

        /// <summary>隔离层上的影子渲染件若挂受光材质，会因该层零光照贡献被整块乘成黑</summary>
        private static int ApplyUnlitToSprites(string[] paths, Material unlit)
        {
            int migrated = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);

                if (contents == null) continue;

                try
                {
                    SpriteRenderer[] sprites = contents.GetComponentsInChildren<SpriteRenderer>(true);

                    // 逐预制体计数：用累计值判"本件有没有改"会让第二件起永远跳过保存
                    int changed = 0;

                    for (int j = 0; j < sprites.Length; j++)
                    {
                        if (sprites[j].sharedMaterial == unlit) continue;

                        sprites[j].sharedMaterial = unlit;
                        changed++;
                    }

                    if (changed == 0) continue;

                    migrated += changed;

                    PrefabUtility.SaveAsPrefabAsset(contents, paths[i]);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return migrated;
        }

        /// <summary>立面件池化对象取该材质；留空会退化成默认材质，夜里不受光</summary>
        private static void WireOverlayMaterial(Material celLit)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(TilemapAdapterPrefabPath);

            if (contents == null)
            {
                Debug.LogError($"[Material] 预制体加载失败：{TilemapAdapterPrefabPath}");
                return;
            }

            try
            {
                TileOverlayDirector director = contents.GetComponentInChildren<TileOverlayDirector>(true);

                if (director == null)
                {
                    Debug.LogWarning($"[Material] {TilemapAdapterPrefabPath} 上找不到 TileOverlayDirector，立面件材质未接线。");
                    return;
                }

                SerializedObject serialized = new SerializedObject(director);
                SerializedProperty material = serialized.FindProperty("defaultMaterial");

                if (material == null)
                {
                    Debug.LogError("[Material] TileOverlayDirector 上找不到 defaultMaterial 字段。");
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

        private static void ReportSceneChange(string sceneName, int tilemaps, int sprites)
        {
            Debug.Log($"[Material] {sceneName}：Tilemap {tilemaps} 个 → Ground，Sprite {sprites} 个 → Actor");
        }
    }
}
#endif
