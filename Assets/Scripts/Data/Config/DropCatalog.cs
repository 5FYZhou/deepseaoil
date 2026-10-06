namespace DeepseaOil.Data
{
    /// <summary>
    /// 掉落物定义的<b>取值点</b>：与 <c>SpecCatalog</c> 同一条纪律 —— 数值 → 结构体的折算只发生在一处。
    /// </summary>
    /// <remarks>
    /// <b>今天它只是转发</b>（<c>DropTuning</c> 的字段 → 结构体），但这层取值接口现在就要有：
    /// 将来掉落物数值若从 SO 移到表里，改的是本文件，而不是每一个消费者。
    /// <para>加一种掉落物 ＝ 加一个 <see cref="DropType"/> 成员 ＋ 这里加一个方法
    /// ＋ 表现层的组件工厂加一行。</para>
    /// </remarks>
    public static class DropCatalog
    {
        /// <summary>水球掉落物：抛物线飞向落点、等玩家碰、被领走时进玩家账本。</summary>
        public static DropDefinition Water()
        {
            DropTuning tuning = DropTuning.LoadOrDefault();

            return new DropDefinition(
                DropType.Water,
                tuning.flightDuration,
                tuning.arcHeight,
                tuning.homingSpeed,
                tuning.reachDistance,
                tuning.amount);
        }
    }
}
