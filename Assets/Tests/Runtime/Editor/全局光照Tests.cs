using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>全局光照契约测试：时段参数保底、排序层登记、受光掩码隔离</summary>
    /// <remarks>不断言 Light2D 与 Volume（EditMode 无图形设备时不可靠），场景表现由 PlayMode 目视验收。</remarks>
    [Category("Presentation")]
    public sealed class 全局光照Tests
    {
        private static readonly TimeOfDayKind[] AllKinds =
        {
            TimeOfDayKind.Day,
            TimeOfDayKind.Dusk,
            TimeOfDayKind.Night,
        };

        [Test]
        public void 时段参数_各时段强度均不低于暗部保底()
        {
            for (int i = 0; i < AllKinds.Length; i++)
            {
                TimeOfDaySetting setting = TimeOfDaySetting.GetDefault(AllKinds[i]);

                Assert.GreaterOrEqual(setting.Intensity, 0.15f, $"{AllKinds[i]} 跌破强度下限 0.15");
                Assert.Greater(
                    setting.AmbientFloorColor.r + setting.AmbientFloorColor.g + setting.AmbientFloorColor.b,
                    0.01f,
                    $"{AllKinds[i]} 暗部补偿色是纯黑");
            }
        }

        [Test]
        public void 时段颜色_线性值不含负数与超限值()
        {
            for (int i = 0; i < AllKinds.Length; i++)
            {
                Color c = TimeOfDaySetting.GetDefault(AllKinds[i]).LightColor;

                Assert.IsTrue(c.r >= 0f && c.r <= 1f, $"{AllKinds[i]} 的 R 越界");
                Assert.IsTrue(c.g >= 0f && c.g <= 1f, $"{AllKinds[i]} 的 G 越界");
                Assert.IsTrue(c.b >= 0f && c.b <= 1f, $"{AllKinds[i]} 的 B 越界");
            }
        }

        [Test]
        public void 排序层_五个受光与隔离层均已在TagManager登记()
        {
            string[] names =
            {
                SortingLayerConsts.Ground,
                SortingLayerConsts.Actor,
                SortingLayerConsts.Aim,
                SortingLayerConsts.Effect,
                SortingLayerConsts.UI,
            };

            for (int i = 0; i < names.Length; i++)
            {
                Assert.GreaterOrEqual(
                    SortingLayer.NameToID(names[i]),
                    0,
                    $"{names[i]} 未在 Tags and Layers 里登记；Global Light 2D 的照射层会落到 -1");
            }
        }

        [Test]
        public void 受光掩码_不包含UI特效与瞄准层()
        {
            if (SortingLayerConsts.GroundId < 0 || SortingLayerConsts.ActorId < 0) Assert.Ignore("Sorting Layer 未登记，跳过掩码判定");

            int mask = SortingLayerConsts.LitLayersMask;

            Assert.AreNotEqual(0, mask & (1 << SortingLayerConsts.GroundId), "掩码漏掉 Ground");
            Assert.AreNotEqual(0, mask & (1 << SortingLayerConsts.ActorId), "掩码漏掉 Actor");

            if (SortingLayerConsts.AimId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.AimId), "掩码错误照射 Aim");
            if (SortingLayerConsts.EffectId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.EffectId), "掩码错误照射 Effect");
            if (SortingLayerConsts.UIId >= 0) Assert.AreEqual(0, mask & (1 << SortingLayerConsts.UIId), "掩码错误照射 UI");
        }
    }
}
