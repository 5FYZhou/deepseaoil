using System;
using System.Collections.Generic;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DeepseaOil.Tests
{
    /// <summary>受光材质矩阵与图层隔离的机器判据</summary>
    /// <remarks>图层顺序与分配错了都不报错、只表现为整片被盖掉或乘成黑。场景断言优先复用已打开的 Range，否则 Additive 打开、断言完立刻关闭，且只在场景打开期间读值。</remarks>
    [Category("Presentation")]
    public sealed class 受光材质Tests
    {
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string TileFlowMatPath = "Assets/Resources/materials/M_TileFlow_Water.mat";
        private const string WaterGlowMatPath = "Assets/Resources/materials/WaterWellGlow.mat";
        private const string EarthGlowMatPath = "Assets/Resources/materials/EarthMoundGlow.mat";
        private const string LifeGlowMatPath = "Assets/Resources/materials/LifeTotemGlow.mat";
        private const string WaterFountainPrefabPath = "Assets/Resources/scane_prebs/Fountain/WaterWell.prefab";
        private const string EarthFountainPrefabPath = "Assets/Resources/scane_prebs/Fountain/EarthMound.prefab";
        private const string LifeFountainPrefabPath = "Assets/Resources/scane_prebs/Fountain/LifeTotem.prefab";
        private const string WaterTexturePath = "Assets/Resources/art/Prop/prop_machine_penqvan.png";
        private const string EarthTexturePath = "Assets/Resources/art/Prop/prop_machine_tiqvqi.png";
        private const string LifeTexturePath = "Assets/Resources/art/Prop/prop_machine_chonggouyi.png";
        private const string CelLitShaderName = "DeepseaOil/2D/CelLit";
        private const string RangeScenePath = "Assets/Scenes/Range.unity";
        private const string AffectedLayersField = "m_ApplyToSortingLayers";
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        [Test]
        public void 着色器_受光两枚都能被引擎定位()
        {
            Assert.IsNotNull(Shader.Find("DeepseaOil/2D/CelLit"), "未找到 DeepseaOil/2D/CelLit");
            Assert.IsNotNull(Shader.Find("DeepseaOil/2D/TileFlow"), "未找到 DeepseaOil/2D/TileFlow");
        }

        [Test]
        public void 着色器_CelLit无编译错误()
        {
            Shader shader = Shader.Find(CelLitShaderName);

            Assert.IsNotNull(shader, $"未找到 {CelLitShaderName}");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{CelLitShaderName} 有编译错误，喷泉材质会退回粉红");
        }

        [Test]
        public void 材质_受光材质的暗部保底不得高于0点20()
        {
            AssertAmbientFloor(CelLitMatPath);
            AssertAmbientFloor(TileFlowMatPath);
        }

        [Test]
        public void 材质_喷泉辉光材质走CelLit且参数有效()
        {
            AssertFountainGlowMaterial(WaterGlowMatPath, "WaterWellGlow");
            AssertFountainGlowMaterial(EarthGlowMatPath, "EarthMoundGlow");
            AssertFountainGlowMaterial(LifeGlowMatPath, "LifeTotemGlow");
        }

        [Test]
        public void 材质_默认受光材质关闭喷泉辉光()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CelLitMatPath);

            Assert.IsNotNull(material, $"材质资产不存在：{CelLitMatPath}");
            Assert.AreEqual(0f, material.GetFloat("_GlowIntensity"), 0.0001f, "默认 CelLit 材质不得发光，否则所有精灵都会亮边");
        }

        [Test]
        public void 预制体_三类泉水的View绑定对应辉光材质()
        {
            AssertFountainViewMaterial(WaterFountainPrefabPath, "WaterWellGlow");
            AssertFountainViewMaterial(EarthFountainPrefabPath, "EarthMoundGlow");
            AssertFountainViewMaterial(LifeFountainPrefabPath, "LifeTotemGlow");
        }

        [Test]
        public void 贴图_三类泉水精灵网格为FullRect()
        {
            AssertFullRectMesh(WaterTexturePath);
            AssertFullRectMesh(EarthTexturePath);
            AssertFullRectMesh(LifeTexturePath);
        }

        [Test]
        public void 图层顺序_自定义层按Ground到UI由前到后排列()
        {
            List<string> order = ReadSortingLayerOrder();

            if (order.Count < 5) Assert.Ignore("Sorting Layer 未补齐，跳过顺序判定");

            // 列表索引越小越晚画：Ground 在最前，UI 在其后，Default 落表尾
            AssertLayerBefore(order, SortingLayerConsts.Ground, SortingLayerConsts.Actor);
            AssertLayerBefore(order, SortingLayerConsts.Actor, SortingLayerConsts.Aim);
            AssertLayerBefore(order, SortingLayerConsts.Aim, SortingLayerConsts.Effect);
            AssertLayerBefore(order, SortingLayerConsts.Effect, SortingLayerConsts.UI);
        }

        [Test]
        public void 场景_Range的Tilemap全部落在Ground受光层()
        {
            if (SortingLayerConsts.GroundId < 0) Assert.Ignore("Ground 未登记，跳过");

            bool visited = VisitRangeScene<TilemapRenderer>(tilemaps =>
            {
                Assert.GreaterOrEqual(tilemaps.Count, 6, "Range 应至少 6 层 Tilemap；数量变少说明有层被删或合并");

                for (int i = 0; i < tilemaps.Count; i++)
                {
                    Assert.AreEqual(
                        SortingLayerConsts.Ground,
                        tilemaps[i].sortingLayerName,
                        $"{tilemaps[i].gameObject.name} 不在 Ground 层：留在 Default 会被灯乘成黑");
                }
            });

            if (!visited) Assert.Ignore("Range 未能打开，跳过");
        }

        [Test]
        public void 场景_Range的全局灯只照Ground与Actor()
        {
            int groundId = SortingLayerConsts.GroundId;
            int actorId = SortingLayerConsts.ActorId;

            if (groundId < 0 || actorId < 0) Assert.Ignore("Sorting Layer 未登记，跳过");

            bool visited = VisitRangeScene<Light2D>(lights =>
            {
                Assert.Greater(lights.Count, 0, "Range 里没有 Light2D，环境基座未装配");

                for (int i = 0; i < lights.Count; i++)
                {
                    SerializedObject serialized = new SerializedObject(lights[i]);
                    SerializedProperty layers = serialized.FindProperty(AffectedLayersField);

                    Assert.IsNotNull(layers, $"Light2D 上找不到 {AffectedLayersField}：URP 版本可能已改字段名");
                    Assert.AreEqual(2, layers.arraySize, "灯的照射层必须是 Ground 与 Actor 两层");

                    List<int> applied = new List<int>(2);

                    for (int j = 0; j < layers.arraySize; j++)
                    {
                        applied.Add(layers.GetArrayElementAtIndex(j).intValue);
                    }

                    CollectionAssert.AreEquivalent(
                        new[] { groundId, actorId },
                        applied,
                        "灯的照射层必须恰为 Ground 与 Actor：多勾 Effect/UI/Aim 会让特效与界面被压暗");
                }
            });

            if (!visited) Assert.Ignore("Range 未能打开，跳过");
        }

        /// <summary>列表索引越小越晚画：front 必须排在 back 之前，否则 front 会被 back 整片盖掉</summary>
        private static void AssertLayerBefore(List<string> order, string front, string back)
        {
            Assert.Less(
                order.IndexOf(front),
                order.IndexOf(back),
                $"{front} 必须排在 {back} 之前（索引越小越晚画）；否则 {front} 会被 {back} 整片盖掉");
        }

        /// <summary>直接读 TagManager 的 m_SortingLayers：运行时 SortingLayer.layers 的索引与渲染顺序无关</summary>
        private static List<string> ReadSortingLayerOrder()
        {
            var order = new List<string>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);

            if (assets == null || assets.Length == 0) return order;

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");

            if (layers == null) return order;

            for (int i = 0; i < layers.arraySize; i++)
            {
                order.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            }

            return order;
        }

        private static void AssertFountainGlowMaterial(string path, string expectedName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Assert.IsNotNull(material, $"材质资产不存在：{path}");
            Assert.AreEqual(expectedName, material.name, $"{path} 的材质名不正确");
            Assert.AreEqual(CelLitShaderName, material.shader.name, $"{path} 未走 CelLit，喷泉将丢失 2D 光照");
            Assert.IsTrue(material.HasProperty("_GlowIntensity"), $"{path} 缺 _GlowIntensity");
            Assert.Greater(material.GetFloat("_GlowIntensity"), 0f, $"{path} 的喷泉辉光未开启");
            Assert.GreaterOrEqual(material.GetFloat("_GlowWidth"), 0f, $"{path} 的 _GlowWidth 不得为负");
            Assert.LessOrEqual(material.GetFloat("_GlowWidth"), 0.1f, $"{path} 的 _GlowWidth 超出量纲");
            Assert.Greater(material.GetColor("_GlowColor").a, 0f, $"{path} 的 _GlowColor 全透明，发光不可见");
        }

        private static void AssertFountainViewMaterial(string prefabPath, string expectedMaterialName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(prefab, $"预制体不存在：{prefabPath}");

            Transform view = prefab.transform.Find("View");
            Assert.IsNotNull(view, $"{prefabPath} 找不到 View 子物体");

            SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, $"{prefabPath}/View 缺 SpriteRenderer");
            Assert.IsNotNull(renderer.sharedMaterial, $"{prefabPath}/View 没有材质");

            Assert.AreEqual(expectedMaterialName, renderer.sharedMaterial.name, $"{prefabPath}/View 绑错材质");
            Assert.AreEqual(CelLitShaderName, renderer.sharedMaterial.shader.name, $"{prefabPath}/View 材质不是 CelLit");
        }

        private static void AssertFullRectMesh(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;

            Assert.IsNotNull(importer, $"找不到贴图导入器：{texturePath}");

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            Assert.AreEqual(
                SpriteMeshType.FullRect,
                settings.spriteMeshType,
                $"{texturePath} 仍是 Tight 网格，轮廓外没有像素可画辉光");
        }

        private static void AssertAmbientFloor(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Assert.IsNotNull(material, $"材质资产不存在：{path}（先跑 DeepseaOil ▸ 光照 ▸ 装配当前场景）");
            Assert.IsTrue(material.HasProperty("_AmbientFloor"), $"{path} 缺 _AmbientFloor");

            // 保底低于夜色通道才不夺色相：高一点就把夜色三通道抬平、抹成中性灰
            Assert.LessOrEqual(material.GetFloat("_AmbientFloor"), 0.05f, $"{path} 的暗部保底高于 0.05：夜里色相会被压成中性灰");
        }

        /// <summary>Additive 打开 Range、在场景加载期间取值断言、随即关闭：对象在关闭后即失效，不能带出去</summary>
        private static bool VisitRangeScene<T>(Action<List<T>> assert) where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RangeScenePath) == null) return false;

            // 编辑器里已经开着 Range 时不能再开一份：两份环境预制体各带一盏全局灯，URP 会报 More than one global light
            Scene scene = SceneManager.GetSceneByPath(RangeScenePath);
            bool owned = !(scene.IsValid() && scene.isLoaded);

            if (owned) scene = EditorSceneManager.OpenScene(RangeScenePath, OpenSceneMode.Additive);

            if (!scene.IsValid() || !scene.isLoaded) return false;

            try
            {
                var found = new List<T>();
                GameObject[] roots = scene.GetRootGameObjects();

                for (int i = 0; i < roots.Length; i++)
                {
                    found.AddRange(roots[i].GetComponentsInChildren<T>(true));
                }

                assert(found);
            }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
            }

            return true;
        }
    }
}
