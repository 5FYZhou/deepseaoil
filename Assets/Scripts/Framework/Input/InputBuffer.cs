using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    public enum InputType
    {
        Jump,
        Dash,
        Attack, // M2
    }

    /// <summary>
    /// 输入缓冲：缓存近期采样输入快照，提供窗口内的按下查询与消费。
    /// </summary>
    /// <remarks>
    /// 时间由调用方传入（不读 Unity Time），必须单调不减；窗口为闭区间 <c>now - window &lt;= t &lt;= now</c>。
    /// 同一次按下只能消费一次；按下仅受窗口时长约束，不随样本被挤出历史而作废。
    /// <b>输入边沿一律由本类提供</b>（按下：<see cref="CanConsume"/>；松开：<see cref="IsJumpReleased"/>），
    /// 宿主不得自行保存"上一帧输入"，且 <see cref="Push"/> 必须先于逻辑层的 <c>Tick</c>。
    /// 契约与设计理由见 <c>Docs/架构约束.md</c> 与 <c>Docs/M1微规划.md</c> §三 D14。
    /// </remarks>
    public sealed class InputBuffer
    {
        /// <summary>表示"该输入已失效"的哨兵值。</summary>
        private const float InvalidTime = float.NaN;

        /// <summary>样本环形缓冲。</summary>
        private readonly InputSnapshot[] _snapshots;

        /// <summary>环形缓冲的写入位置。</summary>
        private int _writeIndex;

        /// <summary>环形缓冲的最新位置。</summary>
        private int _latestIndex => (_writeIndex - 1 + Capacity) % Capacity;

        /// <summary>环形缓冲容量（样本数）。</summary>
        public int Capacity => _snapshots.Length;

        /// <summary>请求的缓冲时长（秒）。</summary>
        public float RequestedSeconds { get; }

        /// <summary>每秒采样次数。</summary>
        public int SampleRatePerSecond { get; }

        /// <summary>最近一次未被消费的按下时间；<see cref="InvalidTime"/> 表示无。</summary>
        private readonly Dictionary<InputType, float> _pendingTimes;

        /// <summary>全部输入类型，构造时缓存：既省一次枚举分配，也避免遍历字典时再写字典。</summary>
        private readonly InputType[] _inputTypes;

        /// <summary>上一物理帧跳跃键是否按住。</summary>
        private bool _prevJumpHeld;

        /// <summary>本物理帧是否发生"按住 → 松开"。</summary>
        private bool _jumpReleased;

        /// <summary>实际生效的历史窗口时长（秒），调手感时应以本值为准。</summary>
        public float EffectiveSeconds => Capacity / (float)SampleRatePerSecond;

        /// <summary>
        /// 构造输入缓冲。
        /// </summary>
        /// <param name="bufferSeconds">需要保留的历史时长（秒），为 0 时存一帧。</param>
        /// <param name="sampleRatePerSecond">每秒采样次数，应与实际调用 <see cref="Push"/> 的频率一致。</param>
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

            // bufferSeconds 为 0 时保留"当前帧"这一格。
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

        /// <summary>
        /// 记录一帧输入快照。缓冲区满时覆盖最旧样本。
        /// </summary>
        public void Push(in InputSnapshot snapshot, float now)
        {
            _snapshots[_writeIndex++] = snapshot;
            _writeIndex %= Capacity;

            if (snapshot.JumpPressed) _pendingTimes[InputType.Jump] = now;
            if (snapshot.DashPressed) _pendingTimes[InputType.Dash] = now;

            // 松开沿在物理帧内就地由相邻两次采样求出：若挪到渲染帧（InputProvider.Update）
            // 求，一个渲染帧对应 0 或 2 个物理帧时会丢沿，可变跳高随之失效。
            _jumpReleased = _prevJumpHeld && !snapshot.JumpHeld;
            _prevJumpHeld = snapshot.JumpHeld;
        }

        /// <summary>
        /// 本物理帧是否为跳跃键"按住 → 松开"的边沿。由 <see cref="Push"/> 每帧刷新，纯查询、无副作用。
        /// </summary>
        /// <remarks>要求 <see cref="Push"/> 先于逻辑层的 <c>Tick</c> 调用，否则会滞后一帧。</remarks>
        public bool IsJumpReleased() => _jumpReleased;

        /// <summary>
        /// 查询窗口内是否有未消费的按下。<b>不</b>消费，可重复调用。
        /// </summary>
        public bool CanConsume(InputType type, float now, float window)
            => IsInWindow(_pendingTimes[type], now, window);

        /// <summary>
        /// 尝试消费一次缓冲。
        /// </summary>
        /// <returns>消费成功返回 true，并作废缓存。</returns>
        public bool TryConsume(InputType type, float now, float window)
        {
            if (!CanConsume(type, now, window)) return false;

            _pendingTimes[type] = InvalidTime;
            return true;
        }

        /// <summary>获取最新的移动数据。</summary>
        public Vector2 GetMove() => _snapshots[_latestIndex].Move;

        /// <summary>清空全部历史。</summary>
        public void Clear()
        {
            Array.Clear(_snapshots, 0, _snapshots.Length);
            _writeIndex = 0;
            _prevJumpHeld = false;
            _jumpReleased = false;

            foreach (InputType type in _inputTypes)
            {
                _pendingTimes[type] = InvalidTime;
            }
        }

        /// <summary>判断时间戳是否落在闭区间窗口内（NaN 的传播特性使无效输入自然判假）。</summary>
        private static bool IsInWindow(float time, float now, float window)
        {
            return time <= now && time >= now - window;
        }
    }
}
