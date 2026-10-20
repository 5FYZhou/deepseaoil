using System.IO;
using DeepseaOil.Logic.World;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>泉眼进圈发光：增益上限、日夜分档、生命泉的静止门</summary>
    /// <remarks>发光走实例材质的 _EmissiveBoost，不触发 OnTriggerEnter2D 就没有亮暗可读，所以这里只锁规则与参数。</remarks>
    [Category("Presentation")]
    public sealed class 泉眼发光Tests
    {
        private const string WaterGlowMatPath = "Assets/Resources/materials/WaterWellGlow.mat";
        private const string EarthGlowMatPath = "Assets/Resources/materials/EarthMoundGlow.mat";
        private const string LifeGlowMatPath = "Assets/Resources/materials/LifeTotemGlow.mat";
        private const string FountainSourcePath = "Assets/Scripts/Presentation/World/Fountain.cs";

        [Test]
        public void 泉眼发光_材质只能写实例不能走属性块()
        {
            string source = File.ReadAllText(FountainSourcePath);

            StringAssert.DoesNotContain(
                "MaterialPropertyBlock", source,
                "属性块会顶掉 SpriteRenderer 逐渲染器的 _MainTex：运行时三口泉变成与 View 等大的白方块，只有改 Sorting Order 才恢复");
        }

        [Test]
        public void 泉眼增益_白天零点二黄昏夜晚零点四且均可调()
        {
            var go = new GameObject("FountainBoostProbe", typeof(Fountain));

            try
            {
                var fountain = go.GetComponent<Fountain>();
                var serialized = new SerializedObject(fountain);
                SerializedProperty day = serialized.FindProperty("emissiveBoostDay");
                SerializedProperty night = serialized.FindProperty("emissiveBoostNight");
                SerializedProperty fade = serialized.FindProperty("emissiveFadeSeconds");

                Assert.IsNotNull(day, "泉眼缺 emissiveBoostDay：白天峰值在编辑器里调不了");
                Assert.IsNotNull(night, "泉眼缺 emissiveBoostNight：黄昏夜晚峰值在编辑器里调不了");
                Assert.IsNotNull(fade, "泉眼缺 emissiveFadeSeconds：只能瞬亮瞬灭");

                Assert.AreEqual(0.2f, day.floatValue, 0.0001f, "白天进圈峰值应为 0.2");
                Assert.AreEqual(0.4f, night.floatValue, 0.0001f, "黄昏夜晚进圈峰值应为 0.4");
                Assert.Greater(fade.floatValue, 0f, "平滑时长为 0：进圈会瞬亮");

                Assert.AreEqual(0.2f, fountain.PeakEmissiveBoost(TimeOfDayKind.Day), 0.0001f, "白天取错峰值");
                Assert.AreEqual(0.4f, fountain.PeakEmissiveBoost(TimeOfDayKind.Dusk), 0.0001f, "黄昏没有并入夜间档");
                Assert.AreEqual(0.4f, fountain.PeakEmissiveBoost(TimeOfDayKind.Night), 0.0001f, "夜晚取错峰值");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 泉眼发光_生命泉只在进圈且静止时亮()
        {
            Assert.IsTrue(Fountain.GlowsInside(FountainKind.Water, true, false), "水泉进圈就该亮，不要求静止");
            Assert.IsTrue(Fountain.GlowsInside(FountainKind.Earth, true, false), "土泉进圈就该亮");
            Assert.IsFalse(Fountain.GlowsInside(FountainKind.Water, false, true), "没进圈的水泉不该亮");

            Assert.IsTrue(Fountain.GlowsInside(FountainKind.Life, true, true), "生命泉进圈且静止时应亮");
            Assert.IsFalse(Fountain.GlowsInside(FountainKind.Life, true, false), "生命泉在圈内走动时不该亮");
            Assert.IsFalse(Fountain.GlowsInside(FountainKind.Life, false, true), "生命泉离圈不该亮");
        }

        [Test]
        public void 生命泉_静止位在回血帧仍为真且走动离圈立刻清零()
        {
            var state = new LifeFountainState(1f);

            Assert.IsTrue(state.Tick(Vector2.zero, true, false, 0f, 1f), "站满 1 秒该回血");
            Assert.IsTrue(state.IsStill, "回血那一帧静止位已清零：泉眼会在回血瞬间闪一下暗");

            Assert.IsFalse(state.Tick(Vector2.zero, true, true, 0f, 0.1f), "有移动输入不该回血");
            Assert.IsFalse(state.IsStill, "有移动输入仍报静止：生命泉会在走路时保持发光");

            state.Tick(Vector2.zero, true, false, 0f, 0.1f);

            Assert.IsTrue(state.IsStill, "原地站定后没有重新报静止");

            state.Tick(Vector2.zero, false, false, 0f, 0.1f);

            Assert.IsFalse(state.IsStill, "出了九宫格仍报静止");

            state.Tick(Vector2.zero, true, false, 0f, 0.1f);
            state.Leave();

            Assert.IsFalse(state.IsStill, "Leave 没有清静止位");
        }

        [Test]
        public void 生命泉蓄力光_颜色从白走到绿且亮度零点二到零点三五()
        {
            var go = new GameObject("LifeChargeProbe", typeof(Fountain));

            try
            {
                var fountain = go.GetComponent<Fountain>();

                SetKind(fountain, FountainKind.Life);
                fountain.SetLifeCharge(0f, false);

                Assert.AreEqual(Color.white, fountain.SelfLightColor, "蓄力起点不是白");
                Assert.AreEqual(0.2f, fountain.SelfLightIntensity, 0.0001f, "蓄力起点亮度应为 0.2");

                fountain.SetLifeCharge(1f, true);

                Assert.AreEqual(0.35f, fountain.SelfLightIntensity, 0.0001f, "蓄满亮度应为 0.35");
                Assert.Greater(fountain.SelfLightColor.g, fountain.SelfLightColor.r, "蓄满应偏绿：G 必须高于 R");
                Assert.Greater(fountain.SelfLightColor.g, fountain.SelfLightColor.b, "蓄满应偏绿：G 必须高于 B");

                fountain.SetLifeCharge(0.5f, true);

                Assert.AreEqual(0.275f, fountain.SelfLightIntensity, 0.0001f, "亮度没有随蓄力线性插值");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 生命泉蓄力光_回血后由绿回白是渐变不是闪断()
        {
            var go = new GameObject("LifeFallProbe", typeof(Fountain));

            try
            {
                var fountain = go.GetComponent<Fountain>();

                SetKind(fountain, FountainKind.Life);
                fountain.SetLifeCharge(1f, true);
                fountain.Tick(0.2f, TimeOfDayKind.Day);

                Assert.AreEqual(0.35f, fountain.SelfLightIntensity, 0.0001f, "蓄满后没有到 0.35");

                fountain.SetLifeCharge(0f, false);
                fountain.Tick(0.1f, TimeOfDayKind.Day);

                Assert.Less(fountain.SelfLightIntensity, 0.35f, "回血后没有开始回落");
                Assert.Greater(fountain.SelfLightIntensity, 0.2f, "回血后一帧就跳到白：没有平滑过渡");
                Assert.Less(fountain.SelfLightColor.r, 1f, "回血后一帧颜色就跳到白：没有平滑过渡");

                for (int i = 0; i < 8; i++) fountain.Tick(0.1f, TimeOfDayKind.Day);

                Assert.AreEqual(0.2f, fountain.SelfLightIntensity, 0.0001f, "回落后没有停在 0.2");
                Assert.AreEqual(Color.white, fountain.SelfLightColor, "回落后没有回到白");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 生命泉蓄力光_不串到水泉土泉()
        {
            var go = new GameObject("WaterChargeProbe", typeof(Fountain));

            try
            {
                var fountain = go.GetComponent<Fountain>();

                SetKind(fountain, FountainKind.Water);
                fountain.SetLifeCharge(1f, true);

                Assert.AreEqual(0.55f, fountain.SelfLightIntensity, 0.0001f, "水泉的自发光强度被蓄力光覆盖了");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 泉眼材质_三份辉光材质都带可写的自发光增益()
        {
            AssertEmissiveProperty(WaterGlowMatPath);
            AssertEmissiveProperty(EarthGlowMatPath);
            AssertEmissiveProperty(LifeGlowMatPath);
        }

        private static void AssertEmissiveProperty(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Assert.IsNotNull(material, $"材质资产不存在：{path}");
            Assert.IsTrue(
                material.HasProperty("_EmissiveBoost"),
                $"{path} 没有 _EmissiveBoost：实例材质的增益写不进去，进圈不亮");
        }

        private static void SetKind(Fountain fountain, FountainKind value)
        {
            var serialized = new SerializedObject(fountain);

            serialized.FindProperty("kind").intValue = (int)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
