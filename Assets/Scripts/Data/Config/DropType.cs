namespace DeepseaOil.Data
{
    /// <summary>
    /// 掉落物的种类：领取时回答"这是什么东西"。
    /// </summary>
    /// <remarks>
    /// <b>为什么只有一种也值得有枚举：</b>它让"领取"的载荷（<c>DropCollected</c>）有类型，
    /// 而不是靠"哪条事件"来区分。加一种掉落物 ＝ 加一个枚举成员 ＋ 一行取值（<see cref="DropCatalog"/>）
    /// ＋ 组件工厂一行 —— 与球种同一条纪律。
    /// <para>放在 <c>Data</c> 而不是 <c>Logic</c>：它是取值口径的一部分（
    /// <see cref="DropDefinition"/> 拿它当键），而 <c>Logic</c> 与表现层都能引用 <c>Data</c>。</para>
    /// </remarks>
    public enum DropType
    {
        /// <summary>水球：领取后进玩家的水球账本。</summary>
        Water = 0,
    }
}
