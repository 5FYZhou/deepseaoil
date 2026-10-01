namespace DeepseaOil.Data
{
    /// <summary>
    /// 缓存条目。AssetModule 内部数据结构，不对外暴露。
    /// 生命周期：CacheStore 创建 → RefCounter 改 refCount → LifecycleMgr 改 canEvict → 淘汰时移除。
    /// </summary>
    internal sealed class CacheEntry
    {
        /// <summary>资源本体。由 LoadScheduler 加载完成后写入。</summary>
        public UnityEngine.Object asset;

        /// <summary>调用方持有数。由 RefCounter 维护，不为负。</summary>
        public int refCount;

        /// <summary>最后访问时间（Time.realtimeSinceStartup）。LoadAsync 命中 / TryGet 时更新。</summary>
        public float lastAccessTime;

        /// <summary>预加载标记。true 时 Retain/Release 是 no-op，LifecycleMgr 永不淘汰。</summary>
        public bool isPreloaded;

        /// <summary>冷却期结束时间。refCount 归零时设为 now + COOLDOWN_SECONDS。</summary>
        public float cooldownUntil;

        /// <summary>是否可淘汰。冷却期结束后由 LifecycleMgr 置 true；LRU 只淘汰 canEvict=true 的条目。</summary>
        public bool canEvict;
    }
}
