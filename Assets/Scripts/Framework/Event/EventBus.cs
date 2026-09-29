using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSeaOil.Logic.Events
{
    /// <summary>
    /// 单个事件类型 <typeparamref name="T"/> 的订阅通道。
    /// </summary>
    /// <remarks>
    /// <para><b>事件类型是 struct 值类型</b>：按值传递，发布方与订阅方不共享堆引用，避免内存滞留与篡改。</para>
    /// </remarks>
    internal sealed class EventChannel<T> where T : struct
    {
        // M0按需扩容比预分配更省内存。
        private const int InitialCapacity = 4;
        private Action<T>[] _handlers = new Action<T>[InitialCapacity];
        private int _writeIndex;

        /// <summary>当前有效订阅者数量。可用于测试断言与订阅泄漏观测。</summary>
        public int HandlerCount => _writeIndex;

        /// <summary>订阅，幂等。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public void Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (IndexOf(handler) >= 0) return; // 幂等：已存在则直接返回
            if (_writeIndex >= _handlers.Length) Array.Resize(ref _handlers, _handlers.Length * 2);

            _handlers[_writeIndex] = handler;
            _writeIndex++;
        }

        /// <summary>退订，幂等。</summary>
        public void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;

            int index = IndexOf(handler);
            if (index < 0) return;

            int last = _writeIndex - 1;

            // 尾部填充法
            _handlers[index] = _handlers[last];
            _handlers[last] = null;

            _writeIndex = last;
        }

        /// <summary>清空全部订阅。</summary>
        public void Clear()
        {
            Array.Clear(_handlers, 0, _handlers.Length);
            _writeIndex = 0;
        }

        /// <summary>
        /// 发布事件。
        /// </summary>
        /// <remarks>
        /// 遍历使用快照：发布过程中增删的订阅者不会收到本次事件
        /// </remarks>
        public void Publish(in T evt)
        {
            int count = _writeIndex;
            if (count == 0) return;

            // 快照（注意：有GC消耗）
            var snapshot = new Action<T>[count];
            Array.Copy(_handlers, snapshot, count);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    snapshot[i]?.Invoke(evt);
                }
                catch (Exception ex)
                {
                    // 异常隔离
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>查找委托，未找到返回 -1。</summary>
        private int IndexOf(Action<T> handler)
        {
            for (int i = 0; i < _handlers.Length; i++)
            {
                if (_handlers[i] == handler) return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// 静态泛型事件总线：按事件类型 <typeparamref name="T"/> 分流订阅与发布。
    /// </summary>
    /// <typeparam name="T">事件类型，struct 值类型。</typeparam>
    /// <example>
    /// <code>
    /// public readonly struct PlayerDied { public readonly int Lives; }
    ///
    /// EventBus&lt;PlayerDied&gt;.Subscribe(OnPlayerDied);
    /// EventBus&lt;PlayerDied&gt;.Publish(new PlayerDied(2));
    /// EventBus&lt;PlayerDied&gt;.Unsubscribe(OnPlayerDied);
    /// </code>
    /// </example>
    public static class EventBus<T> where T : struct
    {
        private static readonly EventChannel<T> Channel = new EventChannel<T>();

        /// <summary>当前有效订阅者数量。用于测试断言与订阅泄漏观测。</summary>
        public static int HandlerCount => Channel.HandlerCount;

        /// <summary>订阅事件，幂等m/summary>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public static void Subscribe(Action<T> handler)
        {
            Channel.Subscribe(handler);
            EventChannelRegistry.Register(typeof(T));
        }

        /// <summary>退订事件，幂等</summary>
        public static void Unsubscribe(Action<T> handler)
        {
            Channel.Unsubscribe(handler);
        }

        /// <summary>发布事件，异常隔离/summary>
        public static void Publish(in T evt)
        {
            Channel.Publish(in evt);
        }

        /// <summary>
        /// 清空本事件类型的全部订阅（测试与场景收尾用，<b>不</b>自动调用）。
        /// </summary>
        /// <remarks>
        /// 无法在泛型基类中遍历所有封闭类型，因此按类型逐个清理。
        /// </remarks>
        public static void Clear()
        {
            Channel.Clear();
        }
    }

    /// <summary>
    /// 非泛型事件总线门面，跨事件类型统一入口。
    /// </summary>
    /// <remarks>
    /// <para><b>M0</b>：<see cref="ClearAll"/>为预留接口，M0阶段无调用方、非验收项。</para>
    /// <para><b>用途</b>：<see cref="EventBus{T}"/>静态字段按封闭类型隔离，非泛型代码无法遍历；
    /// 由<see cref="EventChannelRegistry"/>注册事件类型，
    /// 重置时反射调用<c>EventBus&lt;T&gt;.Clear()</c>，
    /// 省去各类型单独编码。</para>
    /// <para><b>限制</b>：不添加<c>[RuntimeInitializeOnLoadMethod]</c>自动清理。
    /// 当前开启Domain Reload，进入Play模式静态状态自动重置；后续关闭Domain Reload提速时再补充。</para>
    /// </remarks>
    public static class EventBus
    {
        /// <summary>清空所有已知事件类型的订阅。</summary>
        public static void ClearAll()
        {
            EventChannelRegistry.ClearAll();
        }
    }

    /// <summary>
    /// 封闭事件类型登记表。
    /// </summary>
    /// <remarks>仅在订阅发生时写入；发布路径<b>不</b>触碰本表，无每帧开销。</remarks>
    internal static class EventChannelRegistry
    {
        /// <summary>
        /// 仅用于在 <c>nameof</c> 中取得 <c>EventBus&lt;T&gt;.Clear</c> 的方法名。
        /// </summary>
        /// <remarks>
        /// 不能写 <c>nameof(EventBus&lt;object&gt;.Clear)</c>：<c>EventBus&lt;T&gt;</c> 有 <c>where T : struct</c> 约束，
        /// 用 <c>object</c> 实例化会触发 <c>CS0453</c>（object 不是值类型）。
        /// 本类型不参与运行期逻辑，仅作为满足约束的类型占位。
        /// </remarks>
        private readonly struct ClearMethodProbe
        {
        }

        private const int InitialCapacity = 16;
        private static readonly Dictionary<Type, Action> ClearActions = new(InitialCapacity);

        /// <summary>登记事件类型，幂等</summary>
        public static void Register(Type eventType)
        {
            if (ClearActions.ContainsKey(eventType)) return;

            ClearActions.Add(eventType, () => ClearByType(eventType));
        }

        /// <summary>清空所有已登记事件类型的订阅。</summary>
        public static void ClearAll()
        {
            // 先复制键再清理：清理动作本身不改动正在遍历的集合。
            var types = new List<Type>(ClearActions.Keys);
            foreach (Type type in types)
            {
                ClearByType(type);
            }
        }

        /// <summary>通过反射定位并调用封闭类型的 <c>EventBus&lt;T&gt;.Clear()</c>。</summary>
        private static void ClearByType(Type eventType)
        {
            System.Reflection.MethodInfo method = typeof(EventBus<>)
                .MakeGenericType(eventType)
                .GetMethod(nameof(EventBus<ClearMethodProbe>.Clear), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                Debug.LogWarning($"[EventBus] 未能定位 {eventType.Name} 的 Clear 方法，跳过清理。");
                return;
            }

            method.Invoke(null, null);
        }
    }
}
