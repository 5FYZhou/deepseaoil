using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    public enum InputType
    {
        Dash,
        Attack, // M2
    }

    // 输入缓冲：窗口为闭区间 now - window <= t <= now；时间由调用方传入（不读 Unity Time），必须单调不减。
    // 同一次按下只能消费一次；按下仅受窗口时长约束，不随样本被挤出历史而作废。
    // 输入按下沿一律由本类产生（CanConsume / TryConsume）：宿主不得自行保存"上一帧输入"，且 Push 必须先于逻辑层的 Tick。当前只有 Dash 有消费者。
    public sealed class InputBuffer
    {
        // "该输入已失效"的哨兵值；NaN 的传播特性使无效输入在窗口判定里自然判假。
        private const float InvalidTime = float.NaN;

        private readonly InputSnapshot[] _snapshots;

        private int _writeIndex;

        private int _latestIndex => (_writeIndex - 1 + Capacity) % Capacity;

        public int Capacity => _snapshots.Length;

        public float RequestedSeconds { get; }

        // 应与实际调用 Push 的频率一致：不一致会让 EffectiveSeconds 名不副实。
        public int SampleRatePerSecond { get; }

        private readonly Dictionary<InputType, float> _pendingTimes;

        private readonly InputType[] _inputTypes;

        /// <summary>实际生效的历史窗口时长（秒），调手感时应以本值为准。</summary>
        public float EffectiveSeconds => Capacity / (float)SampleRatePerSecond;

        /// <param name="bufferSeconds">需要保留的历史时长（秒），为 0 时存一帧。</param>
        public InputBuffer(float bufferSeconds, int sampleRatePerSecond)
        {
            if (float.IsNaN(bufferSeconds) || float.IsInfinity(bufferSeconds) || bufferSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bufferSeconds), bufferSeconds, "缓冲时长必须为非负值。"
                    );
            }

            if (sampleRatePerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRatePerSecond), sampleRatePerSecond, "采样率必须大于 0。"
                    );
            }

            SampleRatePerSecond = sampleRatePerSecond;
            RequestedSeconds = bufferSeconds;

            int capacity = Mathf.Max(1, Mathf.CeilToInt(bufferSeconds * sampleRatePerSecond));

            _snapshots = new InputSnapshot[capacity];
            _writeIndex = 0;

            var inputTypes = (InputType[])Enum.GetValues(typeof(InputType));
            _inputTypes = inputTypes;
            _pendingTimes = new(inputTypes.Length);
            foreach (InputType type in inputTypes)
            {
                _pendingTimes[type] = InvalidTime;
            }
        }

        // 帧序：必须先于逻辑层的 Tick 调，缓冲区满时覆盖最旧样本；只把"有消费者"的按下沿入账。
        // 松开沿（曾供可变跳高用）已随跳跃曲线删除；将来要松开沿，就地按相邻两次采样求，不能挪到渲染帧求 —— 一个渲染帧对应 0 或 2 个物理帧时会丢沿。
        public void Push(in InputSnapshot snapshot, float now)
        {
            _snapshots[_writeIndex++] = snapshot;
            _writeIndex %= Capacity;

            if (snapshot.DashPressed) _pendingTimes[InputType.Dash] = now;
        }

        /// <summary>查询窗口内是否有未消费的按下。<b>不</b>消费，可重复调用。</summary>
        public bool CanConsume(InputType type, float now, float window)
            => IsInWindow(_pendingTimes[type], now, window);

        /// <returns>消费成功返回 true，并作废缓存。</returns>
        public bool TryConsume(InputType type, float now, float window)
        {
            if (!CanConsume(type, now, window)) return false;

            _pendingTimes[type] = InvalidTime;
            return true;
        }

        public Vector2 GetMove() => _snapshots[_latestIndex].Move;

        public void Clear()
        {
            Array.Clear(_snapshots, 0, _snapshots.Length);
            _writeIndex = 0;

            foreach (InputType type in _inputTypes)
            {
                _pendingTimes[type] = InvalidTime;
            }
        }

        private static bool IsInWindow(float time, float now, float window)
        {
            return time <= now && time >= now - window;
        }
    }
}
