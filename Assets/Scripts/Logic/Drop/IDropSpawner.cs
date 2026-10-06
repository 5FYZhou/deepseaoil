namespace DeepseaOil.Logic.Drop
{
    /// <summary>
    /// 掉落物的<b>生成口</b>：一个方法的窄接口 —— 产出方拿着它就能产出，
    /// 但拿不到持有者的列表、驱动与清场能力。
    /// </summary>
    /// <remarks>
    /// <b>谁实现它：</b>持有掉落物的那一层（当前是表现层的 <c>DropDirector</c>）。
    /// 产出方（喷泉、将来的敌人死亡）在装配期被注入，于是"喷泉认识掉落物管理器"这件事
    /// 只发生在组合根里，不在喷泉内部。
    /// <para><b>为什么返回 bool：</b>持有者才是知道"我有几种、满了没有"的一方（审查把这叫接收权）。
    /// 今天还没有上限，返回值因此恒为 true；等出现上限或节流时，这扇门就在这里，
    /// 产出方一行都不用改。</para>
    /// </remarks>
    public interface IDropSpawner
    {
        /// <summary>请求产出一个掉落物。</summary>
        /// <param name="request">产什么 / 从哪出 / 落在哪。</param>
        /// <returns>被接收（实体已经建出来并开始飞）为 <c>true</c>。</returns>
        bool TrySpawn(in DropSpawnRequest request);
    }
}
