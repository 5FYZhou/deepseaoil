#if UNITY_EDITOR
using System.IO;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepseaOil.EditorTools.Effects
{
    /// <summary>电格复合特效的一键烘焙：常驻萤火虫 + 偶发微型电弧，单预制体双子节点与 TilemapAdapter 接线</summary>
    /// <remarks>预制体在原地覆盖（guid 不变），重复执行幂等。</remarks>
    public static class TileElectricityVfxTool
    {
        private const string MenuPath = "DeepseaOil/特效/一键构建萤火虫与偶发电弧复合特效";
        private const string EffectsDir = "Assets/Resources/effects";
        private const string PrefabPath = EffectsDir + "/Vfx_Tile_Electricity.prefab";
        private const string GlowTexturePath = EffectsDir + "/Tex_Fireflies_Glow.png";
        private const string FirefliesMaterialPath = "Assets/Resources/materials/M_Fireflies_Particle.mat";
        private const string LightningMaterialPath = "Assets/Resources/materials/M_ProceduralLightning.mat";
        private const string AdapterPrefabPath = "Assets/Resources/scane_prebs/TilemapAdapter.prefab";
        private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        private const string LightningShader = "DeepseaOil/Particles/ProceduralLightning";

        private const int TextureSize = 32;

        private const float FireflyRate = 3.5f;
        private const int FireflyMaxParticles = 16;

        // 偶发是核心手感：0.9 次/秒 × 0.06~0.09s 寿命 = 平均 1.1 秒才闪一下，绝不连续喧宾夺主
        private const float ArcRate = 0.9f;
        private const int ArcMaxParticles = 4;
        private const float ArcSizeX = 0.14f;

        [MenuItem(MenuPath)]
        public static void BakeAll()
        {
            EnsureEffectsDirectory();

            Texture2D glow = EnsureGlowTexture();
            Material firefliesMaterial = EnsureFirefliesMaterial(glow);
            Material lightningMaterial = EnsureLightningMaterial();

            if (firefliesMaterial == null || lightningMaterial == null) return;

            GameObject prefab = BuildComposite(firefliesMaterial, lightningMaterial);

            if (prefab == null) return;

            WireAdapter(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;

            Debug.Log($"[Vfx] 电格复合特效已烘焙：{PrefabPath}（萤火虫 {FirefliesMaterialPath}，电弧 {LightningMaterialPath}）。");
        }

        private static void EnsureEffectsDirectory()
        {
            if (Directory.Exists(EffectsDir)) return;

            Directory.CreateDirectory(EffectsDir);

            AssetDatabase.Refresh();
        }

        private static Texture2D EnsureGlowTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];

            float half = (TextureSize - 1) * 0.5f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(1f - distance);

                    alpha *= alpha;

                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(GlowTexturePath, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GlowTexturePath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(GlowTexturePath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"[Vfx] 拿不到贴图导入器（{GlowTexturePath}）：萤火虫会退化成白方块。");

                return null;
            }

            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);
        }

        private static Material EnsureFirefliesMaterial(Texture2D glow)
        {
            if (glow == null) return null;

            Shader shader = Shader.Find(ParticleShader);

            if (shader == null)
            {
                Debug.LogError($"[Vfx] 找不到着色器 {ParticleShader}：萤火虫材质无法生成。");

                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(FirefliesMaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, FirefliesMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            // 新建的 URP 粒子材质是 Opaque（不透明黑块），透明态的开关必须手配
            material.SetTexture("_BaseMap", glow);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(material);

            return material;
        }

        private static Material EnsureLightningMaterial()
        {
            Shader shader = Shader.Find(LightningShader);

            if (shader == null)
            {
                Debug.LogError($"[Vfx] 找不到着色器 {LightningShader}：偶发电弧无法渲染。");

                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(LightningMaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, LightningMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_GlowColor", new Color(0.15f, 0.88f, 1f, 1f));
            material.SetFloat("_CoreWidth", 0.025f);
            material.SetFloat("_GlowWidth", 0.055f);
            material.SetFloat("_Jaggedness", 0.32f);
            material.SetFloat("_FlickerSpeed", 24f);

            EditorUtility.SetDirty(material);

            return material;
        }

        private static GameObject BuildComposite(Material firefliesMaterial, Material lightningMaterial)
        {
            var root = new GameObject("Vfx_Tile_Electricity");

            BuildFireflies(root.transform, firefliesMaterial);
            BuildArcSparks(root.transform, lightningMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Object.DestroyImmediate(root);

            if (prefab == null) Debug.LogError($"[Vfx] 预制体保存失败：{PrefabPath}");

            return prefab;
        }

        private static void BuildFireflies(Transform parent, Material material)
        {
            ParticleSystem ps = AddParticleChild(parent, "Fireflies");

            ParticleSystem.MainModule main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            main.startSpeed = 0.04f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.16f);
            main.startColor = new Color(0.490f, 0.976f, 1f, 1f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = FireflyMaxParticles;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = FireflyRate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.32f;
            shape.radiusThickness = 0.2f;
            shape.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(2.6f, 3.4f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.12f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.490f, 0.976f, 1f), 0f),
                    new GradientColorKey(new Color(0.976f, 0.808f, 0.353f), 0.5f),
                    new GradientColorKey(new Color(0.490f, 0.976f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                });

            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            var bell = new AnimationCurve();
            bell.AddKey(0f, 0.2f);
            bell.AddKey(0.5f, 1f);
            bell.AddKey(1f, 0.2f);

            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, bell);

            ApplyRenderer(ps, material, RenderOrder.TileStateVfx);
        }

        private static void BuildArcSparks(Transform parent, Material material)
        {
            ParticleSystem ps = AddParticleChild(parent, "ArcSparks");

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1.5f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            // 极短寿命是"清脆一闪"的全部来源：一旦被调长就退化成拖着尾巴的长电弧
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = ArcSizeX;
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.28f, 0.38f);
            main.startSizeZ = ArcSizeX;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // 着色器拿 input.color.a 当形状种子：两档 alpha 让每次闪出的折角都不同形
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), Color.white);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = ArcMaxParticles;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = ArcRate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;

            ApplyRenderer(ps, material, RenderOrder.TileStateVfx + 1);
        }

        private static ParticleSystem AddParticleChild(Transform parent, string name)
        {
            var child = new GameObject(name);

            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;

            return child.AddComponent<ParticleSystem>();
        }

        private static void ApplyRenderer(ParticleSystem ps, Material material, int sortingOrder)
        {
            var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = material;
            particleRenderer.sortingLayerName = SortingLayerConsts.Actor;
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
        }

        private static void WireAdapter(GameObject vfxPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AdapterPrefabPath) == null)
            {
                WireSceneAdapter(vfxPrefab);

                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(AdapterPrefabPath);

            try
            {
                TilemapAdapter adapter = contents.GetComponent<TilemapAdapter>();

                if (adapter == null)
                {
                    Debug.LogError($"[Vfx] {AdapterPrefabPath} 上没有 TilemapAdapter：接线跳过。");

                    return;
                }

                TileVfxDirector director = contents.GetComponent<TileVfxDirector>();

                if (director == null) director = contents.AddComponent<TileVfxDirector>();

                var serialized = new SerializedObject(director);

                SerializedProperty prefabProperty = serialized.FindProperty("electricityVfxPrefab");
                SerializedProperty poolProperty = serialized.FindProperty("maxPoolSize");

                if (prefabProperty == null || poolProperty == null)
                {
                    Debug.LogError("[Vfx] TileVfxDirector 的序列化字段改名了：接线失败。");

                    return;
                }

                prefabProperty.objectReferenceValue = vfxPrefab;
                poolProperty.intValue = 32;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, AdapterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void WireSceneAdapter(GameObject vfxPrefab)
        {
            TilemapAdapter adapter = Object.FindObjectOfType<TilemapAdapter>();

            if (adapter == null)
            {
                Debug.LogWarning("[Vfx] 场景里没有 TilemapAdapter，也找不到适配器预制体：只烘焙了预制体，未接线。");

                return;
            }

            TileVfxDirector director = adapter.GetComponent<TileVfxDirector>();

            if (director == null) director = adapter.gameObject.AddComponent<TileVfxDirector>();

            var serialized = new SerializedObject(director);
            SerializedProperty prefabProperty = serialized.FindProperty("electricityVfxPrefab");

            if (prefabProperty == null)
            {
                Debug.LogError("[Vfx] TileVfxDirector 的序列化字段改名了：接线失败。");

                return;
            }

            prefabProperty.objectReferenceValue = vfxPrefab;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(director);
        }
    }
}
#endif
