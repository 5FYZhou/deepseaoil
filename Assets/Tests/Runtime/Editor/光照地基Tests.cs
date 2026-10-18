using DeepseaOil.Presentation.Lighting;
using DeepseaOil.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>光照地基契约测试：着色器可定位、时段参数不越界、图层常量精确</summary>
    /// <remarks>只断言契约不建场景对象；不断言 Shader.isSupported（无图形设备的 EditMode 下会假红）。</remarks>
    [Category("Presentation")]
    public sealed class 光照地基Tests
    {
        [Test]
        public void 着色器_两个受光着色器都能被引擎定位()
        {
            Assert.IsNotNull(Shader.Find("DeepseaOil/2D/CelLit"), "未找到 DeepseaOil/2D/CelLit");
            Assert.IsNotNull(Shader.Find("DeepseaOil/2D/TileFlow"), "未找到 DeepseaOil/2D/TileFlow");
        }

        [Test]
        public void 闪白通道_CelLit必须暴露与ActorTintDriver一致的材质属性()
        {
            Shader celLit = Shader.Find("DeepseaOil/2D/CelLit");

            Assert.IsNotNull(celLit);

            var probe = new Material(celLit);

            // ActorTintDriver 以 HasProperty("_FlashAmount") 决定闪白走材质插值还是 color 乘法
            Assert.IsTrue(probe.HasProperty("_FlashAmount"), "缺 _FlashAmount：受击闪白会退化成整段纯白乘法");
            Assert.IsTrue(probe.HasProperty("_FlashColor"), "缺 _FlashColor：闪白颜色无法按效果指定");
            Assert.IsTrue(probe.HasProperty("_AmbientFloor"), "缺 _AmbientFloor：暗部保底无法配置");

            Object.DestroyImmediate(probe);
        }

        [Test]
        public void 时段契约_所有时段强度都在保底之上()
        {
            TimeOfDayKind[] kinds = { TimeOfDayKind.Day, TimeOfDayKind.Dusk, TimeOfDayKind.Night };

            for (int i = 0; i < kinds.Length; i++)
            {
                TimeOfDaySetting setting = TimeOfDaySetting.GetDefault(kinds[i]);

                Assert.GreaterOrEqual(setting.Intensity, 0.15f, $"{kinds[i]} 跌破强度下限 0.15");
            }
        }

        [Test]
        public void 时段颜色_线性值不含负数与超限值()
        {
            TimeOfDayKind[] kinds = { TimeOfDayKind.Day, TimeOfDayKind.Dusk, TimeOfDayKind.Night };

            for (int i = 0; i < kinds.Length; i++)
            {
                Color c = TimeOfDaySetting.GetDefault(kinds[i]).LightColor;

                Assert.IsTrue(c.r >= 0f && c.r <= 1f, $"{kinds[i]} 的 R 越界");
                Assert.IsTrue(c.g >= 0f && c.g <= 1f, $"{kinds[i]} 的 G 越界");
                Assert.IsTrue(c.b >= 0f && c.b <= 1f, $"{kinds[i]} 的 B 越界");
            }
        }

        [Test]
        public void 排序层常量_六层名字面量精确匹配()
        {
            Assert.AreEqual("Default", SortingLayerConsts.Default);
            Assert.AreEqual("Ground", SortingLayerConsts.Ground);
            Assert.AreEqual("Actor", SortingLayerConsts.Actor);
            Assert.AreEqual("Aim", SortingLayerConsts.Aim);
            Assert.AreEqual("Effect", SortingLayerConsts.Effect);
            Assert.AreEqual("UI", SortingLayerConsts.UI);
        }

        [Test]
        public void 受光掩码_未登记图层不得因负ID移位而越界()
        {
            // 图层未在 TagManager 登记时 NameToID 返回 -1，1 << -1 是未定义移位
            int mask = SortingLayerConsts.LitLayersMask;

            Assert.IsTrue(mask == 0 || (mask & ~0x7fffffff) == 0, "掩码出现负位移出的高位");
        }
    }
}
