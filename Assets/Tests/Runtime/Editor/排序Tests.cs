// ---------------------------------------------------------------------------
// 表现层排序 · Y-Sort 行为测试
//
// 【为什么单独一个文件】它是工程里唯一一条"表现层数学"，与战斗、移动都无关：
//   塞进 战斗框架Tests 会让那份文件的主题变糊，而它要守的不变量很集中
//   （单调、钳制、量化、非法输入退化）。
//
// 【只测纯函数】YSort.OrderFor 与 RenderOrder 的档位换算不碰引擎对象，所以能逐条钉住；
//   真正"谁盖住谁"的观感只能人工 Play 看（两个角色站在不同 y 上）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Foundation;
using DeepseaOil.Presentation;
using NUnit.Framework;

namespace DeepseaOil.Tests
{
    /// <summary>Y-Sort 与渲染档位的纯函数测试。</summary>
    [Category("Presentation")]
    public class 排序Tests
    {
        [Test]
        public void 排序_越靠下档位越大()
        {
            int near = YSort.OrderFor(0f, 500, 559, 4f);
            int far = YSort.OrderFor(2f, 500, 559, 4f);

            Assert.Greater(near, far, "y 越小（越靠下）必须越晚画 —— 否则「站在前面的人」会被后面的挡住");
        }

        [Test]
        public void 排序_档位始终落在频带内()
        {
            float[] ys = { -1000f, -50f, -1f, 0f, 1f, 7.5f, 1000f, float.NaN };

            for (int i = 0; i < ys.Length; i++)
            {
                int order = YSort.OrderFor(ys[i], 500, 559, 4f);

                Assert.GreaterOrEqual(order, 500, $"y={ys[i]} 的档位低于频带下沿");
                Assert.LessOrEqual(order, 559, $"y={ys[i]} 的档位高于频带上沿");
            }
        }

        [Test]
        public void 排序_同一档内给出同一个档位()
        {
            // 每单位 4 档 ⇒ 档宽 0.25 米：差 0.1 米应当落在同一档里。
            Assert.AreEqual(
                YSort.OrderFor(1.0f, 500, 559, 4f),
                YSort.OrderFor(1.1f, 500, 559, 4f),
                "0.25 米一档：差 0.1 米不该换档（换了会让相邻两人每帧互换前后）");
        }

        [Test]
        public void 排序_非法参数退化为下沿而不是抛异常()
        {
            Assert.AreEqual(500, YSort.OrderFor(0f, 500, 559, 0f), "每单位 0 档 ⇒ 退化为下沿");
            Assert.AreEqual(500, YSort.OrderFor(0f, 500, 559, float.NaN), "非数档数 ⇒ 退化为下沿");
            Assert.AreEqual(500, YSort.OrderFor(float.NaN, 500, 559, 4f), "非数 y ⇒ 退化为下沿（不参与遮挡）");
        }

        [Test]
        public void 排序_频带两端写反也能用()
        {
            Assert.AreEqual(
                YSort.OrderFor(0f, 500, 559, 4f),
                YSort.OrderFor(0f, 559, 500, 4f),
                "频带两端写反时应当自行交换，而不是给出一个带外的档位");
        }

        [Test]
        public void 排序_角色与球共用同一条频带且贴地件在频带之下()
        {
            Assert.AreEqual(RenderOrder.ActorOrder(1f), RenderOrder.BallOrder(1f), "球与角色走同一条档位来源");
            Assert.GreaterOrEqual(RenderOrder.ActorOrder(0f), RenderOrder.YSortBandStart);
            Assert.LessOrEqual(RenderOrder.ActorOrder(0f), RenderOrder.YSortBandEnd);

            Assert.Greater(RenderOrder.ActorOverlay, RenderOrder.YSortBandEnd,
                "头顶读数必须高于整个频带：它不该被邻居的身体盖住");
            Assert.Less(RenderOrder.GroundShadow, RenderOrder.YSortBandStart,
                "贴地阴影必须在频带之下：它不参与 Y-Sort");
            Assert.Less(RenderOrder.Aim, RenderOrder.YSortBandStart,
                "瞄准高亮是地面标记：它压在格效果之上、被站在那一格的人盖住");
        }
    }
}
