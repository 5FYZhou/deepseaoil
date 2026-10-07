using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>玩家的运行时账本：血量 ＋ 无敌帧 ＋ 水球计数。由 <see cref="PlayerLogic"/> 持有。</summary>
    public sealed class PlayerStats : IAlivable
    {
        private readonly PlayerSpec _spec;

        private float _current;

        private float _invulnerableUntil = float.NegativeInfinity;

        public PlayerStats(PlayerSpec spec)
        {
            _spec = spec;
            _current = spec.MaxHp;
        }

        public PlayerSpec Spec => _spec;

        public float Maximum => _spec.MaxHp;

        public float Current => _current;

        public int WaterBallCount { get; private set; }

        public bool IsAlive => _current > 0f;

        public bool IsInvulnerable(float now)
        {
            return !CanTakeDamage(now, _invulnerableUntil);
        }

        /// <summary>无敌剩余时长（秒）；不在无敌期时为 0。</summary>
        public float InvulnerableRemaining(float now)
        {
            return Mathf.Max(0f, _invulnerableUntil - now);
        }

        /// <remarks>必须写成 <c>!(now &lt; invulnerableUntil)</c>：任一操作数为 <c>NaN</c> 时它落到"可以受伤"一侧；
        /// 若改写成 <c>now &gt;= invulnerableUntil</c> 则玩家变成<b>永久无敌</b>，而屏幕上什么都看不出来。</remarks>
        public static bool CanTakeDamage(float now, float invulnerableUntil)
        {
            return !(now < invulnerableUntil);
        }

        /// <remarks>被无敌帧挡掉时不扣血、也不写无敌时间：只扣血不写无敌，下一帧立刻再扣一次；只写无敌不扣血，玩家白白进入无敌期。
        /// 接触伤害是"贴住就重复结算"的，靠无敌帧挡。</remarks>
        public bool ApplyDamage(float amount, float now)
        {
            if (amount <= 0f) return false;

            if (!CanTakeDamage(now, _invulnerableUntil)) return false;

            _current = Mathf.Max(0f, _current - amount);

            _invulnerableUntil = now + _spec.InvulnerableDuration;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));

            return true;
        }

        public void ResetToFull()
        {
            _current = _spec.MaxHp;
            _invulnerableUntil = float.NegativeInfinity;

            EventBus<PlayerHealthChanged>.Publish(new PlayerHealthChanged(_current, _spec.MaxHp));
        }

        /// <summary>加水球；<paramref name="amount"/> 非正数时是 no-op。</summary>
        public void AddWaterBall(int amount = 1)
        {
            if (amount <= 0) return;

            WaterBallCount += amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>尝试消耗水球；不够时 <b>不</b> 改动任何状态。</summary>
        public bool TryConsumeWater(int amount = 1)
        {
            if (amount <= 0) return false;

            if (WaterBallCount < amount) return false;

            WaterBallCount -= amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));

            return true;
        }

        /// <summary>水球清零（重开 / 切场景）。血量不在这里重置 —— 那是 <see cref="ResetToFull"/> 的事。</summary>
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
