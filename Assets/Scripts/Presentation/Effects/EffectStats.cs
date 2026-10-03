namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// <c>EffectModule</c> 的只读调试快照。拉模型：由调试面板 / 观测代码主动拉取，不走 EventBus。
    /// </summary>
    /// <remarks>
    /// 与 <c>DataMetrics</c> 同一套观测风格。未 Init 时所有字段为 0，不抛异常。
    /// </remarks>
    public readonly struct EffectStats
    {
        /// <summary>所有 Driver 的活跃实例总数。</summary>
        public readonly int ActiveInstances;

        /// <summary>已注册的 Driver 数（= EffectCatalog 行数 + 手工 Register 的数量）。</summary>
        public readonly int DriverCount;

        /// <summary>所有 Driver 池中待用对象总数。</summary>
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
