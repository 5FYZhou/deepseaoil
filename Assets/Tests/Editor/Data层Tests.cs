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
using DeepseaOil.Config;
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
            // 两个 Init 都是「重复调用即抛异常」，且都没有重置入口（蓝图 §14 开放项 O10）。
            // 因此本类依赖 Domain Reload：每个测试域只跑一次 OneTimeSetUp。
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

            // 这条是 Key 契约的上游形态：表里存「相对 Assets/ 带扩展名」
            Assert.AreEqual("Settings/Renderer2D.asset", weapon.Icon, "GetWeapon(1).Icon 不是 Assets 相对路径");

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
        }

        // ================================================================
        // A5 · 失败路径：不抛异常，句柄以 null 完成，计数进 FailedCount
        // ================================================================

        [UnityTest]
        public IEnumerator A5_失败路径不抛异常()
        {
            int failedBefore = DataMetrics.GetSnapshot().FailedCount;

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
            Assert.DoesNotThrow(() => AssetModule.Release("__never_loaded__"), "Release 未知 Key 抛异常");
            Assert.DoesNotThrow(() => AssetModule.Preload("Assets/Resources/ui/__preload_probe__.prefab"),
                "Preload 抛异常");
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
