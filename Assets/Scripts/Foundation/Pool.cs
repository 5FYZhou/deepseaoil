using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 池空（空闲区为空）时的处理策略。
    /// </summary>
    /// <remarks>
    /// 注意区分「池空」与「达到上限」：本枚举描述的是**池空那一次请求**怎么处理，
    /// 只有 <see cref="CreateOrDrop"/> 会把 <see cref="Pool{T}.MaxSize"/> 当硬上限用。
    /// </remarks>
    public enum PoolOverflowPolicy
    {
        /// <summary>
        /// 池空时现场创建并 <c>LogWarning</c>。<b>不看 MaxSize</b>（旧 Pool 语义，默认值）——
        /// 此时 MaxSize 只作空闲保留上限。热路径请改用 <see cref="CreateOrDrop"/> 或先 Prewarm。
        /// </summary>
        CreateAndWarn = 0,

        /// <summary>
        /// 池空且「累计创建数 &lt; MaxSize」时现场创建（不警告）；
        /// 已达上限则<b>丢弃本次请求</b>（<see cref="Pool{T}.TryGet"/> 返回 false）并计数。
        /// 有上限的对象池（特效池）用这个。
        /// </summary>
        CreateOrDrop = 1,

        /// <summary>
        /// 池空即丢弃，不创建。只吃 Prewarm 出来的与已归还的对象——严格定容池用。
        /// </summary>
        DropSilently = 2,

        /// <summary>
        /// 池空即抛 <see cref="InvalidOperationException"/>。装配期错误用。
        /// </summary>
        Throw = 3,
    }

    /// <summary>池的只读快照。调用方：调试面板 / 观测代码。</summary>
    public readonly struct PoolStats
    {
        /// <summary>当前借出数。</summary>
        public readonly int Active;

        /// <summary>池中待用数。</summary>
        public readonly int Idle;

        /// <summary>历史峰值 Active。</summary>
        public readonly int Peak;

        /// <summary>累计创建数。</summary>
        public readonly int TotalCreated;

        /// <summary>累计被丢弃的请求数（TryGet 失败次数）。</summary>
        public readonly int TotalDropped;

        public PoolStats(int active, int idle, int peak, int totalCreated, int totalDropped)
        {
            Active = active;
            Idle = idle;
            Peak = peak;
            TotalCreated = totalCreated;
            TotalDropped = totalDropped;
        }

        public override string ToString()
            => $"active={Active} idle={Idle} peak={Peak} created={TotalCreated} dropped={TotalDropped}";
    }

    /// <summary>
    /// 通用对象池。
    /// </summary>
    /// <remarks>
    /// <para><b>语义</b></para>
    /// <list type="bullet">
    /// <item><see cref="TryGet"/>：空闲区有货就弹出一个；空了按 <see cref="PoolOverflowPolicy"/> 处理，返回是否借出成功。</item>
    /// <item><see cref="Get"/>：<see cref="TryGet"/> 的便利包装，失败返回 <c>null</c>（调用方需判空）。</item>
    /// <item><see cref="Release"/>：归还。空闲区已达 <see cref="MaxSize"/> 时不再保留，直接交 <c>onDestroy</c>（若有）。</item>
    /// <item><see cref="Prewarm"/>：<b>只造对象</b>，不触发 <c>onGet</c> / <c>onRelease</c>（旧实现调了 <c>onRelease</c>，语义是错的）。</item>
    /// <item><see cref="Clear"/>：清空<b>空闲区</b>，逐个回调 <c>onDestroy</c>；不影响已借出对象。</item>
    /// <item><see cref="Dispose"/>：<see cref="Clear"/> + 标记不可用；幂等。之后再调用任何取还接口都会抛 <see cref="ObjectDisposedException"/>。</item>
    /// </list>
    /// <para><b>MaxSize 的准确含义</b>：空闲区保留上限，同时也是 <see cref="PoolOverflowPolicy.CreateOrDrop"/> 下
    /// 「活跃 + 空闲」的总量上限（因为该策略只在累计创建数未达上限时创建）。</para>
    /// <para><b>兼容性</b>：构造参数沿用旧顺序 <c>(factory, onGet, onRelease)</c>，新增参数一律可选且排在后面；
    /// 旧调用方（<c>AudioManager</c>）行为不变。</para>
    /// <para><b>线程</b>：仅主线程。不做锁。</para>
    /// </remarks>
    public class Pool<T> : IDisposable where T : class
    {
        private readonly string _name;
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;
        private readonly PoolOverflowPolicy _overflowPolicy;

        private readonly Stack<T> _idle = new Stack<T>();

        private int _active;
        private int _peak;
        private int _totalCreated;
        private int _totalDropped;
        private bool _disposed;

        /// <summary>池名。只用于日志，构造时为空则取 <c>typeof(T).Name</c>。</summary>
        public string Name => _name;

        public int MaxSize => _maxSize;

        /// <summary>当前借出数。</summary>
        public int ActiveCount => _active;

        /// <summary>池中待用数。</summary>
        public int IdleCount => _idle.Count;

        /// <summary>兼容旧 API：等同于 <see cref="IdleCount"/>。</summary>
        public int Count => _idle.Count;

        /// <param name="factory">创建新对象。不能返回 null。</param>
        /// <param name="onGet">借出时回调（例如 <c>SetActive(true)</c>）。</param>
        /// <param name="onRelease">归还时回调（例如 <c>SetActive(false)</c>）。</param>
        /// <param name="name">池名，用于日志；为空取类型名。</param>
        /// <param name="maxSize">空闲保留上限 / CreateOrDrop 的总量上限。必须 &gt; 0。</param>
        /// <param name="overflowPolicy">池空策略。默认 <see cref="PoolOverflowPolicy.CreateAndWarn"/>（旧语义）。</param>
        /// <param name="onDestroy">对象被彻底丢弃时回调（例如 <c>Object.Destroy(go)</c>）。详见 <see cref="Release"/> 与 <see cref="Clear"/>。</param>
        public Pool(
            Func<T> factory,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            string name = null,
            int maxSize = int.MaxValue,
            PoolOverflowPolicy overflowPolicy = PoolOverflowPolicy.CreateAndWarn,
            Action<T> onDestroy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));

            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize 必须大于 0。");

            _name = string.IsNullOrEmpty(name) ? typeof(T).Name : name;
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = maxSize;
            _overflowPolicy = overflowPolicy;
        }

        /// <summary>借出一个对象。失败（被丢弃）时返回 false 且 <paramref name="obj"/> 为 null。</summary>
        /// <exception cref="ObjectDisposedException">已 Dispose。</exception>
        /// <exception cref="InvalidOperationException">池空且策略为 <see cref="PoolOverflowPolicy.Throw"/>。</exception>
        public bool TryGet(out T obj)
        {
            ThrowIfDisposed();
            obj = null;

            if (_idle.Count > 0)
            {
                obj = _idle.Pop();
                MarkActive(obj);
                return true;
            }

            switch (_overflowPolicy)
            {
                case PoolOverflowPolicy.CreateAndWarn:
                    Debug.LogWarning($"[Pool<{_name}>] 池为空，正在现场创建对象。" +
                                     $"如果这是热路径，请考虑 Prewarm() 或改用 CreateOrDrop。");
                    obj = CreateNew();
                    MarkActive(obj);
                    return true;

                case PoolOverflowPolicy.CreateOrDrop:
                    if (_totalCreated < _maxSize)
                    {
                        obj = CreateNew();
                        MarkActive(obj);
                        return true;
                    }

                    _totalDropped++;
                    return false;

                case PoolOverflowPolicy.DropSilently:
                    _totalDropped++;
                    return false;

                default: // Throw
                    _totalDropped++;
                    throw new InvalidOperationException(
                        $"[Pool<{_name}>] 池为空且策略为 Throw（MaxSize={_maxSize}）。");

            }
        }

        /// <summary>借出一个对象。池空且策略为丢弃时返回 null，调用方需判空。</summary>
        public T Get() => TryGet(out var obj) ? obj : null;

        /// <summary>
        /// 归还一个对象。
        /// </summary>
        /// <remarks>
        /// <paramref name="obj"/> 为 null（例如 GameObject 已被外部销毁）时只记警告并返回，不抛异常——
        /// 池位于帧循环里，抛异常会连带炸掉整帧。
        /// </remarks>
        public void Release(T obj)
        {
            ThrowIfDisposed();

            if (obj == null)
            {
                Debug.LogWarning($"[Pool<{_name}>] Release(null) 被忽略。");
                return;
            }

            _onRelease?.Invoke(obj);

            _active--;
            if (_active < 0)
            {
                Debug.LogError($"[Pool<{_name}>] Active 计数小于 0：Release 次数多于 Get。已复位为 0。");
                _active = 0;
            }

            if (_idle.Count >= _maxSize)
            {
                // 空闲区已满：不再保留，交给 onDestroy 销毁（GameObject 池靠这条兜住不增长）
                _onDestroy?.Invoke(obj);
                return;
            }

            _idle.Push(obj);
        }

        /// <summary>预创建 <paramref name="count"/> 个对象放进空闲区。不触发 onGet / onRelease。</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负。</exception>
        public void Prewarm(int count)
        {
            ThrowIfDisposed();

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            int create = Math.Min(count, Math.Max(0, _maxSize - _idle.Count));
            for (int i = 0; i < create; i++)
            {
                _idle.Push(CreateNew());
            }
        }

        /// <summary>清空空闲区。已借出的对象不受影响。有 onDestroy 时逐个回调。</summary>
        public void Clear()
        {
            if (_onDestroy != null)
            {
                foreach (var obj in _idle)
                {
                    _onDestroy(obj);
                }
            }

            _idle.Clear();
        }

        public PoolStats GetStats()
            => new PoolStats(_active, _idle.Count, _peak, _totalCreated, _totalDropped);

        /// <summary>清空空闲区并标记不可用。幂等。</summary>
        public void Dispose()
        {
            if (_disposed) return;

            Clear();
            _disposed = true;
        }

        private void MarkActive(T obj)
        {
            _active++;
            if (_active > _peak) _peak = _active;

            _onGet?.Invoke(obj);
        }

        private T CreateNew()
        {
            _totalCreated++;

            T obj = _factory();
            if (obj == null)
            {
                Debug.LogError($"[Pool<{_name}>] factory 返回了 null，池的计数会失真。");
            }

            return obj;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException($"[Pool<{_name}>]");
        }
    }

    /// <summary>引用类型池的旧别名。签名保持不变，仅供既有调用方使用。</summary>
    public class PoolInClass<T> : Pool<T> where T : class
    {
        public PoolInClass(
            Func<T> factory,
            Action<T> onGet = null,
            Action<T> onRelease = null) : base(factory, onGet, onRelease)
        {
        }
    }

    /// <summary>MonoBehaviour 池的旧别名。签名保持不变，仅供既有调用方使用。</summary>
    public class PoolInMono<T> : Pool<T> where T : MonoBehaviour
    {
        public PoolInMono(
            Func<T> factory,
            Action<T> onGet = null,
            Action<T> onRelease = null) : base(factory, onGet, onRelease)
        {
        }
    }
}
