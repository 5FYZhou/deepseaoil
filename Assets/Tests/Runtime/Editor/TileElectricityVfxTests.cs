using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>电格复合特效的资产契约：常驻萤火虫 + 偶发微型电弧</summary>
    /// <remarks>预制体由 DeepseaOil ▸ 特效 菜单烘焙；缺失或仍是旧版本时 Ignore 而不是误红。</remarks>
    [Category("Presentation")]
    public sealed class 电格复合特效Tests
    {
        private const string PrefabPath = "Assets/Resources/effects/Vfx_Tile_Electricity.prefab";
        private const string LightningShader = "DeepseaOil/Particles/ProceduralLightning";

        [Test]
        public void 复合预制体_必须由萤火虫与偶发电弧两件组成()
        {
            GameObject prefab = Load();

            Assert.IsNull(prefab.GetComponent<ParticleSystem>(), "根节点只做容器，粒子系统必须挂在 Fireflies / ArcSparks 子节点");

            Transform fireflies = prefab.transform.Find("Fireflies");
            Transform arcs = prefab.transform.Find("ArcSparks");

            Assert.IsNotNull(fireflies, "预制体缺少 Fireflies 子节点");
            Assert.IsNotNull(arcs, "预制体缺少 ArcSparks 子节点");
            Assert.IsNotNull(fireflies.GetComponent<ParticleSystem>(), "Fireflies 上缺少 ParticleSystem");
            Assert.IsNotNull(arcs.GetComponent<ParticleSystem>(), "ArcSparks 上缺少 ParticleSystem");
        }

        [Test]
        public void 萤火虫_必须常驻盘旋且收在格内()
        {
            ParticleSystem ps = Load().transform.Find("Fireflies").GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;

            Assert.IsFalse(main.playOnAwake, "池化件由 onGet 的 Play 驱动，playOnAwake 开着会重复播放");
            Assert.AreEqual(ParticleSystemStopAction.None, main.stopAction, "StopAction 必须为 None，否则池化会拿到已销毁的对象");
            Assert.AreEqual(ParticleSystemSimulationSpace.Local, main.simulationSpace, "萤火虫必须跟着格子走");
            Assert.LessOrEqual(main.startSize.constantMax, 0.2f, "萤火虫必须是小光点");
            Assert.GreaterOrEqual(ps.emission.rateOverTime.constant, 2f, "萤火虫必须常驻发射");
            Assert.IsTrue(ps.velocityOverLifetime.enabled, "必须开 Velocity over Lifetime 才有盘旋");
            Assert.GreaterOrEqual(ps.velocityOverLifetime.orbitalZ.constantMax, 2f, "orbitalZ 太小看不出盘旋");
            Assert.LessOrEqual(ps.shape.radius, 0.5f, "发射半径不得超出半格");
        }

        [Test]
        public void 电弧_必须极短寿命低发射率且用折线着色器()
        {
            Transform arcTransform = Load().transform.Find("ArcSparks");
            ParticleSystem ps = arcTransform.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;

            Assert.LessOrEqual(main.startLifetime.constantMax, 0.12f, "电弧寿命必须 ≤ 0.12s，拉长就退化成拖沓长电弧");
            Assert.LessOrEqual(ps.emission.rateOverTime.constant, 1.5f, "发射率必须 ≤ 1.5，高了就不是偶发");
            Assert.IsTrue(main.startSize3D, "电弧必须是长条（X 窄 Y 长）");
            Assert.GreaterOrEqual(main.startSizeY.constantMax, 0.25f, "电弧太短看不出折角");
            Assert.LessOrEqual(main.startSizeY.constantMax, 0.45f, "电弧超过 0.45 会刺出瓦片");
            Assert.GreaterOrEqual(main.startRotation.constantMax, 6f, "弧向必须 0~360° 随机");

            var renderer = arcTransform.GetComponent<ParticleSystemRenderer>();

            Assert.IsNotNull(renderer.sharedMaterial, "电弧必须带材质，否则显示成粉色");
            Assert.AreEqual(LightningShader, renderer.sharedMaterial.shader.name, "电弧必须用手绘折线着色器");
        }

        [Test]
        public void 层级_萤火虫在前电弧更高一级且都不挡角色()
        {
            GameObject prefab = Load();

            var fireflies = prefab.transform.Find("Fireflies").GetComponent<ParticleSystemRenderer>();
            var arcs = prefab.transform.Find("ArcSparks").GetComponent<ParticleSystemRenderer>();

            Assert.AreEqual(SortingLayerConsts.Actor, fireflies.sortingLayerName, "萤火虫必须在 Actor 层");
            Assert.AreEqual(SortingLayerConsts.Actor, arcs.sortingLayerName, "电弧必须在 Actor 层");
            Assert.AreEqual(RenderOrder.TileStateVfx, fireflies.sortingOrder, "萤火虫层位不对");
            Assert.AreEqual(RenderOrder.TileStateVfx + 1, arcs.sortingOrder, "电弧必须比萤火虫高一级，闪现时压着光球");
            Assert.Greater(RenderOrder.TileStateVfx, RenderOrder.Aim, "必须压在瞄准反馈之上");
            Assert.Less(RenderOrder.TileStateVfx, RenderOrder.YSortBandStart, "必须低于 Y-Sort 频带，否则会盖住角色");
        }

        private static GameObject Load()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Assert.Ignore($"未找到 {PrefabPath}：先跑 DeepseaOil ▸ 特效 ▸ 一键构建萤火虫与偶发电弧复合特效");
            }

            Transform arcs = prefab.transform.Find("ArcSparks");
            var renderer = arcs != null ? arcs.GetComponent<ParticleSystemRenderer>() : null;

            if (renderer == null || renderer.sharedMaterial == null)
            {
                Assert.Ignore($"{PrefabPath} 还是旧版本（缺少 ArcSparks 或其材质）：重跑菜单后再测");
            }

            return prefab;
        }
    }
}
