using UnityEngine;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家的资源计数（当前只有水球）。<b>纯逻辑</b>。
    /// </summary>
    /// <remarks>
    /// 从白模 <c>ThrowSpawner.WaterBallNum</c> 那个"setter 里直刷 TMP_Text"的属性搬出来：
    /// 计数是逻辑，刷新 UI 是表现层的事，两者之间只该有一条事实事件。
    /// <para>上限当前是"无上限"：需求里没有消耗以外的口径，凭空加一个会因为没有人验证而失真。
    /// 将来策划给上限时加一个 <c>Max</c> 字段即可。</para>
    /// </remarks>
    public sealed class PlayerResources
    {
        /// <summary>水球数量。</summary>
        public int WaterBallCount { get; private set; }

        /// <summary>加资源。<paramref name="amount"/> 非正数时是 no-op。</summary>
        public void Add(int amount = 1)
        {
            if (amount <= 0) return;

            WaterBallCount += amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>尝试消耗资源；不够时 <b>不</b> 改动任何状态。</summary>
        /// <returns>真的扣掉了为 <c>true</c>。</returns>
        public bool TryConsume(int amount = 1)
        {
            if (amount <= 0) return false;

            if (WaterBallCount < amount) return false;

            WaterBallCount -= amount;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));

            return true;
        }

        /// <summary>清零（重开 / 切场景）。</summary>
        public void Reset()
        {
            WaterBallCount = 0;

            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }

        /// <summary>把当前值重播一次（HUD 面板加载完成时用）。</summary>
        public void Announce()
        {
            EventBus<WaterBallCountChanged>.Publish(new WaterBallCountChanged(WaterBallCount));
        }
    }
}
