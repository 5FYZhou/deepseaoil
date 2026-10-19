// ------------------------------------------------------------
// HUD 受击反馈驱动 · EditMode 测试
//
// 【守什么】只守改错了不报错、只表现为观感不对的契约：基线只抓一次（到期必须精确归位）、
//   重播不叠加、暂停退化、三个驱动的失败模式（无锚点 / 非 RectTransform / 目标销毁）不抛。
//
// 【随机】抖动幅度与粒子方向都吃 UnityEngine.Random：用例先 InitState 固定种子，
//   否则「位置变没变」会偶发假绿。
//
// 【不覆盖】真实 Canvas 下的渲染结果、GamePlayPanel 的掉血判定（组合根接线，手工验证）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ------------------------------------------------------------

using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Effects.Drivers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DeepseaOil.Tests
{
    public class HUD受击反馈Tests
    {
        private const int Seed = 20261019;

        private GameObject _canvas;
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("HUD_feedback_canvas", typeof(Canvas));

            _texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            var pixels = new Color32[16];

            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);

            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_sprite != null) Object.DestroyImmediate(_sprite);
            if (_texture != null) Object.DestroyImmediate(_texture);
            if (_canvas != null) Object.DestroyImmediate(_canvas);

            EffectModule.Dispose();
        }

        [Test]
        public void 抖动到期精确回到基线且重播不叠加()
        {
            Random.InitState(Seed);

            RectTransform bar = MakeBar("shake_bar", withSprite: false);

            var baseline = new Vector2(12f, -34f);

            bar.anchoredPosition = baseline;

            var driver = new HudShakeDriver(maxSize: 2);

            EffectHandle first = PlayShake(driver, bar, amplitude: 20f, duration: 0.1f);

            Assert.IsTrue(first.IsValid, "有 RectTransform 锚点必须真的起抖");
            Assert.AreEqual(1, driver.ActiveInstanceCount);
            Assert.AreNotEqual(baseline, bar.anchoredPosition, "第一帧就该有偏移");

            driver.Tick(0.04f);

            PlayShake(driver, bar, amplitude: 20f, duration: 0.1f);

            // 重播只重置计时：若把偏移写进基线，这里就再也回不去了
            driver.Tick(0.2f);

            Assert.AreEqual(baseline, bar.anchoredPosition, "到期必须精确回到首次抓到的基线");
            Assert.AreEqual(0, driver.ActiveInstanceCount, "跑完要摘账，否则每帧白遍历");
        }

        [Test]
        public void 抖动在Stop与CleanAll时立刻归位()
        {
            Random.InitState(Seed);

            RectTransform bar = MakeBar("shake_stop", withSprite: false);

            bar.anchoredPosition = new Vector2(-5f, 8f);

            var driver = new HudShakeDriver(maxSize: 2);

            EffectHandle handle = PlayShake(driver, bar, amplitude: 20f, duration: 1f);

            driver.Tick(0.05f);

            driver.Stop(handle);

            Assert.AreEqual(new Vector2(-5f, 8f), bar.anchoredPosition, "Stop 必须还原基线");

            PlayShake(driver, bar, amplitude: 20f, duration: 1f);

            driver.Tick(0.05f);
            driver.CleanAll();

            Assert.AreEqual(new Vector2(-5f, 8f), bar.anchoredPosition, "CleanAll 必须还原基线");
            Assert.AreEqual(0, driver.ActiveInstanceCount);
        }

        [Test]
        public void 抖动锚点为空或不是RectTransform时返回None()
        {
            var driver = new HudShakeDriver(maxSize: 2);

            Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudShake, EffectContext.Default),
                "没给锚点必须返回 None");

            var plain = new GameObject("plain");

            try
            {
                EffectContext ctx = EffectContext.OnTarget(plain.transform);
                ctx.Value = 20f;

                Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudShake, in ctx),
                    "锚点不是 UI 节点时必须返回 None，不能去写 Transform.localPosition");
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void 抖动目标被销毁后Tick只摘账不抛()
        {
            Random.InitState(Seed);

            RectTransform bar = MakeBar("shake_dead", withSprite: false);

            var driver = new HudShakeDriver(maxSize: 2);

            PlayShake(driver, bar, amplitude: 20f, duration: 1f);

            Object.DestroyImmediate(bar.gameObject);

            Assert.DoesNotThrow(() => driver.Tick(0.016f), "Unity 假 null 必须只摘账");
            Assert.AreEqual(0, driver.ActiveInstanceCount);
        }

        [Test]
        public void 粒子按数量启用并在寿命末全部回收()
        {
            Random.InitState(Seed);

            RectTransform bar = MakeBar("spark_bar", withSprite: false);

            var driver = new HudSparkDriver(maxSize: 24);

            EffectContext ctx = SparkContext(bar, count: 6, life: 0.2f);

            EffectHandle handle = driver.Play(EffectId.HudSpark, in ctx);

            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(6, driver.ActiveInstanceCount, "Value 就是本次粒子数");

            driver.Tick(0.1f);

            Assert.AreEqual(6, driver.ActiveInstanceCount, "寿命没到不该提前熄灭");

            driver.Tick(0.2f);

            Assert.AreEqual(0, driver.ActiveInstanceCount, "到寿命全部回收");
            Assert.IsNotNull(bar.Find("[HudSparks]"), "rig 要常驻复用，不能每播一次重建");

            EffectHandle second = driver.Play(EffectId.HudSpark, in ctx);

            driver.Stop(second);

            Assert.AreEqual(0, driver.ActiveInstanceCount, "Stop 必须立刻熄灭本次粒子");
        }

        [Test]
        public void 粒子锚点为空或不是RectTransform时返回None()
        {
            var driver = new HudSparkDriver(maxSize: 24);

            Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudSpark, EffectContext.Default),
                "没给锚点必须返回 None");

            var plain = new GameObject("plain_spark");

            try
            {
                EffectContext ctx = SparkContext(plain.transform, count: 4, life: 0.2f);

                Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudSpark, in ctx));
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void 粒子目标被销毁后Tick只摘账不抛()
        {
            Random.InitState(Seed);

            RectTransform bar = MakeBar("spark_dead", withSprite: false);

            var driver = new HudSparkDriver(maxSize: 8);

            EffectContext ctx = SparkContext(bar, count: 4, life: 1f);

            driver.Play(EffectId.HudSpark, in ctx);

            Object.DestroyImmediate(bar.gameObject);

            Assert.DoesNotThrow(() => driver.Tick(0.016f));
            Assert.AreEqual(0, driver.ActiveInstanceCount);
        }

        [Test]
        public void 闪白从峰值淡到隐藏()
        {
            RectTransform bar = MakeBar("flash_bar", withSprite: true);

            var driver = new HudFlashDriver(maxSize: 2);

            EffectContext ctx = EffectContext.OnTarget(bar);
            ctx.Value = 0.9f;
            ctx.Duration = 0.1f;

            EffectHandle handle = driver.Play(EffectId.HudFlash, in ctx);

            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(1, driver.ActiveInstanceCount);

            Transform overlayTransform = bar.Find("[HudFlash]");

            Assert.IsNotNull(overlayTransform, "白图要挂在锚点下，跟着血条一起动");

            Image overlay = overlayTransform.GetComponent<Image>();

            Assert.IsTrue(overlay.enabled, "开播即亮");
            Assert.Greater(overlay.color.a, 0.8f, "峰值 alpha 要按 Value 拉满");

            driver.Tick(0.05f);

            Assert.Less(overlay.color.a, 0.8f, "半程必须已经在淡");
            Assert.Greater(overlay.color.a, 0f);

            driver.Tick(0.1f);

            Assert.IsFalse(overlay.enabled, "到期必须隐藏");
            Assert.AreEqual(0, driver.ActiveInstanceCount);
        }

        [Test]
        public void 闪白锚点没有sprite时只警告一次并跳过()
        {
            RectTransform bar = MakeBar("flash_nosprite", withSprite: true);

            // 换成没有 sprite 的图：盖成矩形白块比不闪更糟
            bar.GetComponent<Image>().sprite = null;

            var driver = new HudFlashDriver(maxSize: 2);

            EffectContext ctx = EffectContext.OnTarget(bar);
            ctx.Value = 0.9f;

            LogAssert.Expect(LogType.Warning, "[HudFlash] 锚点没有 Image 或 sprite，闪白会盖成白块，本次跳过。");

            Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudFlash, in ctx));
            Assert.AreEqual(EffectHandle.None, driver.Play(EffectId.HudFlash, in ctx), "第二次同样跳过");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 三种HUD特效都已装配到工厂()
        {
            EffectModule.Dispose();
            EffectModule.Init();

            RectTransform bar = MakeBar("factory_bar", withSprite: true);

            EffectContext shake = EffectContext.OnTarget(bar);
            shake.Value = 20f;
            shake.Duration = 0.1f;

            EffectContext flash = EffectContext.OnTarget(bar);
            flash.Value = 0.9f;

            EffectContext spark = SparkContext(bar, count: 4, life: 0.2f);

            // 漏 case 会静默落到 ParticleDriver：它去懒加载不存在的 prefab，句柄必为空
            Assert.IsTrue(EffectModule.Play(EffectId.HudShake, in shake).IsValid, "HudShake 漏了工厂 case");
            Assert.IsTrue(EffectModule.Play(EffectId.HudFlash, in flash).IsValid, "HudFlash 漏了工厂 case");
            Assert.IsTrue(EffectModule.Play(EffectId.HudSpark, in spark).IsValid, "HudSpark 漏了工厂 case");
        }

        private static EffectHandle PlayShake(HudShakeDriver driver, RectTransform target, float amplitude, float duration)
        {
            EffectContext ctx = EffectContext.OnTarget(target);

            ctx.Value = amplitude;
            ctx.Duration = duration;

            return driver.Play(EffectId.HudShake, in ctx);
        }

        private static EffectContext SparkContext(Transform target, int count, float life)
        {
            EffectContext ctx = EffectContext.OnTarget(target);

            ctx.Value = count;
            ctx.Duration = life;
            ctx.Modulation = new Vector2(420f, 1200f);
            ctx.Tint = Color.white;

            return ctx;
        }

        private RectTransform MakeBar(string name, bool withSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));

            go.transform.SetParent(_canvas.transform, false);

            var rect = (RectTransform)go.transform;

            rect.sizeDelta = new Vector2(550f, 137f);

            var image = go.AddComponent<Image>();

            image.raycastTarget = false;

            if (withSprite) image.sprite = _sprite;

            return rect;
        }
    }
}
