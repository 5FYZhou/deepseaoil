namespace DeepseaOil.Data
{
    /// <summary>Data 层可观测性表面；拉模型，不走 EventBus；只读、不持资源引用、不分配；未 Init 时返回零值快照，不报错</summary>
    public static class DataMetrics
    {
        public static DataSnapshot GetSnapshot()
        {
            var snap = new DataSnapshot();

            snap.ConfigReady = ConfigModule.IsReady;
            snap.TableCount = ConfigModule.IsReady ? TablesMeta.Count : 0;

            if (!AssetModule.IsInitialized)
                return snap;

            snap.CachedAssetCount = AssetModule.Cache.Count;
            snap.LoadingCount = AssetModule.Scheduler.LoadingCount;
            snap.QueuedCount = AssetModule.Scheduler.QueuedCount;
            snap.CompletedCount = AssetModule.Scheduler.CompletedCount;
            snap.FailedCount = AssetModule.Failure.FailedCount;
            snap.EvictedCount = AssetModule.Lifecycle.EvictedCount;
            snap.CacheHits = AssetModule.CacheHits;
            snap.CacheMisses = AssetModule.CacheMisses;

            int total = snap.CacheHits + snap.CacheMisses;
            snap.CacheHitRate = total > 0 ? (float)snap.CacheHits / total : 0f;

            return snap;
        }
    }
}
