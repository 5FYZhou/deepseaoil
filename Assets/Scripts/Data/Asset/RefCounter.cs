using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>引用计数，操作 CacheStore 条目的 refCount</summary>
    /// <remarks>Release 归零设冷却期不立即卸载</remarks>
    internal sealed class RefCounter
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;

        public RefCounter(CacheStore cache, float cooldownSeconds)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
        }

        public bool Retain(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            entry.refCount++;

            // 从 0 变 1：作废冷却期与 canEvict
            if (entry.refCount == 1)
            {
                entry.cooldownUntil = 0f;
                entry.canEvict = false;
            }

            return true;
        }

        public bool Release(string key)
        {
            if (!_cache.TryGetEntry(key, out var entry))
                return false;

            if (entry.isPreloaded)
                return true;

            if (entry.refCount == 0)
                return false;   // 多调了一次

            entry.refCount--;

            if (entry.refCount == 0)
            {
                entry.cooldownUntil = Time.realtimeSinceStartup + _cooldownSeconds;
            }

            return true;
        }
    }
}
