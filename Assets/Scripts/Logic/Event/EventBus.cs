using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Events
{
    // EventChannel 泛型：单个事件类型的订阅通道。
    internal sealed class EventChannel<T> where T : struct
    {
        private const int InitialCapacity = 4;
        private Action<T>[] _handlers = new Action<T>[InitialCapacity];
        private int _writeIndex;

        // HandlerCount：当前有效订阅者数量。
        public int HandlerCount => _writeIndex;

        public void Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (IndexOf(handler) >= 0) return;
            if (_writeIndex >= _handlers.Length) Array.Resize(ref _handlers, _handlers.Length * 2);

            _handlers[_writeIndex] = handler;
            _writeIndex++;
        }

        // 退订：尾部填充法。
        public void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;

            int index = IndexOf(handler);
            if (index < 0) return;

            int last = _writeIndex - 1;

            _handlers[index] = _handlers[last];
            _handlers[last] = null;

            _writeIndex = last;
        }

        public void Clear()
        {
            Array.Clear(_handlers, 0, _handlers.Length);
            _writeIndex = 0;
        }

        /// <summary>发布事件。遍历快照（每次发布分配数组，有 GC 消耗）：发布中增删的订阅者收不到本次事件；订阅者异常被隔离，不打断其余订阅者。</summary>
        public void Publish(in T evt)
        {
            int count = _writeIndex;
            if (count == 0) return;

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
                    Debug.LogException(ex);
                }
            }
        }

        private int IndexOf(Action<T> handler)
        {
            for (int i = 0; i < _handlers.Length; i++)
            {
                if (_handlers[i] == handler) return i;
            }

            return -1;
        }
    }

    // EventBus 泛型：按事件类型分流订阅与发布。
    public static class EventBus<T> where T : struct
    {
        private static readonly EventChannel<T> Channel = new EventChannel<T>();

        public static int HandlerCount => Channel.HandlerCount;

        public static void Subscribe(Action<T> handler)
        {
            Channel.Subscribe(handler);
            EventChannelRegistry.Register(typeof(T));
        }

        public static void Unsubscribe(Action<T> handler)
        {
            Channel.Unsubscribe(handler);
        }

        public static void Publish(in T evt)
        {
            Channel.Publish(in evt);
        }

        // 清空本事件类型的全部订阅，不自动调用。
        public static void Clear()
        {
            Channel.Clear();
        }
    }

    // EventBus：非泛型门面。静态字段按封闭类型隔离，非泛型代码无法遍历，由 EventChannelRegistry 登记事件类型，重置时反射调用泛型 EventBus 的 Clear()。
    // ClearAll 当前无调用方。不加 [RuntimeInitializeOnLoadMethod] 自动清理：当前开启 Domain Reload，进 Play 模式静态状态自动重置。
    public static class EventBus
    {
        public static void ClearAll()
        {
            EventChannelRegistry.ClearAll();
        }
    }

    // 封闭事件类型登记表。仅订阅时写入，发布路径不触碰。
    internal static class EventChannelRegistry
    {
        private readonly struct ClearMethodProbe
        {
        }

        private const int InitialCapacity = 16;
        private static readonly Dictionary<Type, Action> ClearActions = new(InitialCapacity);

        public static void Register(Type eventType)
        {
            if (ClearActions.ContainsKey(eventType)) return;

            ClearActions.Add(eventType, () => ClearByType(eventType));
        }

        public static void ClearAll()
        {
            var types = new List<Type>(ClearActions.Keys);
            foreach (Type type in types)
            {
                ClearByType(type);
            }
        }

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
