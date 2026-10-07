namespace DeepseaOil.Data
{
    /// <summary>
    /// Data 层只读快照（调用时机：DebugOverlay 每 N 帧拉一次）。
    /// struct 值语义：调用方拿到副本、改不了内部状态；所有字段都是瞬时值，不保证跨帧一致。
    /// **字段只增不删** —— DebugOverlay 可能引用旧字段，删字段会静默断链（不报错、只是读数不对）。
    /// </summary>
    public struct DataSnapshot
    {
        public bool ConfigReady;
        /// <summary>已登记的表数（来自 TablesMeta.Count）。</summary>
        public int TableCount;

        /// <summary>缓存中条目总数（含 refCount 为 0 与 isPreloaded 的）。</summary>
        public int CachedAssetCount;
        public int LoadingCount;
        public int QueuedCount;

        // AssetModule：累计计数（进程启动至今）
        public int CompletedCount;
        /// <summary>累计最终失败数（重试已用尽，含降级）。</summary>
        public int FailedCount;
        public int EvictedCount;
        public int CacheHits;
        public int CacheMisses;
        /// <summary>缓存命中率 [0, 1]；无访问时为 0。</summary>
        public float CacheHitRate;
    }
}
