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
    /// <remarks>图层顺序与分配错了都不报错、只表现为整片被盖掉或乘成黑。场景断言用 Additive 打开、断言完立刻关闭，且只在场景打开期间读值。</remarks>
    [Category("Presentation")]
    public sealed class 受光材质Tests
    {
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string TileFlowMatPath = "Assets/Resources/materials/M_TileFlow_Water.mat";
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
        public void 材质_受光材质的暗部保底不得高于0点20()
        {
            AssertAmbientFloor(CelLitMatPath);
            AssertAmbientFloor(TileFlowMatPath);
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

        private static void AssertAmbientFloor(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Assert.IsNotNull(material, $"材质资产不存在：{path}（先跑 DeepseaOil ▸ 光照与后处理 ▸ 一键装配地表受光材质矩阵）");
            Assert.IsTrue(material.HasProperty("_AmbientFloor"), $"{path} 缺 _AmbientFloor");

            // 保底低于夜色通道才不夺色相：高一点就把夜色三通道抬平、抹成中性灰
            Assert.LessOrEqual(material.GetFloat("_AmbientFloor"), 0.05f, $"{path} 的暗部保底高于 0.05：夜里色相会被压成中性灰");
        }

        /// <summary>Additive 打开 Range、在场景加载期间取值断言、随即关闭：对象在关闭后即失效，不能带出去</summary>
        private static bool VisitRangeScene<T>(Action<List<T>> assert) where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RangeScenePath) == null) return false;

            Scene scene = EditorSceneManager.OpenScene(RangeScenePath, OpenSceneMode.Additive);

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
                EditorSceneManager.CloseScene(scene, true);
            }

            return true;
        }
    }
}
