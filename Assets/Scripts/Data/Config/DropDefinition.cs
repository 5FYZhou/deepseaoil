namespace DeepseaOil.Data
{
    /// <summary>
    /// 一种掉落物的<b>全部数值</b>（取值边界）：逻辑侧与表现侧的掉落物实体都只认它。
    /// </summary>
    /// <remarks>
    /// <b>收口前这四个数散在 <c>WaterBall</c> 的 Inspector 上</b>（飞行时长 / 弧高 / 飞向玩家的速度 /
    /// 判定距离），于是"掉落物是什么手感"只能靠点开场景里那个组件看 —— 而掉落物是运行期建出来的，
    /// 场景里根本没有它的实例。
    /// <para><b>行为与属性归实体自己</b>（审查的口径）：本结构体只装"数值"，
    /// "要不要追踪玩家 / 会不会被吸走 / 落地有没有音效"都在掉落物实体那一侧。</para>
    /// </remarks>
    public readonly struct DropDefinition
    {
        /// <summary>种类（= 取值键）。</summary>
        public readonly DropType Type;

        /// <summary>从生成点抛到落点的时长（秒）。</summary>
        public readonly float FlightDuration;

        /// <summary>抛物线的弧高（世界单位）。</summary>
        public readonly float ArcHeight;

        /// <summary>落点 → 玩家的飞行速度（单位/秒）。</summary>
        public readonly float HomingSpeed;

        /// <summary>判定"够到玩家了"的距离（世界单位）。</summary>
        public readonly float ReachDistance;

        /// <summary>领取一次给几个（载荷数量，<b>不写死在订阅方</b>）。</summary>
        public readonly int Amount;

        public DropDefinition(
            DropType type,
            float flightDuration,
            float arcHeight,
            float homingSpeed,
            float reachDistance,
            int amount)
        {
            Type = type;
            FlightDuration = flightDuration;
            ArcHeight = arcHeight;
            HomingSpeed = homingSpeed;
            ReachDistance = reachDistance;
            Amount = amount;
        }
    }
}
