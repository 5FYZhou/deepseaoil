using DeepseaOil.Logic.Services.Time;
using DeepseaOil.Foundation;
using System;
using UnityEngine;

namespace DeepseaOil.Logic.Services
{
    /// <summary>
    /// 全局 Timer 服务。
    ///
    /// TimerManager 本身不依赖 Unity。
    /// 每帧由 GameRoot 驱动。
    /// </summary>
    public sealed class TimerManager : BaseManager<TimerManager>
    {
        private const float MAX_DELTA_TIME = 0.1f;

        private TimerWheel _scaledWheel;
        private TimerWheel _unscaledWheel;

        private TimerManager() { }

        public void Init(float slotDuration = 0.1f, int slotCount = 512)
        {
            _scaledWheel = new TimerWheel(slotDuration, slotCount);

            _unscaledWheel = new TimerWheel(slotDuration, slotCount);
        }

        /// <summary>
        /// 每帧推进 Timer。
        /// </summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (unscaledDeltaTime > 0.5f)
            {
                Debug.LogWarning(
                    $"[Timer] LARGE DELTA! " +
                    $"delta={deltaTime:F3}, " +
                    $"unscaled={unscaledDeltaTime:F3}");
            }
            deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);
            unscaledDeltaTime = Math.Min(unscaledDeltaTime, MAX_DELTA_TIME);

            _scaledWheel.Advance(deltaTime);
            _unscaledWheel.Advance(unscaledDeltaTime);
        }

        /// <summary>
        /// 创建游戏时间 Timer。
        /// </summary>
        public TimerHandle Schedule(float delay, Action callback)
        {
            return _scaledWheel.Schedule(TimerOptions.Once(delay), callback);
        }

        /// <summary>
        /// 创建非缩放 Timer。
        /// </summary>
        public TimerHandle ScheduleUnscaled(float delay, Action callback)
        {
            return _unscaledWheel.Schedule(
                TimerOptions.Once(delay),
                callback);
        }

        /// <summary>
        /// 创建循环 Timer。
        /// </summary>
        public TimerHandle ScheduleRepeating(float interval, Action callback)
        {
            return _scaledWheel.Schedule(
                TimerOptions.Loop(interval), callback);
        }

        /// <summary>
        /// 创建非缩放循环 Timer。
        /// </summary>
        public TimerHandle ScheduleRepeatingUnscaled(float interval, Action callback)
        {
            return _unscaledWheel.Schedule(
                TimerOptions.Loop(interval), callback);
        }

        /// <summary>
        /// 取消 Timer。
        /// </summary>
        public bool Cancel(TimerHandle handle)
        {
            return  _scaledWheel.Cancel(handle) || _unscaledWheel.Cancel(handle);
        }

        /// <summary>
        /// 清空所有 Timer。
        /// </summary>
        public void Clear()
        {
            _scaledWheel.Clear();
            _unscaledWheel.Clear();
        }

        public bool TryGetInfo(TimerHandle handle, out TimerInfo info)
        {
            if (_scaledWheel.TryGetInfo(handle, out info))
                return true;

            if (_unscaledWheel.TryGetInfo(handle, out info))
                return true;

            info = default;
            return false;
        }
    }
}
