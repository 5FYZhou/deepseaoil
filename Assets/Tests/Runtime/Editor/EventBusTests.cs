using System;
using DeepseaOil.Logic.Events;
using NUnit.Framework;

namespace DeepseaOil.Tests
{
    /// <summary>事件总线发布语义：热路径不分配，且发布中增删订阅者的口径不变</summary>
    public class EventBusTests
    {
        private struct ProbeEvent
        {
            public int Value;
        }

        private static int s_sink;

        private static int s_late;

        [TearDown]
        public void TearDown()
        {
            EventBus<ProbeEvent>.Clear();

            s_sink = 0;
            s_late = 0;
        }

        [Test]
        public void 发布一千次不产生托管分配()
        {
            EventBus<ProbeEvent>.Subscribe(OnProbe);

            EventBus<ProbeEvent>.Publish(new ProbeEvent { Value = 1 });

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++) EventBus<ProbeEvent>.Publish(new ProbeEvent { Value = 1 });

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Less(allocated, 1024L, $"发布 1000 次分配了 {allocated} 字节：快照数组没被复用");
            Assert.AreEqual(1001, s_sink, "订阅者没收到全部事件");
        }

        [Test]
        public void 发布中新增的订阅者收不到本次事件()
        {
            Action<ProbeEvent> late = OnLate;
            Action<ProbeEvent> subscribeLate = null;
            subscribeLate = e => EventBus<ProbeEvent>.Subscribe(late);

            EventBus<ProbeEvent>.Subscribe(subscribeLate);

            EventBus<ProbeEvent>.Publish(default);

            Assert.AreEqual(0, s_late, "发布中新增的订阅者收到了同一次事件");

            EventBus<ProbeEvent>.Publish(default);

            Assert.AreEqual(1, s_late, "新增的订阅者在下一次发布没收到事件");
        }

        private static void OnProbe(ProbeEvent evt)
        {
            s_sink += evt.Value;
        }

        private static void OnLate(ProbeEvent evt)
        {
            s_late++;
        }
    }
}
