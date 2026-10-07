using System;
using System.Collections.Generic;

namespace DeepseaOil.Logic.Services.Time
{
    /// <summary>
    /// 基于时间槽的时间轮。
    ///
    /// TimerWheel 不依赖 Unity。
    /// 外部通过 Advance(deltaTime) 推进时间。
    ///
    /// 一个 TimerWheel 只对应一条时间轴。
    /// Scaled / Unscaled 应该由外部 TimerService
    /// 分别持有不同的 TimerWheel。
    /// </summary>
    public sealed class TimerWheel
    {
        private sealed class TimerNode
        {
            public TimerHandle Handle;

            /// <summary>
            /// Timer 当前所在的目标槽。
            /// </summary>
            public int TargetSlot;

            /// <summary>
            /// 跨越完整时间轮的剩余圈数。
            /// </summary>
            ///<remarks>
            /// 例如：
            /// WheelDuration = 10s, Timer 延迟 = 25s
            /// 第一次到达 TargetSlot 时：RemainingRounds = 2
            ///每经过一次 TargetSlot：RemainingRounds--
            /// 当 RemainingRounds == 0 时，
            /// 下一次到达 TargetSlot 才真正检查 ExpireTime。
            /// </remarks>
            public int RemainingRounds;

            /// <summary>
            /// Timer 是否循环。
            /// </summary>
            public bool Repeat;

            /// <summary>
            /// 循环间隔。
            /// </summary>
            public float Interval;

            /// <summary>
            /// 当前周期开始时间。
            /// </summary>
            public double PeriodStartTime;

            /// <summary>
            /// 当前周期结束时间。
            /// </summary>
            public double ExpireTime;

            /// <summary>
            /// 到期时执行。
            /// </summary>
            public Action Callback;

            /// <summary>
            /// 是否已经取消。
            /// </summary>
            public bool Cancelled;
        }

        private readonly List<TimerNode>[] _slots;

        private readonly Dictionary<int, TimerNode> _timers = new();

        /// <summary>
        /// 每个时间槽代表多少秒。
        /// </summary>
        private readonly float _slotDuration;

        /// <summary>
        /// 时间槽数量。
        /// </summary>
        private readonly int _slotCount;

        /// <summary>
        /// 当前所在的时间槽。
        /// </summary>
        private int _currentSlot;

        /// <summary>
        /// 下一个时间槽的绝对时间。
        /// </summary>
        private double _nextSlotTime;

        /// <summary>
        /// 当前时间轴上的时间。
        /// </summary>
        private double _time;

        /// <summary>
        /// 下一个 Timer ID。
        /// </summary>
        private int _nextId = 1;

        /// <summary>
        /// 时间轮总时长。
        /// </summary>
        public float WheelDuration => _slotDuration * _slotCount;

        /// <summary>
        /// 当前时间。
        /// </summary>
        public double Time => _time;

        /// <summary>
        /// 当前 Timer 数量。
        /// </summary>
        public int Count => _timers.Count;

        public TimerWheel(float slotDuration = 0.1f, int slotCount = 512)
        {
            if (slotDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(slotDuration));

            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            _slotDuration = slotDuration;
            _slotCount = slotCount;

            _slots = new List<TimerNode>[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new List<TimerNode>();
            }

            _currentSlot = 0;

            // 当前时间为 0。
            // 第一次切换槽位应该发生在 slotDuration。
            _nextSlotTime = _slotDuration;

            _time = 0d;
        }

        /// <summary>
        /// 创建 Timer。
        /// </summary>
        public TimerHandle Schedule(TimerOptions options, Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (options.Delay < 0f)
                throw new ArgumentOutOfRangeException(nameof(options.Delay));

            if (options.Repeat && options.Interval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(options.Interval));

            int id = CreateId();

            TimerHandle handle = new(id);

            TimerNode node = new()
            {
                Handle = handle,
                TargetSlot = 0,
                RemainingRounds = 0,
                Repeat = options.Repeat,
                Interval = options.Repeat ? options.Interval : 0f,
                Callback = callback,
                Cancelled = false,
                ExpireTime = _time + options.Delay,
                PeriodStartTime = _time
            };

            _timers.Add(id, node);

            AddToWheel(node);

            return handle;
        }

        /// <summary>
        /// 推进时间轮。
        ///
        /// 一个 Unity Frame 可能跨越多个时间槽，
        /// 因此这里使用 while 逐槽处理。
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _time += deltaTime;

            while (_time >= _nextSlotTime)
            {
                _currentSlot++;

                if (_currentSlot >= _slotCount)
                {
                    _currentSlot = 0;
                }

                ProcessSlot(_currentSlot);

                _nextSlotTime += _slotDuration;
            }
        }

        /// <summary>
        /// 处理一个时间槽。
        /// </summary>
        private void ProcessSlot(int slotIndex)
        {
            List<TimerNode> slot = _slots[slotIndex];

            if (slot.Count == 0)
            {
                return;
            }

            /*
             * 清空原列表。
             *
             * Timer 回调内部可以创建新的 Timer，
             * 不会修改当前正在遍历的列表。
             */
            TimerNode[] nodes = slot.ToArray();

            slot.Clear();

            for (int i = 0; i < nodes.Length; i++)
            {
                TimerNode node = nodes[i];

                if (node.Cancelled)
                {
                    continue;
                }

                /*
                 * 还没有完成完整的一圈。
                 *
                 * 当前只是经过了这个 Timer 的目标槽，
                 * 但 Timer 还没有到执行时间。
                 *
                 * 注意：
                 * 这里不能调用 AddToWheel()。
                 *
                 * 因为 AddToWheel() 会重新计算
                 * RemainingRounds。
                 */
                if (node.RemainingRounds > 0)
                {
                    node.RemainingRounds--;

                    Requeue(node);

                    continue;
                }

                /*
                 * 已经没有剩余圈数。
                 *
                 * 现在才真正检查绝对时间。
                 */
                if (_time < node.ExpireTime)
                {
                    /*
                     * 理论上这里主要用于处理时间槽粒度
                     * 导致的提前进入。
                     *
                     * 仍然放回原来的目标槽。
                     */
                    Requeue(node);

                    continue;
                }

                Execute(node);
            }
        }

        /// <summary>
        /// 执行 Timer。
        /// </summary>
        private void Execute(TimerNode node)
        {
            if (node.Cancelled)
            {
                return;
            }

            /*
             * 单次 Timer：
             *
             * 回调执行之前从管理表移除。
             *
             * 这样 callback 内再次调用
             * Exists(handle) 时会得到 false。
             */
            if (!node.Repeat)
            {
                _timers.Remove(node.Handle.Id);
            }

            Action callback = node.Callback;

            try
            {
                callback?.Invoke();
            }
            finally
            {
                if (!node.Repeat)
                {
                    node.Callback = null;
                }
            }

            if (node.Cancelled)
            {
                return;
            }

            /*
             * 循环 Timer。
             *
             * 如果 callback 内取消了 Timer，
             * Cancelled 会阻止它重新加入时间轮。
             */
            if (node.Repeat && _timers.ContainsKey(node.Handle.Id))
            {
                node.PeriodStartTime = node.ExpireTime;
                node.ExpireTime += node.Interval;

                /*
                 * 如果一帧跨过了多个周期，
                 * 不在同一帧补执行大量次数。
                 *
                 * 直接把下一次执行时间推到当前时间之后。
                 */
                if (node.ExpireTime <= _time)
                {
                    node.PeriodStartTime = _time;
                    node.ExpireTime = _time + node.Interval;
                }

                /*
                 * 循环 Timer 是一个全新的执行周期，
                 * 因此重新计算：
                 *
                 * TargetSlot
                 * RemainingRounds
                 */
                AddToWheel(node);
            }
        }

        /// <summary>
        /// 第一次加入时间轮， 或循环 Timer 开始下一次周期时，
        /// 根据 ExpireTime 重新计算： TargetSlot, RemainingRounds
        /// </summary>
        private void AddToWheel(TimerNode node)
        {
            double remaining = node.ExpireTime - _time;

            if (remaining < 0d)
            {
                remaining = 0d;
            }

            /*
             * 向上取整：
             *
             * remaining = 0.01
             * slotDuration = 0.1
             *
             * ticks = 1
             *
             * 至少等待一个时间槽。
             */
            int ticks = (int)Math.Ceiling(remaining / _slotDuration);

            if (ticks < 1)
            {
                ticks = 1;
            }

            /*
             * 当前槽 + ticks
             * 得到目标槽。
             */
            node.TargetSlot = (_currentSlot + ticks) % _slotCount;

            /*
             * 计算需要跨越多少个完整时间轮。
             *
             * 例如：
             *
             * slotCount = 512
             * ticks = 600
             *
             * RemainingRounds = 1
             */
            node.RemainingRounds = (ticks - 1) / _slotCount;

            _slots[node.TargetSlot].Add(node);
        }

        /// <summary>
        /// Timer 已经知道自己的目标槽。
        ///
        /// 这里只负责重新放回目标槽，
        /// 不重新计算 RemainingRounds。
        /// </summary>
        private void Requeue(TimerNode node)
        {
            _slots[node.TargetSlot].Add(node);
        }

        /// <summary>
        /// 清除所有 Timer。
        /// 同时重置时间轮。
        /// </summary>
        public void Clear()
        {
            _timers.Clear();

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
            }

            _currentSlot = 0;
            _nextSlotTime = _slotDuration;
            _time = 0d;
            _nextId = 1;
        }


        /// <summary>
        /// 生成新的 Timer ID。
        /// </summary>
        private int CreateId()
        {
            int id = _nextId++;

            if (_nextId == int.MaxValue)
            {
                _nextId = 1;
            }

            return id;
        }

        /// <summary>
        /// 取消 Timer。
        /// </summary>
        public bool Cancel(TimerHandle handle)
        {
            if (!handle.IsValid)
            {
                return false;
            }

            if (!_timers.TryGetValue(handle.Id, out TimerNode node))
            {
                return false;
            }

            node.Cancelled = true;

            _timers.Remove(handle.Id);

            return true;
        }

        /// <summary>
        /// 判断 Timer 是否存在。
        /// </summary>
        public bool Exists(TimerHandle handle)
        {
            return handle.IsValid &&
                   _timers.ContainsKey(handle.Id);
        }

        public bool TryGetInfo(TimerHandle handle, out TimerInfo info)
        {
            if (!_timers.TryGetValue(handle.Id, out TimerNode node))
            {
                info = default;
                return false;
            }

            double elapsed = _time - node.PeriodStartTime;

            double remaining = node.ExpireTime - _time;

            if (remaining < 0)
                remaining = 0;

            double duration = node.ExpireTime - node.PeriodStartTime;

            float progress = duration <= 0 ? 1f : (float)(elapsed / duration);

            progress = Math.Clamp(progress, 0f, 1f);

            info = new TimerInfo(
                duration: (float)duration,
                elapsedTime: (float)Math.Max(0, elapsed),
                remainingTime: (float)remaining,
                progress: progress,
                isRepeating: node.Repeat);

            return true;
        }
    }
}