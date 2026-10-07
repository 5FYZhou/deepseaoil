namespace DeepseaOil.Presentation.Effects
{
    /// <summary><c>EffectModule</c> 的只读调试快照。拉模型：由调试面板 / 观测代码主动拉取，不走 EventBus。</summary>
    /// <remarks>未 Init 时所有字段为 0，不抛异常。</remarks>
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
