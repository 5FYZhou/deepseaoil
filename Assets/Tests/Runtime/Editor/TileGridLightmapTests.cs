using System;
using System.IO;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Lighting;
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
