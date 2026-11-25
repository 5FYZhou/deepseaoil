using System.Collections;
using System.Reflection;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>全局光照契约测试：时段参数保底、排序层登记、受光掩码隔离</summary>
    /// <remarks>不断言 Light2D 与 Volume（EditMode 无图形设备时不可靠），场景表现由 PlayMode 目视验收。</remarks>
    [Category("Presentation")]
    public sealed class 全局光照Tests
    {
        private static readonly TimeOfDayKind[] AllKinds =
        {
            TimeOfDayKind.Day,
            TimeOfDayKind.Dusk,
            TimeOfDayKind.Night,
        };

        [Test]
        public void 时段参数_各时段强度达标且黄昏夜晚不得撞色()
        {
            for (int i = 0; i < AllKinds.Length; i++)
            {
                Assert.GreaterOrEqual(
                    TimeOfDaySetting.GetDefault(AllKinds[i]).Intensity, 0.15f, $"{AllKinds[i]} 跌破强度下限 0.15");
            }

            TimeOfDaySetting dusk = TimeOfDaySetting.GetDefault(TimeOfDayKind.Dusk);
            TimeOfDaySetting night = TimeOfDaySetting.GetDefault(TimeOfDayKind.Night);

            Color duskLight = dusk.LightColor * dusk.Intensity;
            Color nightLight = night.LightColor * night.Intensity;

            Assert.Greater(duskLight.r, duskLight.b * 1.8f, "黄昏不够暖：和夜晚会撞色");
            Assert.Greater(nightLight.b, nightLight.r * 1.8f, "夜晚不够蓝紫：和黄昏会撞色");
            Assert.Greater(Luminance(duskLight), Luminance(nightLight) * 2f, "黄昏与夜晚亮度太近：两个时段看着一样");
        }

        [Test]
        public void 时段颜色_线性值不含负数与超限值()
        {
            for (int i = 0; i < AllKinds.Length; i++)
            {
                Color c = TimeOfDaySetting.GetDefault(AllKinds[i]).LightColor;

                Assert.IsTrue(c.r >= 0f && c.r <= 1f, $"{AllKinds[i]} 的 R 越界");
                Assert.IsTrue(c.g >= 0f && c.g <= 1f, $"{AllKinds[i]} 的 G 越界");
                Assert.IsTrue(c.b >= 0f && c.b <= 1f, $"{AllKinds[i]} 的 B 越界");
            }
        }

        [Test]
        public void 排序层_五个受光与隔离层均已在TagManager登记()
        {
            string[] names =
            {
                SortingLayerConsts.Ground,
                SortingLayerConsts.Actor,
                SortingLayerConsts.Aim,
                SortingLayerConsts.Effect,
                SortingLayerConsts.UI,
            };

            for (int i = 0; i < names.Length; i++)
            {
                Assert.GreaterOrEqual(
                    SortingLayer.NameToID(names[i]),
                    0,
                    $"{names[i]} 未在 Tags and Layers 里登记；Global Light 2D 的照射层会落到 -1");
            }
        }

        [Test]
        public void 受光掩码_不包含UI特效与瞄准层()
        {
            if (SortingLayerConsts.GroundId < 0 || SortingLayerConsts.ActorId < 0) Assert.Ignore("Sorting Layer 未登记，跳过掩码判定");

            int mask = SortingLayerConsts.LitLayersMask;

            Assert.AreNotEqual(0, mask & (1 << SortingLayerConsts.GroundId), "掩码漏掉 Ground");
            Assert.AreNotEqual(0, mask & (1 << SortingLayerConsts.ActorId), "掩码漏掉 Actor");

            if (SortingLayerConsts.AimId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.AimId), "掩码错误照射 Aim");
            if (SortingLayerConsts.EffectId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.EffectId), "掩码错误照射 Effect");
            if (SortingLayerConsts.UIId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.UIId), "掩码错误照射 UI");
        }

        [Test]
        public void 光标参数_透明度缩放与自发光均已暴露()
        {
            var go = new GameObject("CursorTuningProbe", typeof(TileCursorIndicator));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<TileCursorIndicator>());

                SerializedProperty alpha = serialized.FindProperty("baseBreathMinAlpha");
                SerializedProperty scaleMin = serialized.FindProperty("baseBreathScaleMin");
                SerializedProperty scaleMax = serialized.FindProperty("baseBreathScaleMax");
                SerializedProperty validLight = serialized.FindProperty("validSelfLightColor");
                SerializedProperty invalidLight = serialized.FindProperty("invalidSelfLightColor");
                SerializedProperty lightIntensity = serialized.FindProperty("selfLightIntensity");
                SerializedProperty lightDepth = serialized.FindProperty("selfLightBreathDepth");

                Assert.IsNotNull(alpha, "最低透明度未暴露");
                Assert.IsNotNull(scaleMin, "缩放呼吸下限未暴露");
                Assert.IsNotNull(scaleMax, "缩放呼吸上限未暴露");
                Assert.IsNotNull(validLight, "有效瞄准自发光颜色未暴露");
                Assert.IsNotNull(invalidLight, "无效瞄准自发光颜色未暴露");
                Assert.IsNotNull(lightIntensity, "全局光强度未暴露");
                Assert.IsNotNull(lightDepth, "全局光呼吸深度未暴露");

                Assert.AreEqual(0.75f, alpha.floatValue, 1e-4f, "最低透明度默认值被改动");
                Assert.Less(scaleMin.floatValue, scaleMax.floatValue, "缩放呼吸上下限顺序错误");
                Assert.AreEqual(0.08f, lightIntensity.floatValue, 1e-4f, "自发光中心强度默认值被改动");
                Assert.AreEqual(0.02f, lightDepth.floatValue, 1e-4f, "自发光呼吸深度默认值被改动");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 光标呼吸_透明度有硬下限且缩放到达上下限()
        {
            TileCursorIndicator cursor = CreateCursorProbe(
                out GameObject root,
                out Transform quad,
                out SpriteRenderer renderer);

            try
            {
                SetPrivate(cursor, "baseBreathFrequency", 0.3f);
                SetPrivate(cursor, "baseBreathMinAlpha", 0.75f);
                SetPrivate(cursor, "baseBreathScaleMin", 0.96f);
                SetPrivate(cursor, "baseBreathScaleMax", 1.04f);
                SetPrivate(cursor, "validBaseColor", new Color(1f, 1f, 1f, 0.85f));

                InvokePrivate(cursor, "UpdateBaseVisuals", 0f, 0.016f);

                Assert.AreEqual(1.04f, quad.localScale.x, 1e-4f, "呼吸峰值未采用缩放上限");
                Assert.AreEqual(0.85f, ReadCursorColor(renderer).a, 1e-4f, "呼吸峰值未采用底色 alpha");

                InvokePrivate(cursor, "UpdateBaseVisuals", 1f / (2f * 0.3f), 0.016f);

                Assert.AreEqual(0.96f, quad.localScale.x, 1e-4f, "呼吸波谷未采用缩放下限");
                Assert.AreEqual(0.75f, ReadCursorColor(renderer).a, 1e-4f, "透明度跌破硬下限");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 光标跨格回弹只影响缩放不重置透明度相位()
        {
            TileCursorIndicator cursor = CreateCursorProbe(
                out GameObject root,
                out Transform quad,
                out SpriteRenderer renderer);

            try
            {
                const float now = 0.42f;

                SetPrivate(cursor, "baseBreathFrequency", 0.3f);
                SetPrivate(cursor, "baseBreathMinAlpha", 0.75f);
                SetPrivate(cursor, "baseBreathScaleMin", 0.96f);
                SetPrivate(cursor, "baseBreathScaleMax", 1.04f);
                SetPrivate(cursor, "validBaseColor", new Color(1f, 1f, 1f, 0.85f));
                SetPrivate(cursor, "punchScale", 0.92f);
                SetPrivate(cursor, "punchDuration", 0.15f);
                SetPrivate(cursor, "_targetCellCenter", new Vector3(2f, 3f, 0f));

                InvokePrivate(cursor, "UpdateBaseVisuals", now, 0.016f);

                float alphaBefore = ReadCursorColor(renderer).a;
                float scaleBefore = quad.localScale.x;

                InvokePrivate(cursor, "SnapBaseToTarget");
                InvokePrivate(cursor, "UpdateBaseVisuals", now, 0.016f);

                Assert.AreEqual(alphaBefore, ReadCursorColor(renderer).a, 1e-6f, "跨格重置了透明度呼吸相位");
                Assert.Less(quad.localScale.x, scaleBefore, "跨格回弹没有作用于缩放");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 光标自发光_随显示状态登记且空引用安全()
        {
            var cursorObject = new GameObject("CursorLightProbe", typeof(TileCursorIndicator));
            var lightmapObject = new GameObject("CursorLightmapProbe", typeof(TileGridLightmap));

            try
            {
                var cursor = cursorObject.GetComponent<TileCursorIndicator>();
                var lightmap = lightmapObject.GetComponent<TileGridLightmap>();

                Color validLight = new Color(0.9f, 0.8f, 0.7f, 1f);
                Color invalidLight = new Color(0.8f, 0.2f, 0.2f, 1f);

                SetPrivate(cursor, "validSelfLightColor", validLight);
                SetPrivate(cursor, "invalidSelfLightColor", invalidLight);
                SetPrivate(cursor, "selfLightIntensity", 0.35f);
                SetPrivate(cursor, "_hasAim", true);
                SetPrivate(cursor, "_canSnap", true);
                SetPrivate(cursor, "_targetCellCenter", Vector3.zero);

                SetPrivate(cursor, "_currentState", TileIndicatorState.ValidAim);
                Assert.DoesNotThrow(() => cursor.AppendSteadyLight(null, 0f), "空光照场必须安全退化");
                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(1, lightmap.SteadyEmitterCount, "有效瞄准未登记全局光");
                Assert.AreEqual(validLight, ReadFirstSteadyLightColor(lightmap), "有效瞄准光色错误");

                lightmap.ClearSteadyEmitters();
                SetPrivate(cursor, "_currentState", TileIndicatorState.Hidden);
                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(0, lightmap.SteadyEmitterCount, "Hidden 状态仍在登记全局光");

                SetPrivate(cursor, "_currentState", TileIndicatorState.InvalidAim);
                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(1, lightmap.SteadyEmitterCount, "无效瞄准未登记全局光");
                Assert.AreEqual(invalidLight, ReadFirstSteadyLightColor(lightmap), "无效瞄准光色错误");

                lightmap.ClearSteadyEmitters();
                SetPrivate(cursor, "_hasAim", false);
                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(0, lightmap.SteadyEmitterCount, "无瞄准仍在登记全局光");

                SetPrivate(cursor, "_hasAim", true);
                SetPrivate(cursor, "_canSnap", false);
                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(0, lightmap.SteadyEmitterCount, "未吸附格心仍在登记全局光");
            }
            finally
            {
                Object.DestroyImmediate(lightmapObject);
                Object.DestroyImmediate(cursorObject);
            }
        }

        [Test]
        public void 光标自发光_随呼吸在零点零六到零点一之间变化()
        {
            var cursorObject = new GameObject("CursorLightBreathProbe", typeof(TileCursorIndicator));
            var lightmapObject = new GameObject("CursorLightBreathmapProbe", typeof(TileGridLightmap));

            try
            {
                var cursor = cursorObject.GetComponent<TileCursorIndicator>();
                var lightmap = lightmapObject.GetComponent<TileGridLightmap>();

                SetPrivate(cursor, "baseBreathFrequency", 0.3f);
                SetPrivate(cursor, "selfLightIntensity", 0.08f);
                SetPrivate(cursor, "selfLightBreathDepth", 0.02f);
                SetPrivate(cursor, "_hasAim", true);
                SetPrivate(cursor, "_canSnap", true);
                SetPrivate(cursor, "_targetCellCenter", Vector3.zero);
                SetPrivate(cursor, "_currentState", TileIndicatorState.ValidAim);

                cursor.AppendSteadyLight(lightmap, 0f);
                Assert.AreEqual(0.1f, ReadFirstSteadyLightIntensity(lightmap), 1e-4f, "呼吸峰值未到达 0.10");

                lightmap.ClearSteadyEmitters();
                cursor.AppendSteadyLight(lightmap, 1f / (2f * 0.3f));
                Assert.AreEqual(0.06f, ReadFirstSteadyLightIntensity(lightmap), 1e-4f, "呼吸波谷未到达 0.06");
            }
            finally
            {
                Object.DestroyImmediate(lightmapObject);
                Object.DestroyImmediate(cursorObject);
            }
        }

        private static TileCursorIndicator CreateCursorProbe(
            out GameObject root,
            out Transform quad,
            out SpriteRenderer renderer)
        {
            root = new GameObject("CursorVisualProbe", typeof(TileCursorIndicator));

            var quadObject = new GameObject("BaseQuad");
            quadObject.transform.SetParent(root.transform, false);

            quad = quadObject.transform;
            renderer = quadObject.AddComponent<SpriteRenderer>();

            var cursor = root.GetComponent<TileCursorIndicator>();

            SetPrivate(cursor, "baseQuadTransform", quad);
            SetPrivate(cursor, "baseRenderer", renderer);
            SetPrivate(cursor, "_basePropBlock", new MaterialPropertyBlock());
            SetPrivate(cursor, "_colorPropId", Shader.PropertyToID("_Color"));
            SetPrivate(cursor, "_currentState", TileIndicatorState.ValidAim);
            SetPrivate(cursor, "_punchTimer", 0f);

            return cursor;
        }

        private static Color ReadCursorColor(SpriteRenderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_Color"));
        }

        private static Color ReadFirstSteadyLightColor(TileGridLightmap lightmap)
        {
            FieldInfo field = typeof(TileGridLightmap).GetField(
                "_steadyEmitters",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field);

            var entries = (IList)field.GetValue(lightmap);
            Assert.IsNotNull(entries);
            Assert.Greater(entries.Count, 0);

            object entry = entries[0];
            FieldInfo colorField = entry.GetType().GetField(
                "Color",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(colorField);
            return (Color)colorField.GetValue(entry);
        }

        private static float ReadFirstSteadyLightIntensity(TileGridLightmap lightmap)
        {
            FieldInfo field = typeof(TileGridLightmap).GetField(
                "_steadyEmitters",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field);

            var entries = (IList)field.GetValue(lightmap);
            Assert.IsNotNull(entries);
            Assert.Greater(entries.Count, 0);

            object entry = entries[0];
            FieldInfo intensityField = entry.GetType().GetField(
                "Intensity",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(intensityField);
            return (float)intensityField.GetValue(entry);
        }

        private static void SetPrivate<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"找不到字段 {name}");
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, $"找不到方法 {name}");
            return method.Invoke(target, args);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }
    }
}
