// 关卡编号契约：场景 Level_N ⇔ level_wave.id = N；教学关 id=0，配了行才有波次
using DeepseaOil.Data;
using NUnit.Framework;

namespace DeepseaOil.Tests
{
    public class 关卡编号Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();
        }

        [Test]
        public void 场景名与关卡号一一对应()
        {
            Assert.AreEqual("Level_0", SceneName.GetLevel(0));
            Assert.AreEqual("Level_5", SceneName.GetLevel(5));
            Assert.AreEqual("Level_0", SceneName.Game, "新游戏必须先进教学关");
        }

        [Test]
        public void 五关配置齐全且波次非空()
        {
            for (int id = 1; id <= 5; id++)
            {
                LevelSpec level = ConfigModule.GetLevel(id);

                Assert.AreEqual(id, level.Id);
                Assert.IsNotNull(level.Waves);
                Assert.Greater(level.Waves.Count, 0, $"关卡 {id} 没有配置波次");
            }
        }

        [Test]
        public void 教学关的配表行若存在就必须有波次()
        {
            if (!ConfigModule.HasLevel(0)) return;

            LevelSpec level = ConfigModule.GetLevel(0);

            Assert.AreEqual(0, level.Id);
            Assert.Greater(level.Waves.Count, 0, "教学关的 level_wave 行必须指向至少一波");
        }

        [Test]
        public void 各关波次数符合5_2编排()
        {
            int[] expected = { 2, 3, 4, 4, 7 };

            for (int id = 1; id <= 5; id++)
            {
                LevelSpec level = ConfigModule.GetLevel(id);

                Assert.AreEqual(expected[id - 1], level.Waves.Count, $"关卡 {id} 的波次数与 5.2 编排不符");
            }
        }

        [Test]
        public void 教学关那一波正好两只普怪()
        {
            LevelSpec level = ConfigModule.GetLevel(0);

            Assert.AreEqual(1, level.Waves.Count, "教学关只该有一波");

            WaveSpec wave = level.Waves[0];

            int total = 0;

            for (int i = 0; i < wave.Enemies.Count; i++) total += wave.Enemies[i].Count;

            Assert.AreEqual(2, total, "教学关那一波必须正好 2 只（教程关不刷精英）");
        }
    }
}
