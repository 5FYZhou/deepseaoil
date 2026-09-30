namespace DeepseaOil.Data
{
    /// <summary>
    /// Data 层只读快照。
    /// 调用时机：DebugOverlay 每 N 帧拉一次。
    /// 边界：
    ///   - struct 值语义；调用方拿到副本，改不了内部状态
    ///   - 所有字段为瞬时值，不保证跨帧一致
    ///   - **字段只增不删**（DebugOverlay 可能引用旧字段，删字段会静默断链）
    /// </summary>
    public struct DataSnapshot
    {
        // ─── ConfigModule ───
        /// <summary>ConfigModule 是否已 Init 成功</summary>
        public bool ConfigReady;
        /// <summary>已登记的表数（来自 TablesMeta.Count）</summary>
        public int TableCount;

        // ─── AssetModule：瞬时状态 ───
        /// <summary>缓存中条目总数（含 refCount 为 0 与 isPreloaded 的）</summary>
        public int CachedAssetCount;
        /// <summary>当前正在加载的数量</summary>
        public int LoadingCount;
        /// <summary>排队等待加载的数量</summary>
        public int QueuedCount;

        // ─── AssetModule：累计计数 ───
        /// <summary>累计加载完成数（进程启动至今）</summary>
        public int CompletedCount;
        /// <summary>累计最终失败数（重试已用尽，含降级）</summary>
        public int FailedCount;
        /// <summary>累计淘汰数</summary>
        public int EvictedCount;
        /// <summary>缓存命中次数</summary>
        public int CacheHits;
        /// <summary>缓存未命中次数</summary>
        public int CacheMisses;
        /// <summary>缓存命中率 [0, 1]；无访问时为 0</summary>
        public float CacheHitRate;
    }
}
