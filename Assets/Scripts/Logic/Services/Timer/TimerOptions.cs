namespace DeepseaOil.Logic.Services.Time
{
    public readonly struct TimerOptions
    {
        public readonly float Delay;

        public readonly bool Repeat;

        /// <summary>循环间隔，Repeat=false 时忽略</summary>
        public readonly float Interval;

        public TimerOptions(float delay, bool repeat = false, float interval = 0f)
        {
            Delay = delay;
            Repeat = repeat;
            Interval = interval;
        }

        public static TimerOptions Once(float delay)
        {
            return new TimerOptions(delay, repeat: false, interval: 0f);
        }

        /// <summary>构造循环 Timer</summary>
        public static TimerOptions Loop(float interval = 0)
        {
            return new TimerOptions(interval, repeat: true, interval);
        }
    }

    public readonly struct TimerInfo
    {
        public float Duration { get; }
        public float ElapsedTime { get; }
        public float RemainingTime { get; }
        public float Progress { get; }
        public bool IsRepeating { get; }

        public TimerInfo(float duration, float elapsedTime, float remainingTime, float progress, bool isRepeating)
        {
            Duration = duration;
            ElapsedTime = elapsedTime;
            RemainingTime = remainingTime;
            Progress = progress;
            IsRepeating = isRepeating;
        }
    }

}
