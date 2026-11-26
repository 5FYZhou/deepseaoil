using DeepseaOil.Data;
using DeepseaOil.Presentation;
using DeepseaOil.Presentation.Story;
using DeepseaOil.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 引导与UI反馈Tests
    {
        [Test]
        public void 打字机逐字推进并跳过空白字符()
        {
            Assert.AreEqual(3, Typewriter.RevealCount(0.1f, 24f, 100));
            Assert.AreEqual(100, Typewriter.RevealCount(10f, 24f, 100));
            Assert.AreEqual(100, Typewriter.RevealCount(0f, 0f, 100));
            Assert.AreEqual(0, Typewriter.RevealCount(0f, 24f, 0));
            Assert.IsTrue(Typewriter.IsRevealable('水'));
            Assert.IsFalse(Typewriter.IsRevealable(' '));
            Assert.IsFalse(Typewriter.IsRevealable('\n'));
        }

        [Test]
        public void UI短音按最小间隔节流且有界变调()
        {
            float next = 0f;

            Assert.IsTrue(SfxPlayback.TryReserveUiBlip(1f, 0f, ref next));
            Assert.AreEqual(1.055f, next, 1e-4f);
            Assert.IsFalse(SfxPlayback.TryReserveUiBlip(1.054f, 0.055f, ref next));
            Assert.IsTrue(SfxPlayback.TryReserveUiBlip(1.055f, 0.055f, ref next));

            for (int i = 0; i < 9; i++)
                Assert.That(SfxPlayback.UiBlipPitch(i), Is.InRange(0.98f, 1.02f));
        }

        [Test]
        public void 弹窗读完后的停留时间不接受负值()
        {
            Assert.AreEqual(3f, StoryFlow.NormalizePopupHoldSeconds(3f));
            Assert.AreEqual(0f, StoryFlow.NormalizePopupHoldSeconds(-1f));
            Assert.AreEqual(0f, StoryFlow.NormalizePopupHoldSeconds(float.NaN));
        }

        [Test]
        public void 导航只在安全框外出现并指向屏幕边()
        {
            Rect safe = new Rect(0.06f, 0.08f, 0.88f, 0.84f);

            Assert.IsFalse(
                SupplyNavIndicator.TryResolveEdge(
                    new Vector3(0.5f, 0.5f, 1f),
                    safe,
                    out _,
                    out _));

            Assert.IsFalse(
                SupplyNavIndicator.TryResolveEdge(
                    new Vector3(1.5f, 0.5f, -1f),
                    safe,
                    out _,
                    out _));

            Assert.IsTrue(
                SupplyNavIndicator.TryResolveEdge(
                    new Vector3(1.5f, 0.5f, 1f),
                    safe,
                    out Vector2 edge,
                    out float angle));

            Assert.AreEqual(0.94f, edge.x, 1e-4f);
            Assert.AreEqual(0.5f, edge.y, 1e-4f);
            Assert.AreEqual(-90f, angle, 1e-4f);
        }

        [Test]
        public void 弹窗滑行距离包含自身宽度与边缘余量()
        {
            var go = new GameObject("popup_motion", typeof(RectTransform));

            try
            {
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(800f, 350f);
                go.AddComponent<PopupMotion>();

                Assert.AreEqual(848f, PopupMotion.SlideDistance(rect, 48f), 1e-4f);
                Assert.AreEqual(1f, PopupMotion.EdgeDirection(-400f));
                Assert.AreEqual(-1f, PopupMotion.EdgeDirection(400f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 未激活弹窗不启动滑入且激活后可启动()
        {
            var go = new GameObject("inactive_popup_motion", typeof(RectTransform));

            try
            {
                go.SetActive(false);
                PopupMotion motion = go.AddComponent<PopupMotion>();

                Assert.DoesNotThrow(() => motion.PlayEnter());
                Assert.IsFalse(motion.IsAnimating);

                go.SetActive(true);
                motion.PlayEnter();

                Assert.IsTrue(motion.IsAnimating);
                motion.HideImmediate();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 残血提示受回血冷却和次数上限守卫()
        {
            Assert.IsTrue(
                HudHintBanner.ShouldShowLowHealth(
                    2f,
                    5f,
                    false,
                    true,
                    8.5f,
                    8.5f,
                    0f,
                    0));

            Assert.IsFalse(
                HudHintBanner.ShouldShowLowHealth(
                    2f,
                    5f,
                    true,
                    true,
                    99f,
                    8.5f,
                    0f,
                    0));

            Assert.IsFalse(
                HudHintBanner.ShouldShowLowHealth(
                    2f,
                    5f,
                    false,
                    true,
                    99f,
                    8.5f,
                    0.1f,
                    0));

            Assert.IsFalse(
                HudHintBanner.ShouldShowLowHealth(
                    2f,
                    5f,
                    false,
                    true,
                    99f,
                    8.5f,
                    0f,
                    3));
        }

        [Test]
        public void 剧情与弹窗强调不再使用纯红且命中语义色()
        {
            EnsureDataAssets();

            var contexts = new System.Text.StringBuilder();
            int[] paragraphs =
            {
                1001, 1002, 1003, 2001, 3001, 4001, 4002, 5001, 5002, 6001, 6002, 6003, 7001,
            };

            for (int i = 0; i < paragraphs.Length; i++)
            {
                var lines = ConfigModule.GetDialogueParagraph(paragraphs[i]);

                for (int j = 0; j < lines.Count; j++)
                {
                    contexts.Append(lines[j].Context);
                    contexts.Append('\n');
                }
            }

            string text = contexts.ToString();

            Assert.IsFalse(text.Contains("#FF0000", System.StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains("<color=#F9CE5A>", text);
            StringAssert.Contains("<color=#338CFF>", text);
            StringAssert.Contains("<color=#D0B185>", text);
            StringAssert.Contains("<color=#7EB88A>", text);
            StringAssert.Contains("<color=#DA684C>", text);
        }

        [Test]
        public void 种子满槽提示区分新增配给与脚边掉落()
        {
            Assert.IsFalse(HudHintBanner.ShouldAnnounceSeedDrop(true, false, false));
            Assert.IsTrue(HudHintBanner.ShouldAnnounceSeedDrop(true, true, true));
            Assert.IsTrue(HudHintBanner.ShouldAnnounceSeedDrop(false, false, false));
            Assert.IsFalse(HudHintBanner.ShouldAnnounceSeedDrop(false, false, true));

            EnsureDataAssets();

            string dropped = HudHintBanner.BuildSeedSupplyMessage(SeedType.Fire, "火种子", true);
            StringAssert.Contains("已放在脚边", dropped);
            StringAssert.Contains("按 Q 丢弃旧种", dropped);
            StringAssert.Contains("【火种子】", dropped);

            string granted = HudHintBanner.BuildSeedSupplyMessage(SeedType.Fire, "火种子", false);
            StringAssert.Contains("获得", granted);
            StringAssert.Contains("按 E", granted);

            AssertSeedPromptColor(SeedType.Fire, "火种子", "#FF2D1A");
            AssertSeedPromptColor(SeedType.Elec, "电种子", "#884CFF");
            AssertSeedPromptColor(SeedType.Ice, "冰种子", "#66CDFF");
            AssertSeedPromptColor(SeedType.Plant, "植物种子", "#7EB88A");
        }

        private static void EnsureDataAssets()
        {
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            if (!AssetModule.IsInitialized)
                AssetModule.Init();

            ConfigModule.BindAssets();
        }

        private static void AssertSeedPromptColor(SeedType seedType, string name, string hex)
        {
            string prompt = HudHintBanner.BuildSeedSupplyMessage(seedType, name, false);

            StringAssert.Contains($"<color={hex}>", prompt);
            Assert.IsFalse(prompt.Contains("#85c6c8", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
