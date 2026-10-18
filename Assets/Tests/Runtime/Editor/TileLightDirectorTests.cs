using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using cfg.dso;

namespace DeepseaOil.Tests
{
    /// <summary>地块动态点光源的映射表、池契约与隔离判据</summary>
    /// <remarks>不建 Light2D、不拉场景：EditMode 无图形设备时灯的生命周期不可靠。隔离判据改读预制体的私有照射层字段。</remarks>
    [Category("Presentation")]
    public sealed class 地块点光源Tests
    {
        private const string PrefabPath = "Assets/Resources/lighting/TilePointLight.prefab";
        private const string SortingLayersField = "m_ApplyToSortingLayers";

        [Test]
        public void 映射_火与电状态各自命中对应光型()
        {
            AssertFire(TileStateType.BasicFire);
            AssertFire(TileStateType.FlameField);

            AssertElectric(TileStateType.BasicElectricity);
            AssertElectric(TileStateType.ConductZone);
            AssertElectric(TileStateType.ChargedThorn);
        }

        [Test]
        public void 映射_不发光状态一律不点亮()
        {
            TileStateType[] silent =
            {
                TileStateType.Normal,
                TileStateType.Mud,
                TileStateType.MudSkid,
                TileStateType.Steam,
                TileStateType.BasicWater,
                TileStateType.BasicEarth,
                TileStateType.Freeze,
                TileStateType.TerracottaBrick,
            };

            for (int i = 0; i < silent.Length; i++)
            {
                bool lit = TileLightDirector.TryGetContinuousLight(silent[i], out _, out _, out _, out _);

                Assert.IsFalse(lit, $"{silent[i]} 不该点持续光（蒸汽只由爆轰事件驱动瞬态白光）");
            }
        }

        [Test]
        public void 池契约_未装配时活动光为零且清空可重复调用()
        {
            var go = new GameObject("TestDirector");
            var director = go.AddComponent<TileLightDirector>();

            try
            {
                Assert.AreEqual(0, director.ActiveLightCount, "未 Attach 时不该有借出的光");

                director.ClearAll();
                director.ClearAll();

                Assert.AreEqual(0, director.ActiveLightCount, "ClearAll 必须幂等且把活动光归零");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 隔离_点光预制体的照射层恰为Ground与Actor()
        {
            int groundId = SortingLayer.NameToID(SortingLayerConsts.Ground);
            int actorId = SortingLayer.NameToID(SortingLayerConsts.Actor);

            if (groundId < 0 || actorId < 0) Assert.Ignore("Sorting Layer 未登记，跳过");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.IsNotNull(prefab, $"找不到点光源预制体：{PrefabPath}（点光池已停用；该预制体仅历史保留，缺失不影响网格光场）");

            Light2D light = prefab.GetComponent<Light2D>();

            Assert.IsNotNull(light, "点光源预制体上缺少 Light2D 组件");
            Assert.AreEqual(Light2D.LightType.Point, light.lightType, "点光源预制体的 Light Type 必须是 Point");

            var serialized = new SerializedObject(light);
            SerializedProperty layers = serialized.FindProperty(SortingLayersField);

            Assert.IsNotNull(layers, $"Light2D 上找不到 {SortingLayersField}：URP 版本可能已改字段名");
            Assert.AreEqual(2, layers.arraySize, "点光照射层必须恰好两层，多勾会漏照 Effect/UI/Aim");

            int first = layers.GetArrayElementAtIndex(0).intValue;
            int second = layers.GetArrayElementAtIndex(1).intValue;

            bool groundApplied = first == groundId || second == groundId;
            bool actorApplied = first == actorId || second == actorId;

            Assert.IsTrue(groundApplied && actorApplied, "点光照射层必须是 Ground 与 Actor");
        }

        private static void AssertFire(TileStateType state)
        {
            bool lit = TileLightDirector.TryGetContinuousLight(state, out TileLightDirector.LightKind kind, out Color color, out float radius, out float innerRadius);

            Assert.IsTrue(lit, $"{state} 应当点亮");
            Assert.AreEqual(TileLightDirector.LightKind.Fire, kind, $"{state} 的光型应为 Fire");
            Assert.Greater(radius, innerRadius, $"{state} 的外半径必须大于内半径");
            Assert.Greater(color.r, color.b, $"{state} 的暖橙红要求 R 通道高于 B 通道");
        }

        private static void AssertElectric(TileStateType state)
        {
            bool lit = TileLightDirector.TryGetContinuousLight(state, out TileLightDirector.LightKind kind, out Color color, out float radius, out float innerRadius);

            Assert.IsTrue(lit, $"{state} 应当点亮");
            Assert.AreEqual(TileLightDirector.LightKind.Electric, kind, $"{state} 的光型应为 Electric");
            Assert.Greater(radius, innerRadius, $"{state} 的外半径必须大于内半径");
            Assert.Greater(color.b, color.r, $"{state} 的青白要求 B 通道高于 R 通道");
        }
    }
}
