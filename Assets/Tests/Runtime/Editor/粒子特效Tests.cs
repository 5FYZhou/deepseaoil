// ------------------------------------------------------------
// 粒子特效系统 · EditMode 测试
//
// 【只用 public API】internal 类型（EffectCatalog / EffectSpec）跨程序集不可见，故不碰它们，也不需要 InternalsVisibleTo。
//
// 【守什么】判据只有一条：这条用例守的是不是「改错了不报错、只表现为手感/观感不对」。
//   池的收支（上限生效 / 归还超限销毁 / 失败播放必须归还），EffectContext 的零值与退化，
//   失败模式必须报错且不抛（未 Init / 未注册），幂等与 no-op 守卫，作者乘数只缩放不覆盖，
//   单例型合并，旧句柄的 epoch 语义，工厂漏 case 这类静默失效（漏了落到 ParticleDriver，只表现为不播）。
//
// 【颜色栈两路】换过闪白材质的走材质插值，没换的退化成 color 乘法，两条路都要守还原与基线。
//
// 【未注册判据不写死 id】写死的期望会随 EffectCatalog 增行整条落空。
//
// 【不覆盖】渲染结果、粒子外观、真实 prefab 的加载（需美术资产，只能人工验收）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ------------------------------------------------------------

using System;
using System.Text.RegularExpressions;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Effects.Drivers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeepseaOil.Tests
{
    public class 粒子特效Tests
    {
        /// <summary>随便一个引用类型，用来测池的计数与回调，与 Unity 无关。</summary>
        private sealed class TrackedObject
        {
        }

        private GameObject _prefab;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            // Dispose 是幂等的：未 Init 时 no-op，已 Init 时清干净并复位标记，
            // 于是每个用例都从「未 Init」这个确定的起点开始（不依赖用例执行顺序）。
            EffectModule.Dispose();
        }

        [TearDown]
        public void TearDown()
        {
            EffectModule.Dispose();

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void P2_CreateOrDrop到达上限后丢弃并计数()
        {
            var pool = new Pool<TrackedObject>(
                factory: () => new TrackedObject(),
                name: "P2",
                maxSize: 2,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            Assert.IsTrue(pool.TryGet(out TrackedObject a), "第 1 次借出");
            Assert.IsTrue(pool.TryGet(out TrackedObject b), "第 2 次借出（现场创建，不超上限）");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);

            Assert.IsFalse(pool.TryGet(out TrackedObject c), "第 3 次应被丢弃（已达上限 2）");
            Assert.IsNull(c, "丢弃时不得给出对象");

            PoolStats stats = pool.GetStats();
            Assert.AreEqual(2, stats.Active);
            Assert.AreEqual(2, stats.Peak, "峰值要如实记录（它是 PoolStats 唯一的读者，删了它就成零消费者成员）");
            Assert.AreEqual(2, stats.TotalCreated, "丢弃时不得再创建");
            Assert.AreEqual(1, stats.TotalDropped);

            pool.Release(a);
            Assert.IsTrue(pool.TryGet(out TrackedObject d), "归还之后应能再借出");
            Assert.IsNotNull(d);

            pool.Dispose();
        }

        [Test]
        public void P3_CreateAndWarn保持旧语义_池空现场创建并警告()
        {
            var pool = new Pool<TrackedObject>(
                factory: () => new TrackedObject(),
                name: "P3",
                maxSize: 1,
                overflowPolicy: PoolOverflowPolicy.CreateAndWarn);

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("[Pool<P3>] 池为空")));

            Assert.IsTrue(pool.TryGet(out _), "第 1 次：池空 → 现场创建");
            Assert.IsTrue(pool.TryGet(out _), "第 2 次：CreateAndWarn 不设上限 → 仍然创建");

            Assert.AreEqual(2, pool.GetStats().TotalCreated, "旧语义下池会随用随涨（AudioManager 依赖这条不变）");

            pool.Dispose();
        }

        [Test]
        public void P4_归还超过空闲上限时销毁对象()
        {
            int destroyed = 0;

            var pool = new Pool<TrackedObject>(
                factory: () => new TrackedObject(),
                onRelease: null,
                name: "P4",
                maxSize: 1,
                overflowPolicy: PoolOverflowPolicy.CreateAndWarn,
                onDestroy: _ => destroyed++);

            pool.Prewarm(1);

            Assert.IsTrue(pool.TryGet(out TrackedObject a));
            Assert.IsTrue(pool.TryGet(out TrackedObject b));   // 池空 → 现场创建第 2 个（会打 Warning，不算失败）

            pool.Release(a);    // 空闲 0 → 收下
            pool.Release(b);    // 空闲已满 → 销毁

            Assert.AreEqual(1, destroyed, "超出空闲上限的对象应被销毁，否则池只涨不收");
            Assert.AreEqual(1, pool.IdleCount);
            Assert.AreEqual(0, pool.ActiveCount);

            pool.Dispose();
        }

        [Test]
        public void P5_Dispose幂等且释放后使用抛异常()
        {
            var pool = new Pool<TrackedObject>(factory: () => new TrackedObject(), name: "P5", maxSize: 4);

            pool.Dispose();
            Assert.DoesNotThrow(() => pool.Dispose(), "Dispose 必须幂等");

            Assert.Throws<ObjectDisposedException>(() => pool.Get(), "释放后借出必须响亮地失败");
            Assert.Throws<ObjectDisposedException>(() => pool.Prewarm(1), "释放后预热必须响亮地失败");
            Assert.Throws<ObjectDisposedException>(() => pool.Release(new TrackedObject()), "释放后归还必须响亮地失败");
        }

        [Test]
        public void P6_Release_null只警告不抛()
        {
            var pool = new Pool<TrackedObject>(factory: () => new TrackedObject(), name: "P6", maxSize: 2);

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("[Pool<P6>] Release(null) 被忽略")));

            Assert.DoesNotThrow(() => pool.Release(null), "池在帧循环里，抛异常会连带炸掉整帧");
            Assert.AreEqual(0, pool.ActiveCount);

            pool.Dispose();
        }

        /// <summary>零值与"只写一个字段"都必须归一：参考实现这里会把特效缩到 1%。</summary>
        [Test]
        public void T1_EffectContext默认值与部分初始化()
        {
            EffectContext zero = default;
            Assert.AreEqual(1f, zero.Scale, 1e-5f, "default 的 Scale 必须归一为 1");
            Assert.AreEqual(Vector2.up, zero.Direction, "default 的 Direction 必须归一为 up");

            var partial = new EffectContext { Intensity = 0.5f };
            Assert.AreEqual(1f, partial.Scale, 1e-5f, "只写 Intensity 时 Scale 仍应是 1；参考实现这里会缩到 1%");

            EffectContext dir = EffectContext.At(Vector2.zero, new Vector2(3f, 4f));
            Assert.AreEqual(1f, dir.Direction.magnitude, 1e-4f, "Direction 必须归一化");

            Assert.AreEqual(1f, new EffectContext { Intensity = 5f }.Intensity, 1e-5f, "Intensity 上钳位");
            Assert.AreEqual(0f, new EffectContext { Intensity = -1f }.Intensity, 1e-5f, "Intensity 下钳位");

            Assert.DoesNotThrow(() => EffectContext.OnTarget(null), "目标为 null 不得抛");
            Assert.IsFalse(EffectContext.OnTarget(null).FollowRequested);
        }

        [Test]
        public void M1_未Init时Play返回None并报错()
        {
            Assert.IsFalse(EffectModule.IsInitialized, "SetUp 之后应处于未 Init 状态");

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectModule.Play 在 Init 之前被调用")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.BurstSparks, EffectContext.Default));
        }

        [Test]
        public void M3_未注册的EffectId报错并返回None()
        {
            EffectModule.Init();

            // 判据不写死具体 id：写死的期望会随 EffectCatalog 增行整条落空。
            // 也只有 Shake 能播：其余已登记项都要资源，AssetModule 没 Init 时 Play 会打懒加载失败日志把期望顺序打乱。
            Assert.Greater(EffectModule.GetStats().DriverCount, 0, "驱动表得装配过，否则下面走的是 Init 之前的失败分支");

            if (AssetModule.IsInitialized)
            {
                Assert.Pass("AssetModule 已被其它用例 Init：本条的两个判据都依赖未 Init，跳过。");

                return;
            }

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectId Shake 未注册")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.Shake, EffectContext.Default), "未注册的 id 必须返回 None");
        }

        [Test]
        public void M4_重复Init报错且不重置()
        {
            EffectModule.Init();
            int driversBefore = EffectModule.GetStats().DriverCount;

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectModule.Init 被调用了两次")));
            EffectModule.Init();

            Assert.IsTrue(EffectModule.IsInitialized);
            Assert.AreEqual(driversBefore, EffectModule.GetStats().DriverCount,
                "第二次 Init 不得静默重建第二份驱动表");
        }

        [Test]
        public void D1_预制体没有ParticleSystem时报错且不占用池()
        {
            EffectModule.Init();

            _prefab = new GameObject("D1_prefab");   // 故意不给 ParticleSystem
            _root = new GameObject("D1_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 4, prewarm: 0, assetKey: "effects/D1");

            EffectModule.Register(EffectId.Shake, driver);

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("预制体（含子物体）上没有 ParticleSystem")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero)));

            Assert.AreEqual(0, driver.ActiveInstanceCount, "失败的播放不得留下活跃实例");
            Assert.AreEqual(1, driver.PooledObjectCount, "借出的对象必须归还池，否则每次失败漏一个池位");
        }

        [Test]
        public void D2_池满丢弃并返回None_Stop之后回池()
        {
            EffectModule.Init();

            _prefab = new GameObject("D2_prefab", typeof(ParticleSystem));
            _root = new GameObject("D2_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 1, prewarm: 0, assetKey: "effects/D2");

            EffectModule.Register(EffectId.Shake, driver);

            EffectHandle first = EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(first.IsValid, "第 1 次播放应成功（空闲区空 → 现场实例化）");
            Assert.AreEqual(1, driver.ActiveInstanceCount);

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("池已满")));

            EffectHandle second = EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.one));
            Assert.AreEqual(EffectHandle.None, second, "到达上限后应丢弃并返回 None");
            Assert.AreEqual(1, driver.ActiveInstanceCount, "丢弃不得改变活跃数");

            EffectModule.Stop(first);
            Assert.AreEqual(0, driver.ActiveInstanceCount, "Stop 之后应回收");
            Assert.AreEqual(1, driver.PooledObjectCount, "回收的对象应回到池里");

            EffectHandle third = EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(third.IsValid, "池里有货时应能再播");
            Assert.AreNotEqual(first, third, "复用池对象也要发新句柄");
        }

        [Test]
        public void D3_CleanAll回收实例且旧句柄成为no_op()
        {
            EffectModule.Init();

            _prefab = new GameObject("D3_prefab", typeof(ParticleSystem));
            _root = new GameObject("D3_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 4, prewarm: 0, assetKey: "effects/D3");

            EffectModule.Register(EffectId.Shake, driver);

            EffectHandle before = EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(before.IsValid);

            EffectModule.CleanAll();
            Assert.AreEqual(0, driver.ActiveInstanceCount, "CleanAll 应清空活跃实例");

            EffectHandle after = EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(after.IsValid, "CleanAll 之后应能重新播");

            EffectModule.Stop(before);
            Assert.AreEqual(1, driver.ActiveInstanceCount,
                "CleanAll 之前发出的句柄必须失效，不得误停 CleanAll 之后的新实例");

            EffectModule.Stop(after);
            Assert.AreEqual(0, driver.ActiveInstanceCount);
        }

        [Test]
        public void D4_未Init时Stop与CleanAll与Tick是no_op()
        {
            Assert.IsFalse(EffectModule.IsInitialized);

            Assert.DoesNotThrow(() => EffectModule.Stop(EffectHandle.None));
            Assert.DoesNotThrow(() => EffectModule.CleanAll());
            Assert.DoesNotThrow(() => EffectModule.Tick(0.016f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void D5_单例型重复Play合并且不新增实例()
        {
            EffectModule.Init();

            _prefab = new GameObject("D5_prefab", typeof(ParticleSystem));
            _root = new GameObject("D5_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: true, maxSize: 2, prewarm: 0, assetKey: "effects/D5");

            EffectModule.Register(EffectId.Shake, driver);

            EffectHandle a = EffectModule.Play(EffectId.Shake,
                new EffectContext { Position = Vector2.zero, Intensity = 0.2f, Scale = 1f });

            EffectHandle b = EffectModule.Play(EffectId.Shake,
                new EffectContext { Position = Vector2.one, Intensity = 0.9f, Scale = 1f });

            Assert.IsTrue(a.IsValid);
            Assert.AreEqual(a, b, "单例型：重复 Play 应合并到同一实例，返回同一句柄");
            Assert.AreEqual(1, driver.ActiveInstanceCount, "单例型不得新增实例");

            EffectModule.Stop(b);
            Assert.AreEqual(0, driver.ActiveInstanceCount, "单例型：任一 handle 都能停掉当前实例");
        }

        [Test]
        public void D6_作者乘数只缩放不覆盖()
        {
            EffectModule.Init();

            _prefab = new GameObject("D6_prefab", typeof(ParticleSystem));
            _root = new GameObject("D6_root");

            // 作者授权的乘数：Curve 类模式下幅度全在 Inspector 曲线的 Multiplier 字段里。
            ParticleSystem prefabPs = _prefab.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule prefabMain = prefabPs.main;
            prefabMain.startSizeMultiplier = 0.25f;
            ParticleSystem.EmissionModule prefabEmission = prefabPs.emission;
            prefabEmission.rateOverTimeMultiplier = 3f;

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 4, prewarm: 0, assetKey: "effects/D6");

            EffectModule.Register(EffectId.Shake, driver);

            // ① Intensity = 1（At 系列就是 1）：必须是作者原值，一位都不能改
            EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));

            ParticleSystem inst = _root.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(inst, "应实例化出一个池对象");
            Assert.AreEqual(0.25f, inst.main.startSizeMultiplier, 1e-4f,
                "作者的 size 乘数被覆盖成 1 —— 预制体里的 Start Size 会「变成默认大小」");
            Assert.AreEqual(3f, inst.emission.rateOverTimeMultiplier, 1e-4f, "作者的 rate 乘数被覆盖 —— 粒子量会与预制体对不上");

            // ② Intensity = 0：作者值 × MinIntensityScale(0.4)，而不是绝对 0.4
            EffectModule.CleanAll();
            EffectModule.Play(EffectId.Shake, new EffectContext { Position = Vector2.zero, Intensity = 0f, Scale = 1f });

            ParticleSystem low = _root.GetComponentInChildren<ParticleSystem>();
            Assert.AreEqual(0.1f, low.main.startSizeMultiplier, 1e-4f, "强度 0 应是 0.25 × 0.4");
            Assert.AreEqual(1.2f, low.emission.rateOverTimeMultiplier, 1e-4f, "强度 0 应是 3 × 0.4");

            // ③ 再回到 Intensity = 1：不得叠加（0.25 仍是 0.25，不是 0.0625）
            EffectModule.CleanAll();
            EffectModule.Play(EffectId.Shake, EffectContext.At(Vector2.zero));

            ParticleSystem again = _root.GetComponentInChildren<ParticleSystem>();
            Assert.AreEqual(0.25f, again.main.startSizeMultiplier, 1e-4f,
                "多次播放把乘数越缩越小：缩放基准必须是预制体的作者值");
            Assert.AreEqual(3f, again.emission.rateOverTimeMultiplier, 1e-4f);
        }

        /// <summary>在测试根下造一具体：View 上是真身，ShadowView 上的不是。</summary>
        private static Transform MakeBody(
            Transform parent,
            Color baseline,
            Color shadowTint,
            out SpriteRenderer body,
            out SpriteRenderer shadow)
        {
            var root = new GameObject("N1_root");

            root.transform.SetParent(parent, false);

            var view = new GameObject("View");
            view.transform.SetParent(root.transform, false);

            body = view.AddComponent<SpriteRenderer>();
            body.color = baseline;   // 非白：证明基线被捕获

            var shadowNode = new GameObject("ShadowView");
            shadowNode.transform.SetParent(view.transform, false);

            shadow = shadowNode.AddComponent<SpriteRenderer>();
            shadow.color = shadowTint;

            return root.transform;
        }

        /// <summary>给真身换精灵闪白材质：两条路都要有用例守着</summary>
        private static bool TryAttachFlashMaterial(SpriteRenderer body)
        {
            Material flash = Resources.Load<Material>("materials/M_SpriteFlash");

            if (flash == null) return false;

            body.sharedMaterial = flash;

            return true;
        }

        [Test]
        public void N1_颜色栈在乘法回退路上的瞬态持续态与三条还原路径()
        {
            EffectModule.Init();

            _root = new GameObject("N1_test_root");

            var driver = new ActorTintDriver(maxSize: 4);

            EffectModule.Register(EffectId.Shake, driver);

            Color baseline = new Color(0.9f, 0.8f, 0.7f, 1f);
            Color shadowTint = new Color(0f, 0f, 0f, 0.6f);

            Transform actor = MakeBody(_root.transform, baseline, shadowTint, out SpriteRenderer body, out SpriteRenderer shadow);

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.Flash, EffectContext.Default),
                "没给 Follow 必须返回 None");

            LogAssert.NoUnexpectedReceived();

            var tint = new Color(0.45f, 0.45f, 0.45f, 1f);

            var slow = EffectContext.OnTarget(actor);
            slow.Tint = tint;

            EffectHandle slowHandle = EffectModule.Play(EffectId.SlowTint, in slow);

            Assert.IsTrue(
                slowHandle.IsValid,
                "减速垫色必须真被 ActorTintDriver 接住（漏 case 会落到 ParticleDriver）");

            // 颜色必须走 Unity 的容差 ==；NUnit 对 Color 是按位比，浮点误差会让两边打印一模一样却判不等
            Assert.IsTrue(tint * baseline == body.color, "持续态乘在基线上");
            Assert.IsTrue(shadow.color == shadowTint, "ShadowView 不被着色");

            var flash = EffectContext.OnTarget(actor);
            flash.Duration = 0.12f;

            EffectModule.Play(EffectId.Flash, in flash);

            Assert.IsTrue(body.color == Color.white, "没换材质的渲染器上，瞬态必须退化成乘法闪白而不是什么都不做");

            EffectModule.Tick(0.05f);
            EffectModule.Tick(0.05f);

            Assert.IsTrue(body.color == Color.white, "0.12 秒未到，闪白不该提前掉");

            EffectModule.Tick(0.05f);

            Assert.IsTrue(tint * baseline == body.color, "到点自动退回减速色");

            EffectModule.Stop(slowHandle);

            Assert.IsTrue(body.color == baseline, "Stop 必须还原基线色");

            EffectModule.Play(EffectId.Flash, in flash);
            EffectModule.CleanAll();

            Assert.IsTrue(body.color == baseline, "CleanAll 必须还原基线色，不能洗成白");

            EffectModule.Stop(slowHandle);

            Assert.IsTrue(body.color == baseline, "旧句柄必须失效");

            UnityEngine.Object.DestroyImmediate(actor.gameObject);

            Assert.DoesNotThrow(() => EffectModule.Tick(0.016f), "目标销毁后 Tick 只能摘账");
            Assert.AreEqual(0, driver.ActiveInstanceCount, "目标销毁后账本必须归零");

            driver.Dispose();
        }

        [Test]
        public void N2_换上闪白材质后走材质插值且不污染影子与常态色()
        {
            EffectModule.Init();

            _root = new GameObject("N2_test_root");

            var driver = new ActorTintDriver(maxSize: 4);

            EffectModule.Register(EffectId.Shake, driver);

            Color baseline = new Color(0.9f, 0.8f, 0.7f, 1f);
            Color shadowTint = new Color(0f, 0f, 0f, 0.6f);

            Transform actor = MakeBody(_root.transform, baseline, shadowTint, out SpriteRenderer body, out SpriteRenderer shadow);

            Assert.IsTrue(TryAttachFlashMaterial(body), "M_SpriteFlash 必须在 Resources 里：Assets/Resources/materials/M_SpriteFlash.mat");

            var tint = new Color(0.45f, 0.45f, 0.45f, 1f);

            var slow = EffectContext.OnTarget(actor);
            slow.Tint = tint;

            EffectHandle slowHandle = EffectModule.Play(EffectId.SlowTint, in slow);

            var flash = EffectContext.OnTarget(actor);
            flash.Duration = 0.12f;

            EffectModule.Play(EffectId.Flash, in flash);

            var block = new MaterialPropertyBlock();

            body.GetPropertyBlock(block);

            Assert.AreEqual(1f, block.GetFloat("_FlashAmount"), 1e-4f, "闪白要靠材质插值拉满，乘数是拉不出纯白的");
            Assert.IsTrue(tint * baseline == body.color, "瞬态在材质通道上跑，color 只能写持续态");

            var shadowBlock = new MaterialPropertyBlock();

            shadow.GetPropertyBlock(shadowBlock);

            Assert.IsTrue(shadowBlock.isEmpty, "ShadowView 不该拿到任何属性块");

            EffectModule.Tick(0.06f);

            body.GetPropertyBlock(block);

            Assert.AreEqual(0.5f, block.GetFloat("_FlashAmount"), 0.15f, "0.12 秒行程过半，插值要能线性落回来");

            EffectModule.Tick(0.06f);
            EffectModule.Tick(0.06f);

            body.GetPropertyBlock(block);

            Assert.AreEqual(0f, block.GetFloat("_FlashAmount"), 1e-4f, "到点必须归零，否则角色永远停在纯白");

            EffectModule.Stop(slowHandle);

            Assert.IsTrue(body.color == baseline, "Stop 只还原 color，不该动材质通道");

            EffectModule.CleanAll();

            Assert.IsTrue(body.color == baseline, "CleanAll 必须还原基线色");

            body.GetPropertyBlock(block);

            Assert.AreEqual(0f, block.GetFloat("_FlashAmount"), 1e-4f, "共享材质上不许残留满值");

            UnityEngine.Object.DestroyImmediate(actor.gameObject);

            driver.Dispose();
        }
    }
}
