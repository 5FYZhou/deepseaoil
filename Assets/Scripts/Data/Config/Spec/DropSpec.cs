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
        /// <param name="visuals">观感颜色表；为 <c>null</c> 时表现层走自己的兜底。</param>
        public DropSpec(DropTuning tuning, VisualPalette visuals = null)
        {
            _tuning = tuning;
            Visuals = visuals;
        }

        /// <summary>观感颜色表（掉落物本体色）。表现层从它取色，逻辑层不认识它。</summary>
        public VisualPalette Visuals { get; }

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
