namespace DeepseaOil.Foundation
{
    /// <summary>通用冷却件，只管"现在能不能用"，不读时间</summary>
    // 投掷与冲刺共用；存绝对时刻，不受 timeScale=0 影响；窗口条件归 InputBuffer
    public sealed class Cooldown
    {
        // 负无穷=开局即可用
        private float _nextAllowedTime = float.NegativeInfinity;

        public bool CanUse(float now)
        {
            return now >= _nextAllowedTime;
        }

        // interval 秒；≤ 0 即无冷却
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
