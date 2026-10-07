using DeepseaOil.Logic.Services.Time;
using DeepseaOil.Logic.Service;
using System;
using UnityEngine;

namespace DeepseaOil.Logic.Services
{
    /// <summary>全局计时器（时间轮）：麻痹 / 冷却 / 倒计时这类"持续到某时刻"的东西排在这里。</summary>
    /// <remarks>
    /// 本身不依赖 Unity（除了超长帧的一条 Warning），每帧由 <c>GameRoot</c> 的服务表驱动 —— <b>不是单例</b>：参数走构造、生命周期归组合根（与 <c>UIMgr</c> / <c>AudioManager</c> 同一待遇）。
    /// <para><b>两个时间轴</b>：<c>Schedule*</c> 走缩放时间（暂停即停），<c>ScheduleUnscaled*</c> 走未缩放时间（暂停照走）。口径由驱动方给，本类不读 <c>Time</c>。</para>
    /// <para><b>不要用它做格子上的持续伤害</b>（§11 用法边界）：格子本身每帧在 Tick、天然有钟，给每个中毒格挂一个 timer 只会让格子数变成 timer 数。</para>
    /// </remarks>
    public sealed class TimerManager : IService
    {
        private const float MAX_DELTA_TIME = 0.1f;

        private readonly float _slotDuration;
        private readonly int _slotCount;

        private TimerWheel _scaledWheel;
        private TimerWheel _unscaledWheel;

        /// <param name="slotDuration">每槽时长（秒），默认 0.1。</param>
        /// <param name="slotCount">槽数，默认 512（0.1 × 512 = 51.2 秒一圈）。</param>
        public TimerManager(float slotDuration = 0.1f, int slotCount = 512)
        {
            _slotDuration = slotDuration;
            _slotCount = slotCount;
        }

        /// <summary>建两条时间轮。<c>IService.Init()</c> 保持无参：参数在构造时就给定了。</summary>
        public void Init()
        {
            _scaledWheel = new TimerWheel(_slotDuration, _slotCount);

            _unscaledWheel = new TimerWheel(_slotDuration, _slotCount);
        }

        /// <summary>
        /// 每帧推进 Timer。
        /// </summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (_scaledWheel == null) return;

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
        /// <remarks><b>已知边界</b>：两条轮各自从 1 开始发 id，而句柄只带 id 不带轮标记 —— "两轮恰好撞同一个 id"时会先取消 scaled 那条。
        /// 当前规模碰不到（麻痹 / 冷却各自只用一条轮），登记备查。</remarks>
        public bool Cancel(TimerHandle handle)
        {
            if (_scaledWheel == null) return false;

            return _scaledWheel.Cancel(handle) || _unscaledWheel.Cancel(handle);
        }

        /// <summary>
        /// 清空所有 Timer。
        /// </summary>
        public void Clear()
        {
            _scaledWheel?.Clear();
            _unscaledWheel?.Clear();
        }

        /// <summary>拆除：清空两条轮（<c>IService</c> 的收尾口，<c>GameRoot</c> 按服务表反序调）。</summary>
        public void Dispose()
        {
            Clear();
        }

        public bool TryGetInfo(TimerHandle handle, out TimerInfo info)
        {
            if (_scaledWheel != null && _scaledWheel.TryGetInfo(handle, out info))
                return true;

            if (_unscaledWheel != null && _unscaledWheel.TryGetInfo(handle, out info))
                return true;

            info = default;
            return false;
        }

        /// <summary>当前登记在两个轮里的 timer 数（诊断用）。</summary>
        public int Count => (_scaledWheel?.Count ?? 0) + (_unscaledWheel?.Count ?? 0);
    }
}
