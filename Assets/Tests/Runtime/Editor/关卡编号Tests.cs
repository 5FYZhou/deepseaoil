// 关卡编号契约：场景 Level_N ⇔ level_wave.id = N；Level_0 是教学关，没有配表行
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
        public void 教学关没有关卡配置()
        {
            Assert.Throws<ConfigLoadException>(() => ConfigModule.GetLevel(0));
        }
    }
}
