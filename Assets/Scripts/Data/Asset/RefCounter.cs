using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 引用计数（操作 CacheStore 条目的 refCount），主线程独占；refCount 不为负；isPreloaded 条目常驻、不受计数影响。
    /// Release 归零只设冷却期、不立即卸载；未知 Key / 重复 Release 返回 false（不抛异常），调用方：AssetModule。
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
