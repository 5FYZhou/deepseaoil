using UnityEngine;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家血量状态：血量、无敌帧、重置。<b>不含物理查询、不含视觉、不含输入</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么它不是 MonoBehaviour：</b>逻辑层的硬约束是"零引擎类型"，而血量的全部规则
    /// （扣多少、能不能扣、什么时候重置）与引擎无关。接触检测、<c>Time.fixedTime</c>、刚体位置
    /// 都在表现层的 <c>PlayerHealthController</c> 里，本类只被喂进"扣多少、现在几点"。
    /// <para><b>它自己发布事实事件</b>（<see cref="PlayerHealthChanged"/>）：数据的持有者最清楚
    /// "什么时候变了"。让表现层记得发，等于把"忘了发 ⇒ HUD 静默不更新"这种缺陷埋进调用点。</para>
    /// </remarks>
    public sealed class PlayerHealth
    {
        private readonly PlayerSpec _spec;

        private float _current;

        private float _invulnerableUntil = float.NegativeInfinity;

        /// <summary>当前血量。</summary>
        public float Current => _current;

        /// <summary>血量上限。</summary>
        public float Maximum => _spec.MaxHp;

        /// <summary>是否还有血。</summary>
        public bool IsAlive => _current > 0f;

        /// <summary>本帧的配置（只读，供表现层取接触伤害 / 无敌时长 / 重试延时）。</summary>
        public PlayerSpec Spec => _spec;

        public PlayerHealth(in PlayerSpec spec)
        {
            _spec = spec;
            _current = spec.MaxHp;
        }

        /// <summary>是否处于无敌期。</summary>
        public bool IsInvulnerable(float now)
        {
            return !CanTakeDamage(now, _invulnerableUntil);
        }

        /// <summary>无敌剩余时长（秒）；不在无敌期时为 0。</summary>
        public float InvulnerableRemaining(float now)
        {
            return Mathf.Max(0f, _invulnerableUntil - now);
        }

        /// <summary>
        /// 无敌帧判据。<b>静态纯函数</b>，所以 EditMode 测试能直接喂时间戳。
        /// </summary>
        /// <param name="now">当前时间。</param>
        /// <param name="invulnerableUntil">无敌结束时间；从未受击时可以是负无穷或 <c>NaN</c>。</param>
        /// <returns>可以承受伤害为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>写成 <c>!(now &lt; invulnerableUntil)</c> 而不是 <c>now &gt;= invulnerableUntil</c>，</b>
        /// 是为了让非法时间戳落到"可以受伤"这一侧：两个操作数里有 <c>NaN</c> 时前者为 <c>true</c>
        /// （照常结算），后者为 <c>false</c>（玩家变成<b>永久无敌</b>，而屏幕上什么都不会显示）。
        /// "看起来在受伤却永远不死"比"挨了一下不该挨的打"糟得多 —— 前者查不出来。
        /// </remarks>
        public static bool CanTakeDamage(float now, float invulnerableUntil)
        {
            return !(now < invulnerableUntil);
        }

        /// <summary>
        /// 扣血。<b>唯一入口</b>（接触、将来的陷阱与技能都走这里）。
        /// </summary>
        /// <param name="amount">伤害值；非正数直接忽略。</param>
        /// <param name="now">当前时间。</param>
        /// <returns>真的扣掉了血为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>被挡掉时不扣血、不写无敌时间</b>：两件事必须一起发生或一起不发生。
        /// 只扣血不写无敌，下一帧立刻再扣一次；只写无敌不扣血，玩家会白白进入无敌期。
        /// <para>接触伤害是"贴住就重复结算"的，靠无敌帧挡：没有它，玩家贴在敌人身上时
        /// 每帧都会扣一次、十帧内打空 —— 那不是"被打了十下"，是一瞬间死。</para>
        /// </remarks>
        public bool ApplyDamage(float amount, float now)
        {
            if (amount <= 0f) return false;

            if (!CanTakeDamage(now, _invulnerableUntil)) return false;

            _current = Mathf.Max(0f, _current - amount);

            _invulnerableUntil = now + _spec.InvulnerableDuration;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));

            return true;
        }

        /// <summary>回到满血并清掉无敌期。</summary>
        /// <remarks>
        /// 清掉无敌是刻意的：重置之后玩家已经不在敌人身边（清场），不需要靠无敌撑过重开的那一帧；
        /// 而留着它会让"下一次真的被打到"晚 0.8 秒才掉血。
        /// </remarks>
        public void ResetToFull()
        {
            _current = _spec.MaxHp;
            _invulnerableUntil = float.NegativeInfinity;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));
        }

        /// <summary>把当前值重播一次（HUD 面板加载完成时用，见 <c>RequestHudRefresh</c>）。</summary>
        public void Announce()
        {
            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));
        }
    }
}
