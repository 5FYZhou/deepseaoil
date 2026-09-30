using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 缓存表。持有所有已加载资源条目。
    /// 边界：
    ///   - 主线程独占，无锁
    ///   - Key 为字符串，大小写敏感
    /// </summary>
    internal sealed class CacheStore
    {
        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>();

        public int Count => _entries.Count;

        /// <summary>尝试取条目（不改变引用计数）。调用方：RefCounter / LifecycleMgr / AssetModule。</summary>
        public bool TryGetEntry(string key, out CacheEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>
        /// 尝试取类型匹配的条目。调用方：AssetModule.LoadAsync / TryGet。
        /// 边界：Key 存在但类型不匹配返回 false（不抛异常）。
        /// </summary>
        public bool TryGet<T>(string key, out CacheEntry entry) where T : UnityEngine.Object
        {
            if (_entries.TryGetValue(key, out entry) && entry.asset is T)
                return true;

            entry = null;
            return false;
        }

        /// <summary>
        /// 写入条目（已存在则覆盖）。
        /// 调用方：AssetModule 的 onDone 回调。
        /// 边界：调用方需保证不覆盖 refCount &gt; 0 的条目——D1 的 pending 合并已经保证同一 Key 不会并发加载两次。
        /// </summary>
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

        /// <summary>更新访问时间。调用方：LoadAsync 命中 / TryGet 命中。</summary>
        public void Touch(string key)
        {
            if (_entries.TryGetValue(key, out var e))
                e.lastAccessTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 移除条目。调用方：LifecycleMgr 淘汰时。
        /// 边界：调用方保证 refCount == 0 &amp;&amp; canEvict == true &amp;&amp; !isPreloaded。
        /// </summary>
        public void Remove(string key) => _entries.Remove(key);

        /// <summary>
        /// 遍历所有条目。调用方：LifecycleMgr.Tick。
        /// 边界：返回内部集合本身（避免每帧临时数组），**遍历中不得增删**。
        /// </summary>
        public IEnumerable<KeyValuePair<string, CacheEntry>> AllEntries => _entries;

        /// <summary>全部清空。调用方：AssetModule.Dispose。</summary>
        public void Clear() => _entries.Clear();
    }
}
