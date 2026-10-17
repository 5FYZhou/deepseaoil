using System;
using System.IO;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>网格光照场契约：夜色反差、光场矩形对齐、着色器接入、暗部保底上限</summary>
    /// <remarks>不断言纹理上传与图形设备（EditMode 下不可靠）；对齐判据用纯函数算，不建场景对象。</remarks>
    [Category("Presentation")]
    public sealed class 网格光照场Tests
    {
        private const string CelLitShaderPath = "Assets/Resources/materials/Shaders/CelLit.shader";
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string TileFlowMatPath = "Assets/Resources/materials/M_TileFlow_Water.mat";

        // 取大保底高于夜色基准，max() 会把整片夜晚压平成中性灰
        private const float AmbientFloorCeiling = 0.20f;

        [Test]
        public void 时段参数_夜晚强度压到零点二为火光留出反差()
        {
            Assert.AreEqual(0.20f, TimeOfDaySetting.GetDefault(TimeOfDayKind.Night).Intensity, 0.001f, "夜晚未压到 0.20：火海爆不出来");
            Assert.AreEqual(0.98f, TimeOfDaySetting.GetDefault(TimeOfDayKind.Day).Intensity, 0.001f, "白天通透基准被改动");
        }

        [Test]
        public void 光照图矩形_纹素中心必须落在格心上()
        {
            var geometry = new GridGeometry(Vector2.zero, 1f);
            var bounds = new BoundsInt(-30, -17, 0, 63, 32, 1);

            Vector4 rect = TileGridLightmap.ComputeLightmapParams(in geometry, bounds);

            Assert.AreEqual(-30f, rect.x, 1e-4f, "光照图左边界不是最小格的外沿");
            Assert.AreEqual(-17f, rect.y, 1e-4f, "光照图下边界不是最小格的外沿");
            Assert.AreEqual(63f, rect.z, 1e-4f, "光照图宽度不等于格子数");
            Assert.AreEqual(32f, rect.w, 1e-4f, "光照图高度不等于格子数");

            var texelCenter = new Vector2(rect.x, rect.y) + new Vector2(geometry.CellSize * 0.5f, geometry.CellSize * 0.5f);

            Assert.AreEqual(geometry.CellCenter(bounds.min), texelCenter, "纹素中心与格心差半格，整张光场会整体偏移");
        }

        [Test]
        public void 着色器_CelLit已接入网格光照图且已剔除硬阶与时间抖动()
        {
            string source = File.ReadAllText(CelLitShaderPath);

            StringAssert.Contains("sampler_TileLightmap", source, "CelLit 未接入网格光照图：光斑会退回点光的正圆衰减");

            // 硬阶会把扩散的指数尾巴抬成平顶，平顶边界就是 45° 菱形；时间抖动会让临界像素群魔乱舞
            StringAssert.DoesNotContain("_CelBands", source, "CelLit 仍带分阶：光斑会被切成菱形平顶");
            StringAssert.DoesNotContain("_OrganicJitter", source, "CelLit 仍带手绘微扰属性");
            StringAssert.DoesNotContain("_Time.y", source, "CelLit 仍在光照采样里注入时间抖动：光斑会像沸水");

            int paramsDecl = source.IndexOf("float4 _TileLightmapParams", StringComparison.Ordinal);
            int cbufferEnd = source.IndexOf("CBUFFER_END", StringComparison.Ordinal);

            Assert.Greater(paramsDecl, 0, "CelLit 缺 _TileLightmapParams 声明");
            Assert.Greater(paramsDecl, cbufferEnd, "全局量 _TileLightmapParams 必须在 CBUFFER 之外，否则破坏 SRP Batcher 的常量布局");
        }

        [Test]
        public void 扩散_对角一步的透过率必须赢过两步正交()
        {
            const float axis = 0.72f;

            float diagonal = TileGridLightmap.DiagonalTransmission(axis);

            Assert.Less(diagonal, axis, "对角一步不该比正交一步透得更多");
            Assert.Greater(diagonal, axis * axis, "对角小于 a² 时对角项永远输给两步正交：等值线仍是 45° 菱形");
        }

        [Test]
        public void 着色器_每一层瓦片都采样网格光照图()
        {
            string[] paths =
            {
                "Assets/Resources/materials/Shaders/CelLit.shader",
                "Assets/Resources/materials/Shaders/TileFlow.shader",
                "Assets/Resources/materials/Shaders/TilePlantFlow.shader",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i]);

                StringAssert.Contains("sampler_TileLightmap", source, $"{paths[i]} 没接网格光照场：这层瓦片夜里不会被火光点亮");
                StringAssert.Contains("_CarryLightPos", source, $"{paths[i]} 没接随身光源：玩家脚下会是黑的");
            }
        }

        [Test]
        public void 渲染器_未显式指定材质的精灵必须走受光材质()
        {
            string source = File.ReadAllText("Assets/Settings/Renderer2D.asset");

            StringAssert.Contains("m_DefaultMaterialType: 2", source, "Renderer2D 默认材质类型不是 Custom：新建精灵会退回 URP 默认材质，不受网格光");
            StringAssert.Contains("guid: 4b9d1a76c3e24f8ea5d0b2c91e37a640", source, "Renderer2D 的 Custom 默认材质不是 M_CelLit_Default");
        }

        [Test]
        public void 预制体_不得再引用URP默认受光材质()
        {
            string[] prefabs = Directory.GetFiles("Assets/Resources", "*.prefab", SearchOption.AllDirectories);

            Assert.Greater(prefabs.Length, 0, "一个预制体都没扫到：路径筛选逻辑失效");

            for (int i = 0; i < prefabs.Length; i++)
            {
                StringAssert.DoesNotContain(
                    "a97c105638bdf8b4a8650670310a4cd3",
                    File.ReadAllText(prefabs[i]),
                    $"{prefabs[i]} 仍用 URP 默认精灵材质：这个物件不会被火光点亮");
            }
        }

        [Test]
        public void 投掷物_受光材质可从Resources取到()
        {
            Assert.IsNotNull(
                Resources.Load<Material>("materials/M_CelLit_Default"),
                "球换材质用的 Resources 路径取不到：球会退回只认全局光的 Sprite-Lit，靠近火堆也不亮");
        }

        [Test]
        public void 材质_暗部保底不得高于夜景基准()
        {
            AssertAmbientFloor(CelLitMatPath);
            AssertAmbientFloor(TileFlowMatPath);
        }

        [Test]
        public void 网格光照场_空气与墙体透过率默认在量纲内()
        {
            var go = new GameObject("TileGridLightmapProbe", typeof(TileGridLightmap));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<TileGridLightmap>());

                SerializedProperty air = serialized.FindProperty("airTransmission");
                SerializedProperty obstacle = serialized.FindProperty("obstacleTransmission");

                Assert.IsNotNull(air, "找不到 airTransmission 字段");
                Assert.IsNotNull(obstacle, "找不到 obstacleTransmission 字段");

                Assert.AreEqual(0.72f, air.floatValue, 1e-4f, "空气透过率基线被改动");
                Assert.AreEqual(0.20f, obstacle.floatValue, 1e-4f, "墙体透过率基线被改动");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 火光呼吸_频率必须低于闪烁阈值且随身光默认开启()
        {
            var go = new GameObject("TileGridLightmapBreathProbe", typeof(TileGridLightmap));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<TileGridLightmap>());

                SerializedProperty hertz = serialized.FindProperty("fireBreathHertz");
                SerializedProperty carry = serialized.FindProperty("carryLightIntensity");

                Assert.IsNotNull(hertz, "找不到 fireBreathHertz 字段");
                Assert.IsNotNull(carry, "找不到 carryLightIntensity 字段");

                // 2.5Hz 会被看成闪烁：呼吸必须压在 1.5Hz 以下
                Assert.Greater(hertz.floatValue, 0f, "呼吸频率为 0：火光彻底静止，失去生机");
                Assert.LessOrEqual(hertz.floatValue, 1.5f, "呼吸频率高于 1.5Hz：观感会从呼吸变成闪烁");

                Assert.Greater(carry.floatValue, 0f, "随身光源强度为 0：玩家在夜里照不亮脚下");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 参数暴露_光照常数默认值必须与旧硬编码逐位对齐()
        {
            var go = new GameObject("TileGridLightmapTuningProbe", typeof(TileGridLightmap));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<TileGridLightmap>());

                SerializedProperty baseProp = serialized.FindProperty("fireBreathBase");
                SerializedProperty exp = serialized.FindProperty("diagonalTransmissionExponent");
                SerializedProperty jitter = serialized.FindProperty("electricJitterHertz");
                SerializedProperty floor = serialized.FindProperty("electricPulseFloor");
                SerializedProperty depth = serialized.FindProperty("electricPulseDepth");
                SerializedProperty fire = serialized.FindProperty("fireSourceColor");
                SerializedProperty electric = serialized.FindProperty("electricSourceColor");

                Assert.IsNotNull(baseProp, "找不到 fireBreathBase");
                Assert.IsNotNull(exp, "找不到 diagonalTransmissionExponent");
                Assert.IsNotNull(jitter, "找不到 electricJitterHertz");
                Assert.IsNotNull(floor, "找不到 electricPulseFloor");
                Assert.IsNotNull(depth, "找不到 electricPulseDepth");
                Assert.IsNotNull(fire, "找不到 fireSourceColor");
                Assert.IsNotNull(electric, "找不到 electricSourceColor");

                Assert.AreEqual(1f, baseProp.floatValue, 1e-4f, "呼吸基准被改动：默认必须等于旧的 1.0");
                Assert.AreEqual(1.41421356f, exp.floatValue, 1e-4f, "对角指数被挪到 0.707：45° 菱形会回来");
                Assert.AreEqual(18f, jitter.floatValue, 1e-4f, "电弧频率基线被改动");
                Assert.AreEqual(0.85f, floor.floatValue, 1e-4f, "电光下限基线被改动");
                Assert.AreEqual(0.15f, depth.floatValue, 1e-4f, "电光幅度基线被改动");

                Color fireCol = fire.colorValue;
                Assert.AreEqual(1f, fireCol.r, 1e-3f, "火光红通道基线被改动");
                Assert.AreEqual(0.549f, fireCol.g, 1e-3f, "火光绿通道基线被改动");
                Assert.AreEqual(0.259f, fireCol.b, 1e-3f, "火光蓝通道基线被改动");

                Color electricCol = electric.colorValue;
                Assert.AreEqual(0.490f, electricCol.r, 1e-3f, "电光红通道基线被改动");
                Assert.AreEqual(0.976f, electricCol.g, 1e-3f, "电光绿通道基线被改动");
                Assert.AreEqual(1f, electricCol.b, 1e-3f, "电光蓝通道基线被改动");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 参数暴露_着色器新增系数必须同时写进属性与常量缓冲()
        {
            string[] paths =
            {
                "Assets/Resources/materials/Shaders/CelLit.shader",
                "Assets/Resources/materials/Shaders/TileFlow.shader",
                "Assets/Resources/materials/Shaders/TilePlantFlow.shader",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i]);

                StringAssert.Contains("_TileLightBoost (\"Tile Light Boost\"", source, $"{paths[i]} 的 _TileLightBoost 没写进 Properties：材质面板调不到");
                AssertInCBuffer(source, paths[i], "_TileLightBoost");
            }

            string celLit = File.ReadAllText(paths[0]);

            StringAssert.Contains("_RimAlphaWeight (\"Rim Alpha Weight\"", celLit, "CelLit 的 _RimAlphaWeight 没写进 Properties");
            AssertInCBuffer(celLit, paths[0], "_RimAlphaWeight");
        }

        [Test]
        public void 参数暴露_时段参数必须可由Inspector覆盖()
        {
            var go = new GameObject("EnvironmentDirectorProbe", typeof(EnvironmentDirector));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<EnvironmentDirector>());

                SerializedProperty overrides = serialized.FindProperty("customTimeOfDayOverrides");

                Assert.IsNotNull(overrides, "EnvironmentDirector 没暴露 customTimeOfDayOverrides：策划只能改代码调昼夜");
                Assert.IsTrue(overrides.isArray, "customTimeOfDayOverrides 不是数组");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void AssertInCBuffer(string source, string path, string name)
        {
            int start = source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int end = source.IndexOf("CBUFFER_END", StringComparison.Ordinal);
            int decl = source.IndexOf($"float {name};", StringComparison.Ordinal);

            Assert.Greater(start, 0, $"{path} 缺 UnityPerMaterial 常量缓冲");
            Assert.Greater(decl, start, $"{path} 的 {name} 没写进 UnityPerMaterial：SRP Batcher 会断批");
            Assert.Less(decl, end, $"{path} 的 {name} 落在常量缓冲之外：SRP Batcher 会断批");
        }

        [Test]
        public void 角色预制体_身体必须使用受光材质()
        {
            string[] paths =
            {
                "Assets/Resources/enemies/Enemy_1.prefab",
                "Assets/Resources/enemies/Enemy_2.prefab",
                "Assets/Resources/scane_prebs/Player.prefab",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i]);

                StringAssert.Contains("4b9d1a76c3e24f8ea5d0b2c91e37a640", source, $"{paths[i]} 的身体没挂受光材质");

                // SpriteFlashUnlit 不采样 _TileLightmap：挂回它，角色离火多远都是同一张原画
                StringAssert.DoesNotContain(
                    "d4b8e0f6a2c94f1d8b5e7a3c9f0d2e61", source, $"{paths[i]} 仍挂 SpriteFlashUnlit：角色不吃光照");
            }
        }

        [Test]
        public void 泉眼_三种kind必须各有光色且默认强度为正()
        {
            var go = new GameObject("FountainProbe", typeof(Fountain));

            try
            {
                var serialized = new SerializedObject(go.GetComponent<Fountain>());
                SerializedProperty kind = serialized.FindProperty("kind");
                SerializedProperty intensity = serialized.FindProperty("selfLightIntensity");

                Assert.IsNotNull(kind, "找不到 Fountain.kind");
                Assert.IsNotNull(intensity, "找不到 Fountain.selfLightIntensity");
                Assert.Greater(intensity.floatValue, 0f, "泉眼自发光默认强度为 0：三个泉眼在夜里依旧是黑的");

                Color water = ResolveFountainColor(serialized, kind, FountainKind.Water);
                Color earth = ResolveFountainColor(serialized, kind, FountainKind.Earth);
                Color life = ResolveFountainColor(serialized, kind, FountainKind.Life);

                Assert.AreNotEqual(water, earth, "水泉与土泉光色相同：夜里分不清是哪口泉");
                Assert.AreNotEqual(earth, life, "土泉与生命泉光色相同");
                Assert.AreNotEqual(water, life, "水泉与生命泉光色相同");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 网格光照场_道具光源可以重复登记与清空()
        {
            var go = new GameObject("SteadyEmitterProbe", typeof(TileGridLightmap));

            try
            {
                var lightmap = go.GetComponent<TileGridLightmap>();

                Assert.AreEqual(0, lightmap.SteadyEmitterCount, "新建的光照场不该自带道具光");

                lightmap.AddSteadyEmitter(Vector2.zero, Color.red, 0.5f, 0.15f);

                Assert.AreEqual(1, lightmap.SteadyEmitterCount, "道具光没登记上：泉眼不会发光");

                lightmap.AddSteadyEmitter(Vector2.zero, Color.red, 0f, 0.15f);

                Assert.AreEqual(1, lightmap.SteadyEmitterCount, "强度为 0 的道具光不该占名额");

                lightmap.ClearSteadyEmitters();

                Assert.AreEqual(0, lightmap.SteadyEmitterCount, "清空失败：重装配会把同一口泉登记两次");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Color ResolveFountainColor(SerializedObject serialized, SerializedProperty kind, FountainKind value)
        {
            kind.intValue = (int)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return ((Fountain)serialized.targetObject).SelfLightColor;
        }

        private static void AssertAmbientFloor(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Assert.IsNotNull(material, $"材质资产不存在：{path}");

            if (!material.HasProperty("_AmbientFloor")) Assert.Fail($"{path} 缺 _AmbientFloor");

            float floor = material.GetFloat("_AmbientFloor");

            Assert.LessOrEqual(floor, AmbientFloorCeiling, $"{path} 的暗部保底 {floor} 高于夜景基准：夜晚会被取大保底压平成中性灰");
        }
    }
}
