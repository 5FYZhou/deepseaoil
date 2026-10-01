using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 引用计数。操作 CacheStore 中条目的 refCount。
    /// 边界：
    ///   - 主线程独占
    ///   - refCount 不为负；Release 未知 Key 或重复 Release 返回 false，由调用方决定怎么记
    ///   - isPreloaded = true 的条目不受引用计数影响（常驻）
    /// </summary>
    internal sealed class RefCounter
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;

        public RefCounter(CacheStore cache, float cooldownSeconds)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// 引用计数 +1。调用方：AssetModule.LoadAsync 命中缓存时、加载完成时。
        /// 边界：Key 不存在返回 false；isPreloaded 条目直接返回 true（no-op）。
        /// </summary>
        public bool Retain(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            entry.refCount++;

            // 从 0 变 1：资源又被持有了，冷却期与可淘汰标记作废
            if (entry.refCount == 1)
            {
                entry.cooldownUntil = 0f;
                entry.canEvict = false;
            }

            return true;
        }

        /// <summary>
        /// 引用计数 −1。调用方：AssetModule.Release。
        /// 边界：Key 不存在 / isPreloaded / 已经是 0 都返回 false 或 no-op，不抛异常。
        ///       归零时设置冷却期，**不立即卸载**。
        /// </summary>
        public bool Release(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            if (entry.refCount == 0)
                return false;   // 调用方多调了一次 Release

            entry.refCount--;

            if (entry.refCount == 0)
            {
                entry.cooldownUntil = Time.realtimeSinceStartup + _cooldownSeconds;
                // canEvict 仍为 false，等 LifecycleMgr 在冷却期结束（或切场景）时置 true
            }

            return true;
        }
    }
}
