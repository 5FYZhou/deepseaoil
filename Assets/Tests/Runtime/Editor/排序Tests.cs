// ---------------------------------------------------------------------------
// 表现层排序 · Y-Sort 行为测试
//
// 【为什么单独一个文件】它是工程里唯一一条"表现层数学"，与战斗、移动都无关。
//
// 【留什么 · 砍什么】判据只有一条：这条用例守的是不是「改错了不报错、只表现为观感不对」。
//   留：单调（越靠下越晚画）、钳制（档位始终在频带内，含 NaN）、量化（0.25 米一档，
//       相邻两人不会每帧互换前后）、非法参数退化（每单位 0 档 / 非数档数 → 下沿，不抛）。
//   砍：原「角色与球共用同一条频带且贴地件在频带之下」—— 它是**常量对常量**的断言
//       （RenderOrder 的静态档位比大小）＋ 一条转发（ActorOrder ≡ BallOrder），
//       改错了不会静默，只会编译期就换个常量。
//
// 【只测纯函数】YSort.OrderFor 不碰引擎对象，所以能逐条钉住；
//   真正"谁盖住谁"的观感只能人工 Play 看（两个角色站在不同 y 上）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Foundation;
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
        }
    }
}
