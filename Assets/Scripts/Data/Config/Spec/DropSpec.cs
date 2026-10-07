using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一种掉落物的取值边界：持有 <c>DropTuning</c> SO，暴露被消费的语义点。
    /// </summary>
    /// <remarks>
    /// <b>它存在的理由与 <see cref="ProjectileSpec"/> 相同，只是数据全在 SO 一侧</b>：
    /// 掉落物今天没有表，但把"取值走 <c>ConfigModule</c>"这条口径统一之后，
    /// 将来掉落物数值若从 SO 移到表里，改的是本类与 <c>ConfigModule.GetDrop</c>，
    /// 而不是每一个消费者。
    /// <para><b>行为与属性归实体自己</b>（审查的口径）：本类只装"数值"，
    /// "要不要追踪玩家 / 会不会被吸走 / 落地有没有音效"都在掉落物实体那一侧。</para>
    /// <para>SO 不对外暴露：<c>DropTuning</c> 不是消费者的词汇，<see cref="DropSpec"/> 才是。</para>
    /// </remarks>
    public sealed class DropSpec
    {
        private readonly DropTuning _tuning;

        /// <param name="tuning">掉落物调参（SO）。</param>
        public DropSpec(DropTuning tuning)
        {
            _tuning = tuning;
        }

        /// <summary>本体颜色。</summary>
        /// <remarks>
        /// <b>调色板自己不出去</b>（同 <see cref="ProjectileSpec.BallColor"/> 的理由）：
        /// 唯一权威来源是 <c>ConfigModule.Visuals</c>，本属性只是按掉落物的语义解析一次。
        /// </remarks>
        public Color Color => ConfigModule.Visuals.waterBall;

        /// <summary>从生成点抛到落点的时长（秒）。</summary>
        public float FlightDuration => _tuning.flightDuration;

        /// <summary>抛物线的弧高（世界单位）。</summary>
        public float ArcHeight => _tuning.arcHeight;

        /// <summary>落点 → 玩家的飞行速度（单位/秒）。</summary>
        public float HomingSpeed => _tuning.homingSpeed;

        /// <summary>判定"够到玩家了"的距离（世界单位）。</summary>
        public float ReachDistance => _tuning.reachDistance;

        /// <summary>领取一次给几个（载荷数量，<b>不写死在订阅方</b>）。</summary>
        public int Amount => _tuning.amount;

        /// <summary>本体视觉直径（世界单位）。</summary>
        public float BodyDiameter => _tuning.bodyDiameter;

        /// <summary>触发半径（世界单位）。</summary>
        public float TriggerRadius => _tuning.triggerRadius;
    }
}
