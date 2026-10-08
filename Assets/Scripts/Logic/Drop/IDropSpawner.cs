namespace DeepseaOil.Logic.Drop
{
    /// <summary>掉落物生成口，实现方是持有掉落物的表现层</summary>
    public interface IDropSpawner
    {
        /// <summary>请求产出掉落物，恒为 true；上限与节流由持有者判定</summary>
        bool TrySpawn(in DropSpawnRequest request);
    }
}
