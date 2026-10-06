namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 通用冷却件：<b>只回答"现在能不能用"</b>，不读时间、不做输入缓冲。
    /// </summary>
    /// <remarks>
    /// <b>为什么做成通用件：</b>投掷冷却与冲刺冷却是同一类东西（"资格计时器"），
    /// 只是名字不同 —— 两处各写一份的下场是"移动层用一套口径、战斗层用另一套"，
    /// 而那种不一致不报错，只表现为两个技能的手感莫名其妙地不同。
    /// <para><b>为什么存"绝对时刻"而不是"剩余秒数"：</b>绝对时刻不受暂停影响
    /// （<c>timeScale = 0</c> 时渲染帧的 <c>Time.time</c> 也停，但一个已经记下的时刻不需要每帧累减），
    /// 也不需要在每个驱动点都记得减一次 Δt。</para>
    /// <para><b>它不做输入缓冲：</b>冲刺的资格是"冷却 ＋ 缓冲窗口"两层条件，
    /// 窗口那一半属于 <c>InputBuffer</c>（它有采样率、有容量、会被清空），
    /// 吞进冷却件里会让两套语义纠缠在一起。</para>
    /// <para><b>初始值是负无穷：</b>开局即可用，不需要额外的"是不是第一次"判断。</para>
    /// <para>用<b>类</b>而不是结构体：两个实例分别归战斗层与移动层持有，
    /// 结构体字段经属性/装箱传递时会被复制，改的就不是原来那一份了。</para>
    /// </remarks>
    public sealed class Cooldown
    {
        /// <summary>下一次允许使用的时间；初始为负无穷（开局即可用）。</summary>
        private float _nextAllowedTime = float.NegativeInfinity;

        /// <summary>现在能不能用。<b>纯查询</b>，可重复调用、无副作用。</summary>
        public bool CanUse(float now)
        {
            return now >= _nextAllowedTime;
        }

        /// <summary>记录一次使用，并在 <paramref name="interval"/> 秒内拒绝下一次。</summary>
        /// <param name="now">当前时间。</param>
        /// <param name="interval">间隔（秒）；非正数视为无冷却。</param>
        public void MarkUsed(float now, float interval)
        {
            _nextAllowedTime = interval > 0f ? now + interval : float.NegativeInfinity;
        }

        /// <summary>清掉冷却（重开 / 切场景 / 重生）。可重复调用。</summary>
        public void Reset()
        {
            _nextAllowedTime = float.NegativeInfinity;
        }
    }
}
