namespace DeepseaOil.Data
{
    /// <summary>Data 层只读快照；struct 值语义，调用方拿副本；字段只增不删，删字段静默断链</summary>
    public struct DataSnapshot
    {
        public bool ConfigReady;
        public int TableCount;

        /// <summary>缓存条目数，含 refCount=0 与 isPreloaded</summary>
        public int CachedAssetCount;
        public int LoadingCount;
        public int QueuedCount;

        public int CompletedCount;
        /// <summary>累计最终失败数，含降级</summary>
        public int FailedCount;
        public int EvictedCount;
        public int CacheHits;
        public int CacheMisses;
        /// <summary>缓存命中率 [0,1]，无访问时为 0</summary>
        public float CacheHitRate;
    }
}
