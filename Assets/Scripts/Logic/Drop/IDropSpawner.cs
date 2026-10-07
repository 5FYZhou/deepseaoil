namespace DeepseaOil.Logic.Drop
{
    /// <summary>掉落物的生成口。</summary>
    /// <remarks>
    /// 产出方只能产出，拿不到持有者的列表与清场能力；实现方是持有掉落物的那一层（表现层 <c>DropDirector</c>）。
    /// 上限与节流由持有者判定。
    /// </remarks>
    public interface IDropSpawner
    {
        /// <summary>请求产出一个掉落物；被接收（实体已建出并开始飞）为 <c>true</c>。当前无上限，恒为 <c>true</c>。</summary>
        bool TrySpawn(in DropSpawnRequest request);
    }
}
