namespace DeepseaOil.Data
{
    /// <summary>
    /// Data 层可观测性表面。**拉模型**：不推送事件，不走 EventBus。
    /// 调用时机：DebugOverlay 每 N 帧拉一次。
    /// 边界：
    ///   - 只读，不修改任何 Module 状态
    ///   - 不持有资源引用
    ///   - 允许在未 Init 时调用（返回零值快照）
    ///   - 不分配（struct + 无 List），每帧调用开销可忽略
    /// </summary>
    public static class DataMetrics
    {
        public static DataSnapshot GetSnapshot()
        {
            var snap = new DataSnapshot();

            // ── ConfigModule ──
            snap.ConfigReady = ConfigModule.IsReady;
            snap.TableCount = ConfigModule.IsReady ? DeepseaOil.Config.TablesMeta.Count : 0;

            // ── AssetModule ──
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
