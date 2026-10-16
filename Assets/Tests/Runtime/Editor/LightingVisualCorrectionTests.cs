using System.Collections.Generic;
using DeepseaOil.Presentation.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.Tests
{
    /// <summary>视觉矫正方案的机器判据：白天主光基准、光斑微扰封印、Tonemapping 剔除、火海降维</summary>
    /// <remarks>色彩空间是 Linear，任务书的十六进制色值必须经 Color.linear；裸 sRGB 字面量会被当成线性值而整体压暗。</remarks>
    [Category("Presentation")]
    public sealed class 视觉矫正Tests
    {
        private const string CelLitMatPath = "Assets/Resources/materials/M_CelLit_Default.mat";
        private const string ProfileAssetPath = "Assets/Settings/DefaultGlobalVolumeProfile.asset";

        [Test]
        public void 白天主光_强度为零点九八且为微暖白()
        {
            TimeOfDaySetting day = TimeOfDaySetting.GetDefault(TimeOfDayKind.Day);

            Assert.AreEqual(0.98f, day.Intensity, 0.001f, "白天全局光强度未设为 0.98 通透基准");
            Assert.AreEqual(1.0f, day.LightColor.r, 0.01f, "白天主光应无红通道偏色");
            Assert.GreaterOrEqual(day.LightColor.g, 0.95f, "白天主光绿通道偏离过大");
            Assert.GreaterOrEqual(day.LightColor.b, 0.86f, "白天主光蓝通道偏离过大（#FFFBF0 线性化后约 0.871）");
            Assert.Less(day.LightColor.b, day.LightColor.g, "暖白要求 B < G");
        }

        [Test]
        public void 色彩空间_工程为线性且白天色已线性化()
        {
            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace, "工程切到 Gamma 后整套 sRGB→线性换算都失去意义");

            TimeOfDaySetting day = TimeOfDaySetting.GetDefault(TimeOfDayKind.Day);

            // 裸字面量 #FFFBF0 = 0.941：命中即说明有人把 sRGB 当线性填了
            Assert.Less(day.LightColor.b, 0.94f, "白天主光 B 仍是 sRGB 裸值：线性空间下会整体发灰发暗");
        }

        [Test]
        public void 着色器_CelLit暴露微扰与自发光属性()
        {
            Shader celLit = Shader.Find("DeepseaOil/2D/CelLit");

            Assert.IsNotNull(celLit, "未找到 DeepseaOil/2D/CelLit");

            var probe = new Material(celLit);

            Assert.IsTrue(probe.HasProperty("_OrganicJitter"), "CelLit 缺 _OrganicJitter：光斑会退回同心正圆");
            Assert.IsTrue(probe.HasProperty("_EmissiveBoost"), "CelLit 缺 _EmissiveBoost：火海瓦片无法自发光");

            Object.DestroyImmediate(probe);
        }

        [Test]
        public void 材质_受光默认材质的微扰已封印在零点零一五()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CelLitMatPath);

            Assert.IsNotNull(material, $"材质资产不存在：{CelLitMatPath}（先跑 一键装配地表受光材质矩阵）");

            Assert.IsTrue(material.HasProperty("_OrganicJitter"), $"{CelLitMatPath} 缺 _OrganicJitter");
            Assert.AreEqual(0.015f, material.GetFloat("_OrganicJitter"), 0.001f, "光斑微扰未封印在 0.015");
        }

        [Test]
        public void 后处理_Tonemapping已剔除且色彩提纯已注入()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileAssetPath);

            if (profile == null) Assert.Ignore($"未找到 {ProfileAssetPath}（先跑 一键装配当前场景环境基座）");

            Assert.IsFalse(profile.TryGet(out Tonemapping tonemapping) && tonemapping.active, "Tonemapping 仍然生效：2D 纯色块会被二次压缩出灰雾");

            Assert.IsTrue(profile.TryGet(out ColorAdjustments adjustments), "缺少 Color Adjustments：线性空间损失未被补偿");
            Assert.IsTrue(adjustments.active, "Color Adjustments 未生效");
            Assert.AreEqual(10f, adjustments.contrast.value, 0.01f, "对比度未封印在 10");
            Assert.AreEqual(12f, adjustments.saturation.value, 0.01f, "饱和度未封印在 12");
        }

        [Test]
        public void 火海降维_五十格连通火海只占一盏氛围灯()
        {
            var row = new List<Vector3Int>(50);
            var blockA = new List<Vector3Int>(25);
            var blockB = new List<Vector3Int>(25);
            var lone = new List<Vector3Int>(2);

            for (int i = 0; i < 50; i++) row.Add(new Vector3Int(i, 0, 0));

            for (int i = 0; i < 25; i++)
            {
                blockA.Add(new Vector3Int(i, 0, 0));
                blockB.Add(new Vector3Int(i, 10, 0));
            }

            lone.Add(new Vector3Int(0, 0, 0));
            lone.Add(new Vector3Int(40, 40, 0));

            Assert.AreEqual(1, TileLightDirector.CountConnectedRegions(row), "50 格连通火海必须只合并成 1 盏重心灯");
            Assert.AreEqual(2, TileLightDirector.CountConnectedRegions(blockA), "两块各 25 格的火海必须各自 1 盏，不能跨屏共用");
            Assert.AreEqual(0, TileLightDirector.CountConnectedRegions(lone), "孤立 1~2 格不该走合并，逐格小灯更贴合形状");
        }

        [Test]
        public void 火海降维_两块断开火海不得共用一盏()
        {
            var split = new List<Vector3Int>(60);

            // 一条 5 格宽的带子中间挖空 1 列：两段仍是同一集合，但已不连通
            for (int i = 0; i < 30; i++) split.Add(new Vector3Int(i, 0, 0));
            for (int i = 31; i <= 60; i++) split.Add(new Vector3Int(i, 0, 0));

            Assert.AreEqual(2, TileLightDirector.CountConnectedRegions(split), "挖断后的两段必须各占一盏灯");
        }
    }
}
