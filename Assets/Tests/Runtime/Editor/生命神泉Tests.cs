using System.IO;
using DeepseaOil.Data;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.World;
using DeepseaOil.Presentation.World;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public class 生命神泉Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetModule.Dispose();

            if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
            ConfigModule.BindAssets();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

        [Test]
        public void 静止空蓄在一点五到一点八秒内完成一次治疗()
        {
            var state = new LifeFountainState(3f);
            const float dt = 1f / 60f;
            float elapsed = 0f;
            bool healed = false;

            while (elapsed < 2f && !healed)
            {
                elapsed += dt;
                healed = state.Tick(Vector2.zero, true, false, 0f, dt);
            }

            Assert.IsTrue(healed, "静止蓄力两秒仍未回血");
            Assert.GreaterOrEqual(elapsed, 1.5f, "静止回血过快");
            Assert.LessOrEqual(elapsed, 1.8f, "静止回血过慢");
        }

        [Test]
        public void 圈内移动和离圈都缓衰且不把已有蓄力清零()
        {
            var inside = new LifeFountainState(3f);
            var outside = new LifeFountainState(3f);

            inside.Tick(Vector2.zero, true, false, 0f, 1f);
            outside.Tick(Vector2.zero, true, false, 0f, 1f);

            float charged = inside.StillSeconds;

            inside.Tick(Vector2.zero, true, true, 0f, 0.5f);
            outside.Tick(Vector2.zero, false, false, 0f, 0.5f);

            Assert.Greater(charged, 0f, "前置蓄力没有累积");
            Assert.Greater(inside.StillSeconds, 0f, "圈内移动把蓄力清零");
            Assert.Greater(outside.StillSeconds, 0f, "离圈把蓄力清零");
            Assert.Less(inside.StillSeconds, charged, "圈内移动没有衰减");
            Assert.Less(outside.StillSeconds, inside.StillSeconds, "离圈衰减不比圈内移动衰减快");
        }

        [Test]
        public void 显式Leave仍然硬清零()
        {
            var state = new LifeFountainState(3f);

            state.Tick(Vector2.zero, true, false, 0f, 1f);
            state.Leave();

            Assert.AreEqual(0f, state.StillSeconds);
            Assert.IsFalse(state.IsStill);
        }

        [Test]
        public void 生命泉触发半径按原值扩大百分之十五()
        {
            Assert.AreEqual(3.45f, Fountain.ScaleTriggerRadius(3f, 1.15f), 0.0001f);
            Assert.AreEqual(1.725f, Fountain.ScaleTriggerRadius(1.5f, 1.15f), 0.0001f);
        }

        [Test]
        public void 满血时治疗请求不产生治疗()
        {
            var stats = new PlayerStats(ConfigModule.GetPlayer());

            Assert.IsFalse(stats.TryHeal(), "满血仍返回治疗效果");
            Assert.AreEqual(stats.Spec.MaxHp, stats.Current);
        }
    }

    public class 反馈视觉调优Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetModule.Dispose();

            if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
            ConfigModule.BindAssets();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

        [Test]
        public void 电弧脉冲扩大且蒸汽与电弧亮度提高()
        {
            Assert.AreEqual(1.5f, ConfigModule.VisualFeel.arcImpulseRadius, 0.0001f);
            Assert.Greater(ConfigModule.VisualFeel.arcImpulseIntensity, 0.35f);
            Assert.Greater(ConfigModule.VisualFeel.steamImpulseIntensity, 0.6f);
        }

        [Test]
        public void 衰减速率默认与预制体覆写均延长()
        {
            string source = File.ReadAllText("Assets/Scripts/Presentation/Lighting/TileGridLightmap.cs");
            string prefab = File.ReadAllText("Assets/Resources/scane_prebs/[Environment].prefab");

            StringAssert.Contains("[Range(1f, 30f)]", source);
            StringAssert.Contains("impulseDecayRate = 4.5f", source);
            StringAssert.Contains("impulseDecayRate: 4.5", prefab);
        }
    }
}
