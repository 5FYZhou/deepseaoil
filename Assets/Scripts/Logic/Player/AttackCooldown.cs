namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 攻击冷却：只回答"现在能不能打"，不读时间。
    /// </summary>
    /// <remarks>
    /// 从白模 <c>ThrowSpawner._nextAttackTime</c> 那个裸 <c>float</c> 比较搬出来，
    /// 换成一个有名字的对象：裸字段的问题是"谁都能读、谁都能写"，
    /// 而"下次可攻击时间"与"现在几点"必须来自同一个时间源（<c>Time.time</c> vs <c>Time.fixedTime</c>
    /// 混用会让冷却看起来随机变长），收进这里只有一个入口。
    /// <para>它<b>不做输入缓冲</b>：冷却期内的按下不会排队。与白模一致 ——
    /// 需求里的"攻击间隔"就是"按了但不给打"，而不是"提前按也算数"。</para>
    /// </remarks>
    public sealed class AttackCooldown
    {
        /// <summary>下一次允许攻击的时间；初始为负无穷（开局即可攻击）。</summary>
        private float _nextAllowedTime = float.NegativeInfinity;

        /// <summary>现在能不能攻击。</summary>
        public bool CanAttack(float now)
        {
            return now >= _nextAllowedTime;
        }

        /// <summary>记录一次攻击，并在 <paramref name="interval"/> 秒内拒绝下一次。</summary>
        /// <param name="now">当前时间。</param>
        /// <param name="interval">间隔（秒）；非正数视为无冷却。</param>
        public void MarkUsed(float now, float interval)
        {
            _nextAllowedTime = interval > 0f ? now + interval : float.NegativeInfinity;
        }

        /// <summary>清掉冷却（重开 / 切场景）。</summary>
        public void Reset()
        {
            _nextAllowedTime = float.NegativeInfinity;
        }
    }
}
