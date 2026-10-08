namespace DeepseaOil.Presentation.Effects
{
    /// <summary>只读调试快照，拉模型（不走 EventBus）；未 Init 时字段全 0 不抛</summary>
    public readonly struct EffectStats
    {
        public readonly int ActiveInstances;

        public readonly int DriverCount;

        public readonly int PooledObjects;

        public EffectStats(int activeInstances, int driverCount, int pooledObjects)
        {
            ActiveInstances = activeInstances;
            DriverCount = driverCount;
            PooledObjects = pooledObjects;
        }

        public override string ToString()
            => $"active={ActiveInstances} drivers={DriverCount} pooled={PooledObjects}";
    }
}
