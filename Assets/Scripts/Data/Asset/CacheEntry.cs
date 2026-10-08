namespace DeepseaOil.Data
{
    /// <summary>缓存条目</summary>
    /// <remarks>由 CacheStore 创建，RefCounter 改 refCount，LifecycleMgr 改 canEvict</remarks>
    internal sealed class CacheEntry
    {
        /// <summary>资源本体</summary>
        public UnityEngine.Object asset;

        /// <summary>调用方持有数</summary>
        public int refCount;

        public float lastAccessTime;

        /// <summary>预加载标记，true 永不淘汰、不计引用</summary>
        public bool isPreloaded;

        /// <summary>冷却期结束时间 = now + COOLDOWN_SECONDS</summary>
        public float cooldownUntil;

        public bool canEvict;
    }
}
