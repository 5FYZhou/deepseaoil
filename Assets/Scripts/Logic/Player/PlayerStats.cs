using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家的运行时账本：<b>血量 ＋ 无敌帧 ＋ 水球计数</b>。由 <see cref="PlayerLogic"/> 持有。
    /// </summary>
    /// <remarks>
    /// <b>它取代了"三份"：</b>收口前玩家的值分散在 <c>PlayerSpec</c>（表值结构与折算）、
    /// <c>PlayerHealth</c>（血量 ＋ 无敌帧）、<c>PlayerStats</c>（水球）三处，
    /// "这个值我要去哪里找"因此要逐个判断、还容易问错人。现在只认本类一处。
    /// <para><b>只读配置（<see cref="Spec"/>）与可变状态（本类的字段）刻意分开：</b>
    /// <c>Spec</c> 是 <c>ConfigModule</c> 给出的取值边界（表 ＋ SO），本类的字段才是"这一局跑出来的东西"。</para>
    /// <para><b>它自己发布事实事件</b>：数据的持有者最清楚"什么时候变了"，
    /// 让表现层记得发等于把"忘了发 ⇒ HUD 静默不更新"埋进调用点。</para>
    /// <para><b>无物理查询、无视觉、无输入</b>：接触检测、<c>Time.fixedTime</c>、刚体位置都在表现层，
    /// 本类只被喂进"扣多少、现在几点"。</para>
    /// </remarks>
    public sealed class PlayerStats : IAlivable
    {
        private readonly PlayerSpec _spec;

        private float _current;

        private float _invulnerableUntil = float.NegativeInfinity;

        /// <param name="spec">玩家取值边界（表行 ＋ 移动 SO ＋ 投掷调参）。</param>
        public PlayerStats(PlayerSpec spec)
        {
            _spec = spec;
            _current = spec.MaxHp;
        }

        /// <summary>本局的配置取值边界（只读）：血量 / 无敌 / 受击 / 接触半径。</summary>
        public PlayerSpec Spec => _spec;

        /// <summary>血量上限。</summary>
        public float Maximum => _spec.MaxHp;

        /// <summary>当前血量。</summary>
        public float Current => _current;

        /// <summary>水球数量。</summary>
        public int WaterBallCount { get; private set; }

        /// <inheritdoc />
        public bool IsAlive => _current > 0f;

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
        /// <returns>真的扣掉了血为 <c>true</c>（被无敌帧挡掉时为 <c>false</c>）。</returns>
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

        /// <summary>
        /// 回到满血并清掉无敌期。
        /// </summary>
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

        /// <summary>加水球。<paramref name="amount"/> 非正数时是 no-op。</summary>
        public void AddWaterBall(int amount = 1)
        {
            if (amount <= 0) return;

            WaterBallCount += amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>尝试消耗水球；不够时 <b>不</b> 改动任何状态。</summary>
        /// <returns>真的扣掉了为 <c>true</c>。</returns>
        public bool TryConsumeWater(int amount = 1)
        {
            if (amount <= 0) return false;

            if (WaterBallCount < amount) return false;

            WaterBallCount -= amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));

            return true;
        }

        /// <summary>水球清零（重开 / 切场景）。血量不在这里重置 —— 那是 <see cref="ResetToFull"/> 的事。</summary>
        /// <remarks>
        /// <b>当前零生产消费者</b>（打空重来走 <c>PlayerLogic.RespawnTo</c>，它不扣不清水球）。
        /// 刻意保留：它是"重开一局"这条语义的另一半，删掉之后重开的实现会缺一个口。
        /// </remarks>
        public void ResetWater()
        {
            WaterBallCount = 0;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>把两块读数各重播一次（HUD 面板加载完成时用）。</summary>
        public void Announce()
        {
            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));
        }
    }
}
