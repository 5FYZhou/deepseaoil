using DeepseaOil.Presentation;
using NUnit.Framework;
using cfg.dso;

namespace DeepseaOil.Tests
{
    /// <summary>一次性音效占位时长口径：素材带静音尾巴时，voice 不能被 clip.length 占满</summary>
    public class 音频策略Tests
    {
        [Test]
        public void 长尾音效被截到上限()
        {
            Assert.AreEqual(SfxPlayback.DefaultMaxSeconds, SfxPlayback.SecondsOf(AudioId.EnemySpawn, 6.5f), 1e-4f,
                "6.5s 的出生音效没被截：它会占满 16 个 voice 并盖住 BGM");

            Assert.IsTrue(SfxPlayback.IsCapped(AudioId.EnemySpawn, 6.5f), "被截断的音效没走收尾淡出");
        }

        [Test]
        public void 短音效不被放大也不淡出()
        {
            Assert.AreEqual(0.3f, SfxPlayback.SecondsOf(AudioId.EnemyKnockback, 0.3f), 1e-4f,
                "短音效被拉到上限会变成占着 voice 的静音");

            Assert.IsFalse(SfxPlayback.IsCapped(AudioId.EnemyKnockback, 0.3f), "短音效不该淡出");
        }

        [Test]
        public void 死亡音效放完整段()
        {
            Assert.AreEqual(6.6f, SfxPlayback.SecondsOf(AudioId.PlayerDie, 6.6f), 1e-4f,
                "死亡 sting 被截断会只剩半句");

            Assert.IsFalse(SfxPlayback.IsCapped(AudioId.PlayerDie, 6.6f), "白名单内的音效不该走收尾淡出");
        }
    }
}
