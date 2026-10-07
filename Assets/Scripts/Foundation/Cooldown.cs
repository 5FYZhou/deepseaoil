namespace DeepseaOil.Foundation
{
    /// <summary>通用冷却件：只回答"现在能不能用"，不读时间、不做输入缓冲。</summary>
    // 投掷与冲刺共用一个口径：各写一份不报错，只表现为两个技能的手感莫名其妙地不同。
    // 存"绝对时刻"而不是"剩余秒数"：暂停时不需要累减，也不受 timeScale = 0 影响。
    // 窗口那一半条件属于 InputBuffer（它有采样率、有容量、会被清空）。
    public sealed class Cooldown
    {
        // 初始为负无穷：开局即可用，不需要"是不是第一次"判断。
        private float _nextAllowedTime = float.NegativeInfinity;

        // 纯查询：可重复调用、无副作用。
        public bool CanUse(float now)
        {
            return now >= _nextAllowedTime;
        }

        // interval 为秒；非正数视为无冷却。
        public void MarkUsed(float now, float interval)
        {
            _nextAllowedTime = interval > 0f ? now + interval : float.NegativeInfinity;
        }

        public void Reset()
        {
            _nextAllowedTime = float.NegativeInfinity;
        }
    }
}
