// ---------------------------------------------------------------------------
// 粒子特效系统 · EditMode 测试
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 末级 Editor 是 Unity 的**硬要求**，
// 不能改名：asdef 程序集无法引用预定义程序集 Assembly-CSharp，而 EffectModule / Pool
// 就在 Assembly-CSharp 里。本目录没有 asmdef，靠路径里的 Editor 落 Assembly-CSharp-Editor。
//
// 【只用 public API】与 Data层Tests 同一约定：internal 类型（EffectCatalog / EffectSpec）
// 跨程序集不可见，所以本文件不碰它们，也就不需要 InternalsVisibleTo。
//
// 【覆盖判据】只钉「错了会静默出事」的：
//   P1 Prewarm 语义      —— 旧实现预热时调 onRelease，对象还没用过就被"归还"了
//   P2 CreateOrDrop 上限 —— 没有上限的池 = 高频 Play 时内存无限增长；DropSilently 又会让
//                            "忘了预热"变成"这个特效永远播不出来"（最难查的一类问题）
//   P3 CreateAndWarn     —— AudioManager 走的就是默认策略，行为必须一字不变
//   P4 归还超限销毁      —— 没有这条，池只会涨不会收
//   P5 IDisposable       —— 释放后使用必须响亮地失败，而不是静默操作一个空池
//   P6 Release(null)     —— GameObject 池会遇到"对象已被外部销毁"；抛异常会炸掉整帧
//   T1 EffectContext     —— default / 只写一个字段时 Scale 与 Direction 的归一化；
//                            参考实现这里会把特效缩到 1%（new EffectContext{Intensity=0.5f}）
//   T2 EffectHandle      —— 哨兵值与相等语义
//   M1/M3/M6/M7 失败模式 —— 未 Init / 未注册 / Preload 早调 / 懒加载被拒：都必须只报错不抛
//   M4 幂等守卫          —— 二次 Init 不得静默重建第二份驱动表
//   M5 空跑              —— 装配完什么资源都没有时，Tick/CleanAll/Dispose 不能报错
//   D1 预制体缺组件      —— 借出的对象必须归还，否则每次失败都漏一个池位
//   D2 池满丢弃 + Stop   —— 上限真的生效、回收真的回池、复用真的发新句柄
//   D3 CleanAll + 旧句柄 —— 过期句柄不得误停 CleanAll 之后的新实例（epoch 语义）
//   D4 未 Init 的 Stop   —— 装配顺序出错时不得炸帧
//
// 【不覆盖】渲染结果、粒子外观、真实 prefab 的加载（需要美术资产，只能人工验收）、
// 播放到期后的自动回收（EditMode 下粒子不模拟，断言它等于写假测试）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;
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

        // ================================================================
        // Pool
        // ================================================================

        [Test]
        public void P1_Prewarm不得触发onGet与onRelease()
        {
            int created = 0;
            int got = 0;
            int released = 0;

            var pool = new Pool<TrackedObject>(
                factory: () => { created++; return new TrackedObject(); },
                onGet: _ => got++,
                onRelease: _ => released++,
                name: "P1",
                maxSize: 8,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            pool.Prewarm(3);

            Assert.AreEqual(3, pool.IdleCount, "预热 3 个应全部进空闲区");
            Assert.AreEqual(3, created, "factory 应被调用 3 次");
            Assert.AreEqual(0, released, "预热是「造对象」，不是「归还对象」——不得触发 onRelease");
            Assert.AreEqual(0, got, "预热不得触发 onGet");

            PoolStats stats = pool.GetStats();
            Assert.AreEqual(0, stats.Active, "预热不算借出");
            Assert.AreEqual(3, stats.Idle);
            Assert.AreEqual(3, stats.TotalCreated);

            pool.Dispose();
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
            Assert.AreEqual(2, stats.Peak);
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

        // ================================================================
        // 数据类型
        // ================================================================

        [Test]
        public void T1_EffectContext默认值与部分初始化()
        {
            EffectContext zero = default;
            Assert.AreEqual(1f, zero.Scale, 1e-5f, "default 的 Scale 必须归一为 1");
            Assert.AreEqual(Vector2.up, zero.Direction, "default 的 Direction 必须归一为 up");
            Assert.AreEqual(0f, zero.Intensity, 1e-5f, "未显式赋值的 Intensity 是 0（0~1 语义的最小值）");
            Assert.IsFalse(zero.FollowRequested);

            var partial = new EffectContext { Intensity = 0.5f };
            Assert.AreEqual(1f, partial.Scale, 1e-5f,
                "只写 Intensity 时 Scale 仍应是 1；参考实现这里会缩到 1%（Mathf.Max(0.01f, 0)）");
            Assert.AreEqual(0.5f, partial.Intensity, 1e-5f);

            EffectContext at = EffectContext.At(new Vector2(1f, 2f));
            Assert.AreEqual(new Vector2(1f, 2f), at.Position);
            Assert.AreEqual(1f, at.Intensity, 1e-5f, "At() 是满强度");
            Assert.AreEqual(Vector2.up, at.Direction);

            EffectContext dir = EffectContext.At(Vector2.zero, new Vector2(3f, 4f));
            Assert.AreEqual(1f, dir.Direction.magnitude, 1e-4f, "Direction 必须归一化");
            Assert.AreEqual(0.6f, dir.Direction.x, 1e-4f);

            Assert.AreEqual(1f, new EffectContext { Intensity = 5f }.Intensity, 1e-5f, "Intensity 上钳位");
            Assert.AreEqual(0f, new EffectContext { Intensity = -1f }.Intensity, 1e-5f, "Intensity 下钳位");

            var go = new GameObject("T1_target");
            try
            {
                go.transform.position = new Vector3(5f, 6f, 0f);

                EffectContext onTarget = EffectContext.OnTarget(go.transform);
                Assert.IsTrue(onTarget.FollowRequested);
                Assert.AreSame(go.transform, onTarget.Follow);
                Assert.AreEqual(new Vector2(5f, 6f), onTarget.Position, "OnTarget 应取目标当前位置");

                Assert.DoesNotThrow(() => EffectContext.OnTarget(null), "目标为 null 不得抛");
                Assert.IsFalse(EffectContext.OnTarget(null).FollowRequested);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T2_EffectHandle_None与相等语义()
        {
            EffectHandle none = EffectHandle.None;

            Assert.IsFalse(none.IsValid, "None 不是有效句柄");
            Assert.AreEqual("None", none.ToString());
            Assert.IsTrue(none == default, "None 就是 default");
            Assert.IsFalse(none != default);
            Assert.IsTrue(none.Equals((object)default(EffectHandle)));
            Assert.AreEqual(default(EffectHandle).GetHashCode(), none.GetHashCode(), "相等对象必须有相同哈希");

            // 无效句柄经 EffectModule.Stop 必须是 no-op（M1 之外的独立断言，防止守卫被删）
            Assert.DoesNotThrow(() => EffectModule.Stop(none));
        }

        // ================================================================
        // EffectModule 生命周期与失败模式
        // ================================================================

        [Test]
        public void M1_未Init时Play返回None并报错()
        {
            Assert.IsFalse(EffectModule.IsInitialized, "SetUp 之后应处于未 Init 状态");

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectModule.Play 在 Init 之前被调用")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.HitSpark, EffectContext.Default));
        }

        [Test]
        public void M2_Play_None静默返回None()
        {
            EffectModule.Init();

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.None, EffectContext.Default),
                "None 是哨兵值：静默返回，不查表、不报错");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void M3_未注册的EffectId报错并返回None()
        {
            EffectModule.Init();

            // ScreenShake 故意不在 EffectCatalog 里（它的驱动还没实现）——这正是「未注册」的可见形态
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectId ScreenShake 未注册")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.ScreenShake, EffectContext.Default));
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
        public void M5_空跑InitTickCleanAllDispose不报错()
        {
            EffectModule.Init();

            EffectStats stats = EffectModule.GetStats();
            Assert.Greater(stats.DriverCount, 0, "EffectCatalog 至少应装配出一个驱动");
            Assert.AreEqual(0, stats.ActiveInstances, "Instance 都还没播，活跃数必须是 0");
            Assert.AreEqual(0, stats.PooledObjects, "资源未到位时池还没建，待用数必须是 0");

            Assert.DoesNotThrow(() => EffectModule.Tick(0.016f));
            Assert.DoesNotThrow(() => EffectModule.CleanAll());
            Assert.DoesNotThrow(() => EffectModule.Stop(EffectHandle.None));
            Assert.DoesNotThrow(() => EffectModule.Tick(0f));
            Assert.DoesNotThrow(() => EffectModule.Dispose());

            Assert.IsFalse(EffectModule.IsInitialized);
            Assert.AreEqual(0, EffectModule.GetStats().DriverCount, "Dispose 之后不得残留驱动");
            Assert.AreEqual(0, EffectModule.GetStats().ActiveInstances);
        }

        [Test]
        public void M6_Preload未Init时报错不抛()
        {
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] EffectModule.Preload 在 Init 之前被调用")));

            Assert.DoesNotThrow(() => EffectModule.Preload());
        }

        [Test]
        public void M7_懒加载在AssetModule未Init时被拒且不抛()
        {
            // 保证前置条件确定：本用例只验证「资源层没就绪时懒加载被拦住」，
            // 不依赖测试集里别的 fixture 有没有 Init 过 AssetModule。
            DeepseaOil.Data.AssetModule.Dispose();

            EffectModule.Init();

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("[Effect] 懒加载 HitSpark 失败")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.HitSpark, EffectContext.Default),
                "资源未就位时 Play 返回 None，但请求会被记下（由 EffectModule 在资源到位后补播）");
        }

        // ================================================================
        // ParticleDriver
        // ================================================================

        [Test]
        public void D1_预制体没有ParticleSystem时报错且不占用池()
        {
            EffectModule.Init();

            _prefab = new GameObject("D1_prefab");   // 故意不给 ParticleSystem
            _root = new GameObject("D1_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 4, prewarm: 0, assetKey: "effects/D1");

            // 用 Catalog 里没有的 ObjectShake 手工注册：不碰真实资源，用例自给自足
            EffectModule.Register(EffectId.ObjectShake, driver);

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("预制体（含子物体）上没有 ParticleSystem")));

            Assert.AreEqual(EffectHandle.None, EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero)));

            Assert.AreEqual(0, driver.ActiveInstanceCount, "失败的播放不得留下活跃实例");
            Assert.AreEqual(1, driver.PooledObjectCount, "借出的对象必须归还池，否则每次失败漏一个池位");
            Assert.AreEqual(0, EffectModule.GetStats().ActiveInstances);
        }

        [Test]
        public void D2_池满丢弃并返回None_Stop之后回池()
        {
            EffectModule.Init();

            _prefab = new GameObject("D2_prefab", typeof(ParticleSystem));
            _root = new GameObject("D2_root");

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 1, prewarm: 0, assetKey: "effects/D2");

            EffectModule.Register(EffectId.ObjectShake, driver);

            EffectHandle first = EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(first.IsValid, "第 1 次播放应成功（空闲区空 → 现场实例化）");
            Assert.AreEqual(1, driver.ActiveInstanceCount);

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("池已满")));

            EffectHandle second = EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.one));
            Assert.AreEqual(EffectHandle.None, second, "到达上限后应丢弃并返回 None");
            Assert.AreEqual(1, driver.ActiveInstanceCount, "丢弃不得改变活跃数");

            EffectModule.Stop(first);
            Assert.AreEqual(0, driver.ActiveInstanceCount, "Stop 之后应回收");
            Assert.AreEqual(1, driver.PooledObjectCount, "回收的对象应回到池里");

            EffectHandle third = EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));
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

            EffectModule.Register(EffectId.ObjectShake, driver);

            EffectHandle before = EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));
            Assert.IsTrue(before.IsValid);

            EffectModule.CleanAll();
            Assert.AreEqual(0, driver.ActiveInstanceCount, "CleanAll 应清空活跃实例");
            Assert.AreEqual(0, EffectModule.GetStats().ActiveInstances);

            EffectHandle after = EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));
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

            EffectModule.Register(EffectId.ObjectShake, driver);

            EffectHandle a = EffectModule.Play(EffectId.ObjectShake,
                new EffectContext { Position = Vector2.zero, Intensity = 0.2f, Scale = 1f });

            EffectHandle b = EffectModule.Play(EffectId.ObjectShake,
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

            // 作者在预制体上授权的乘数：Curve / Random Between Two Curves 模式下，
            // Inspector 曲线下方的 "Multiplier" 就是这两个字段，作者的整个幅度可能都在里面。
            ParticleSystem prefabPs = _prefab.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule prefabMain = prefabPs.main;
            prefabMain.startSizeMultiplier = 0.25f;
            ParticleSystem.EmissionModule prefabEmission = prefabPs.emission;
            prefabEmission.rateOverTimeMultiplier = 3f;

            var driver = new ParticleDriver(_prefab, _root.transform,
                isSingleton: false, maxSize: 4, prewarm: 0, assetKey: "effects/D6");

            EffectModule.Register(EffectId.ObjectShake, driver);

            // ① Intensity = 1（At 系列就是 1）：必须是作者原值，一位都不能改
            EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));

            ParticleSystem inst = _root.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(inst, "应实例化出一个池对象");
            Assert.AreEqual(0.25f, inst.main.startSizeMultiplier, 1e-4f,
                "作者的 size 乘数被覆盖成 1 —— 预制体里的 Start Size 会「变成默认大小」");
            Assert.AreEqual(3f, inst.emission.rateOverTimeMultiplier, 1e-4f,
                "作者的 rate 乘数被覆盖 —— 粒子量会与预制体对不上");

            // ② Intensity = 0：作者值 × MinIntensityScale(0.4)，而不是绝对 0.4
            EffectModule.CleanAll();
            EffectModule.Play(EffectId.ObjectShake,
                new EffectContext { Position = Vector2.zero, Intensity = 0f, Scale = 1f });

            ParticleSystem low = _root.GetComponentInChildren<ParticleSystem>();
            Assert.AreEqual(0.1f, low.main.startSizeMultiplier, 1e-4f, "强度 0 应是 0.25 × 0.4");
            Assert.AreEqual(1.2f, low.emission.rateOverTimeMultiplier, 1e-4f, "强度 0 应是 3 × 0.4");

            // ③ 再回到 Intensity = 1：不得叠加（0.25 仍是 0.25，不是 0.0625）
            EffectModule.CleanAll();
            EffectModule.Play(EffectId.ObjectShake, EffectContext.At(Vector2.zero));

            ParticleSystem again = _root.GetComponentInChildren<ParticleSystem>();
            Assert.AreEqual(0.25f, again.main.startSizeMultiplier, 1e-4f,
                "多次播放把乘数越缩越小：缩放基准必须是预制体的作者值，不是上一次的结果");
            Assert.AreEqual(3f, again.emission.rateOverTimeMultiplier, 1e-4f);
        }
    }
}
