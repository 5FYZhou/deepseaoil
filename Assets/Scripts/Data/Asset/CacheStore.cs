using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>缓存表，主线程独占</summary>
    internal sealed class CacheStore
    {
        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>();

        public int Count => _entries.Count;

        public bool TryGetEntry(string key, out CacheEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>Key 存在但类型不匹配返回 false</summary>
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
                refCount = 0,           // 调用方 Retain
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
