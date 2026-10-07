using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 缓存表：持有所有已加载资源条目；主线程独占、无锁；Key 大小写敏感；lastAccessTime 是 LRU 依据，命中时由 Touch 更新。
    /// </summary>
    internal sealed class CacheStore
    {
        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>();

        public int Count => _entries.Count;

        public bool TryGetEntry(string key, out CacheEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>
        /// Key 存在但类型不匹配返回 false，不抛异常。
        /// </summary>
        public bool TryGet<T>(string key, out CacheEntry entry) where T : UnityEngine.Object
        {
            if (_entries.TryGetValue(key, out entry) && entry.asset is T)
                return true;

            entry = null;
            return false;
        }

        public void Put(string key, UnityEngine.Object asset, bool isPreloaded)
        {
            _entries[key] = new CacheEntry
            {
                asset = asset,
                refCount = 0,           // Retain 由调用方显式做
                lastAccessTime = Time.realtimeSinceStartup,
                isPreloaded = isPreloaded,
                cooldownUntil = 0f,
                canEvict = false,
            };
        }

        public void Touch(string key)
        {
            if (_entries.TryGetValue(key, out var e))
                e.lastAccessTime = Time.realtimeSinceStartup;
        }

        public void Remove(string key) => _entries.Remove(key);

        public IEnumerable<KeyValuePair<string, CacheEntry>> AllEntries => _entries;

        public void Clear() => _entries.Clear();
    }
}
