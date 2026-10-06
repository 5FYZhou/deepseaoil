using DeepseaOil.Data;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家的账本：<b>水球计数 ＋ 血量</b>。由 <see cref="PlayerLogic"/> 内部组合持有，不单独外流。
    /// </summary>
    /// <remarks>
    /// <b>它由 <c>PlayerResources</c> 改名而来（收口决策：命名要覆盖内容）。</b>
    /// 改名前它只管水球，而血量住在表现层的 <c>PlayerHealthController</c> 里 ——
    /// 同一个玩家的两份状态分散在两个层、两个构造点，于是"玩家被打空了谁负责"没有唯一答案。
    /// 现在两者都是本类的字段：玩家的数据全在一处。
    /// <para><b>它自己发布事实事件</b>（<see cref="WaterBallCountChanged"/>、
    /// <c>PlayerHealthChanged</c> 由 <see cref="PlayerHealth"/> 发）：数据的持有者最清楚"什么时候变了"，
    /// 让表现层记得发等于把"忘了发 ⇒ HUD 静默不更新"埋进调用点。
    /// <b>事件名与载荷都没变</b>：HUD 对本次改造无感。</para>
    /// <para><b>上限当前是"无上限"</b>：需求里没有消耗以外的口径，凭空加一个会因为没有人验证而失真。</para>
    /// </remarks>
    public sealed class PlayerStats
    {
        /// <summary>血量 ＋ 无敌帧。</summary>
        public PlayerHealth Health { get; }

        /// <summary>水球数量。</summary>
        public int WaterBallCount { get; private set; }

        /// <summary>是否还有血。</summary>
        public bool IsAlive => Health.IsAlive;

        public PlayerStats(in PlayerSpec spec)
        {
            Health = new PlayerHealth(in spec);
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

        /// <summary>水球清零（重开 / 切场景）。血量不在这里重置 —— 那是 <see cref="PlayerHealth.ResetToFull"/> 的事。</summary>
        public void ResetWater()
        {
            WaterBallCount = 0;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>把两块读数各重播一次（HUD 面板加载完成时用）。</summary>
        public void Announce()
        {
            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));

            Health.Announce();
        }
    }
}
