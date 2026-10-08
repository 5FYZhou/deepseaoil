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

    // 输入缓冲：窗口闭区间 now-window <= t <= now；时间由调用方传入须单调不减。
    // 按下沿由本类产生：宿主不得自存"上一帧输入"，Push 必须先于逻辑层 Tick。
    public sealed class InputBuffer
    {
        // "已失效"哨兵值，NaN 在窗口判定里判假。
        private const float InvalidTime = float.NaN;

        private readonly InputSnapshot[] _snapshots;

        private int _writeIndex;

        private int _latestIndex => (_writeIndex - 1 + Capacity) % Capacity;

        public int Capacity => _snapshots.Length;

        public float RequestedSeconds { get; }

        public int SampleRatePerSecond { get; }

        private readonly Dictionary<InputType, float> _pendingTimes;

        private readonly InputType[] _inputTypes;

        /// <summary>实际生效的历史窗口时长（秒）</summary>
        public float EffectiveSeconds => Capacity / (float)SampleRatePerSecond;

        // bufferSeconds 为秒；0 时存一帧
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

        // 帧序：先于逻辑层 Tick 调，缓冲区满时覆盖最旧样本；按下沿不能挪到渲染帧求（一渲染帧对应 0 或 2 个物理帧会丢沿）。
        public void Push(in InputSnapshot snapshot, float now)
        {
            _snapshots[_writeIndex++] = snapshot;
            _writeIndex %= Capacity;

            if (snapshot.DashPressed) _pendingTimes[InputType.Dash] = now;
        }

        /// <summary>查询窗口内有无未消费的按下，不消费，可重复调</summary>
        public bool CanConsume(InputType type, float now, float window)
            => IsInWindow(_pendingTimes[type], now, window);

        // 消费成功返回 true，并作废缓存。
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
