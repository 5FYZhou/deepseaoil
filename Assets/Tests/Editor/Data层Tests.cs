// ---------------------------------------------------------------------------
// Data 层 · 运行期测试
//
// 为什么放在 Assets/Tests/Editor/ 而不是 Assets/Tests/EditMode/：
//   Assets/Tests/EditMode/ 被 DeepseaOil.EditorTools.Tests.asmdef 覆盖，
//   而 asmdef 程序集**无法**引用预定义程序集 Assembly-CSharp —— Data 层就在 Assembly-CSharp 里，
//   所以那个目录下的测试「看不到 DeepseaOil.Data」。
//   本目录没有 asmdef，落 Assembly-CSharp-Editor：既能引用 Assembly-CSharp，
//   又由 Test Framework 自动引用 NUnit。详见 框架蓝图 §10.2「测试可达性」。
//
// 只用 public API：AssetRegistry / CacheStore / LifecycleMgr / RefCounter 都是 internal，
// 跨程序集不可见。想直接测它们，要么加 InternalsVisibleTo，要么把它们改成 public ——
// 两条都要先改设计，本次不做（蓝图 §14）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Text.RegularExpressions;
using DeepseaOil.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeepseaOil.Tests
{
    public class Data层Tests
    {
        /// <summary>真实资源：仓库里已存在的 UI 预设体（Assets/Resources/ui/Panel/BeginPanel.prefab）。</summary>
        const string PanelKey = "Assets/Resources/ui/Panel/BeginPanel.prefab";

        /// <summary>确定不存在的 Key，用来踩失败路径。</summary>
        const string MissingKey = "Assets/Resources/ui/__NoSuchAsset__.prefab";

        /// <summary>等待完成的上限帧数。超时即失败，并提示可能要改 PlayMode 测试。</summary>
        const int WaitFrames = 300;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // 可重复执行（同一个域里连按两次 Run All 也不会炸）：
            //   AssetModule.Dispose() 是幂等的 —— 未初始化时是 no-op，已初始化时清干净并复位标记，
            //   所以紧接着的 Init() 一定能成功。
            //   ConfigModule 没有重置入口（蓝图 §14 开放项 O10），只能靠 IsReady 守卫跳过；
            //   上一个 run 留下的 holder 仍在这个域里有效。
            AssetModule.Dispose();

            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

        // ================================================================
        // A1 · 配置链路：真实 StreamingAssets/Luban JSON → cfg.Tables → ConfigModule
        // ================================================================

        [Test]
        public void A1_配置链路跑通()
        {
            Assert.IsTrue(ConfigModule.IsReady, "ConfigModule 未就绪");

            var weapon = ConfigModule.GetWeapon(1);
            Assert.IsNotNull(weapon, "GetWeapon(1) 为 null");
            Assert.AreEqual("木剑", weapon.Name, "GetWeapon(1).Name");

            // 用 ToString 比较，避免依赖生成字段的具体数值类型
            Assert.AreEqual("10", weapon.Pow.ToString(), "GetWeapon(1).Pow");

            // Key 契约（蓝图 §11）：表里存的是**资源路径字符串**。
            // ⚠️ 这里**不能断言具体字面量**——那是策划填的数据，会随填表变化。
            //    本用例只校验"形态像资源路径"；"能不能真的加载出来"由 A4 用真实加载验收。
            Assert.IsFalse(string.IsNullOrEmpty(weapon.Icon), "GetWeapon(1).Icon 为空");
            Assert.IsTrue(Regex.IsMatch(weapon.Icon, @"\.(png|jpg|jpeg|tga|psd|asset|prefab|mat)$"),
                "GetWeapon(1).Icon 不像资源路径（缺可识别扩展名）：" + weapon.Icon);

            Assert.IsNotNull(weapon.IconItem_Ref, "外键 Weapon.icon_item → Item 未解析");
            Assert.IsNotNull(ConfigModule.GetFish(1002), "GetFish(1002) 为 null");

            Assert.AreEqual(3, ConfigModule.GetAllWeapons().Count, "GetAllWeapons().Count");

            Assert.AreEqual(3, TablesMeta.Count, "TablesMeta.Count");
            Assert.AreEqual(3, TablesMeta.Names.Length, "TablesMeta.Names.Length");

            Assert.IsNotNull(ConfigModule.Tables, "逃生舱 Tables 为 null");
        }

        // ================================================================
        // A2 · DataMetrics 拉模型
        // ================================================================

        [Test]
        public void A2_DataMetrics拉模型()
        {
            var snap = DataMetrics.GetSnapshot();

            Assert.IsTrue(snap.ConfigReady, "ConfigReady 应为 true");
            Assert.AreEqual(3, snap.TableCount, "TableCount 应等于 TablesMeta.Count");
            Assert.GreaterOrEqual(snap.CachedAssetCount, 0, "CachedAssetCount 不应为负");
            Assert.GreaterOrEqual(snap.CacheHitRate, 0f, "CacheHitRate 下界");
            Assert.LessOrEqual(snap.CacheHitRate, 1f, "CacheHitRate 上界");
        }

        // ================================================================
        // A3 · 装配错误必须暴露：两个 Init 都是「重复调用即抛」
        // ================================================================

        [Test]
        public void A3_重复Init抛异常()
        {
            Assert.Throws<InvalidOperationException>(() => ConfigModule.InitFromStreamingAssets(),
                "ConfigModule.Init 重复调用应抛 InvalidOperationException");
            Assert.Throws<InvalidOperationException>(() => AssetModule.Init(),
                "AssetModule.Init 重复调用应抛 InvalidOperationException");
        }

        // ================================================================
        // A4 · 真实资源端到端：Assets 相对 Key → ResolvePath → Resources.LoadAsync → 缓存
        // ================================================================

        [UnityTest]
        public IEnumerator A4_真实资源端到端()
        {
            var handle = AssetModule.LoadAsync<GameObject>(PanelKey);

            Assert.IsNotNull(handle, "LoadAsync 返回了 null 句柄");
            Assert.IsFalse(handle.IsDone, "第一次加载不应该是「已完成」句柄（说明缓存里已有条目）");

            yield return WaitDone(handle, "A4_真实资源端到端");

            Assert.IsNotNull(handle.Asset,
                "加载完成但资源为 null。可能原因：① 该路径不在 Assets/Resources/ 下；"
                + "② ResolvePath 转换错误；③ EditMode 下 Resources.LoadAsync 返回了空。Key = " + PanelKey);

            Assert.IsTrue(AssetModule.TryGet<GameObject>(PanelKey, out var cached), "TryGet 未命中刚加载的 Key");
            Assert.AreSame(handle.Asset, cached, "TryGet 拿到的不是句柄里的那个资源");

            AssetModule.Release(PanelKey);

            // ── 第二段：表里真实的 icon 能不能**真的**加载出来 ──
            // 这是 A1 不敢断言字面量的那一项的**真实验收**：走完整链路
            // 表值 → AssetRegistry.ResolvePath → Resources.LoadAsync<Sprite>。
            // 注意 icon 必须同时满足「存在」与「在 Assets/Resources/ 下」两个条件；
            // 只满足前者（例如 "Settings/Renderer2D.asset"）会在运行时才暴露，就是这条要防的。
            // 若加载失败，Data 层会打一条 Error → 本用例失败，这是预期行为。
            string icon = ConfigModule.GetWeapon(1).Icon;
            var iconHandle = AssetModule.LoadAsync<Sprite>(icon);

            yield return WaitDone(iconHandle, "A4_表内 icon 加载");

            Assert.IsNotNull(iconHandle.Asset,
                "表里的 icon 加载失败：应为 Sprite 且位于 Assets/Resources/ 下。Key = " + icon);

            AssetModule.Release(icon);
        }

        // ================================================================
        // A5 · 失败路径：不抛异常，句柄以 null 完成，计数进 FailedCount
        // ================================================================

        [UnityTest]
        public IEnumerator A5_失败路径不抛异常()
        {
            int failedBefore = DataMetrics.GetSnapshot().FailedCount;

            // Unity Test Framework 默认把测试期间出现的 LogType.Error 判为
            // 「Unhandled log message」并使**测试**失败（Warning 不会）。
            // 而本测试要断言的恰恰是「失败被**记录**下来、而不是抛异常」——
            // 所以必须先声明我们期待这条错误日志。
            // 附带好处：若 Data 层将来不再记这条错误，本测试会因「期待的日志未出现」而失败。
            //
            // 只声明一条：重试的前两次 HandleFailure 只重新入队、不打日志，
            // 只有重试耗尽后才走 RecordFailure 打一次 LogError。
            LogAssert.Expect(LogType.Error,
                new Regex(Regex.Escape("[Asset] load failed: " + MissingKey)));

            var handle = AssetModule.LoadAsync<GameObject>(MissingKey);
            Assert.IsNotNull(handle, "LoadAsync 返回了 null 句柄");

            yield return WaitDone(handle, "A5_失败路径不抛异常");

            // 未注册 fallback 时应以 null 完成；注册过则拿到 fallback。两者都算「不抛、有结果」。
            Assert.IsTrue(handle.IsDone, "失败后句柄仍未完成");

            Assert.GreaterOrEqual(DataMetrics.GetSnapshot().FailedCount, failedBefore + 1,
                "FailedCount 没有增长：失败没有被记录");
        }

        // ================================================================
        // A6 · 生命周期烟测：切场景 / 预加载 / 释放都不抛
        // ================================================================

        [Test]
        public void A6_生命周期烟测()
        {
            Assert.DoesNotThrow(() => AssetModule.OnSceneSwitch(), "OnSceneSwitch 抛异常");

            // 未知 Key 的 Release 只记 Warning（不抛）。Warning 不会让 UTF 判测试失败，
            // 所以这里不需要 LogAssert.Expect。
            Assert.DoesNotThrow(() => AssetModule.Release("__never_loaded__"), "Release 未知 Key 抛异常");

            // 用**真实存在**的路径做预加载探针。
            // 早先这里用的是不存在的路径，而 Preload 只是入队、要等 Tick 才真正发起加载 ——
            // 那个注定失败的加载会在后面的某一帧回调里打一条 Error，
            // 落进「另一个测试」或「任何测试之外」的作用域，造成偶发失败。
            Assert.DoesNotThrow(() => AssetModule.Preload(PanelKey), "Preload 抛异常");

            Assert.DoesNotThrow(() => AssetModule.Tick(0.016f), "Tick 抛异常");
            Assert.DoesNotThrow(() => DataMetrics.GetSnapshot(), "GetSnapshot 抛异常");
        }

        // ================================================================
        // 辅助
        // ================================================================

        /// <summary>
        /// 轮询推进加载直到句柄完成。Data 层的队列由 AssetModule.Tick 驱动，
        /// 而 Resources.LoadAsync 的完成还需要编辑器循环推进 —— 所以既要 Tick 也要 yield。
        /// </summary>
        static IEnumerator WaitDone<T>(AsyncHandle<T> handle, string tag) where T : UnityEngine.Object
        {
            for (int i = 0; i < WaitFrames && !handle.IsDone; i++)
            {
                AssetModule.Tick(0.016f);
                yield return null;
            }

            if (!handle.IsDone)
            {
                Assert.Fail(tag + "：等待 " + WaitFrames + " 帧后句柄仍未完成。"
                    + "最可能的原因：EditMode 测试里 Resources.LoadAsync 的 completed 回调不触发。"
                    + "处置：把本测试移到 PlayMode（见 框架蓝图 §14 待验证项 6），不要放宽这里的断言。");
            }
        }
    }
}
