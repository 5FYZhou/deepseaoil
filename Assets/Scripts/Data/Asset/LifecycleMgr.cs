using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>生命周期管理：冷却期标记 ＋ LRU 淘汰 ＋ 淘汰后的资源回收。</summary>
    /// <remarks>调用时机：Tick 由 <c>GameRoot</c> 每帧驱动；OnSceneSwitch 由 <c>SceneService</c> 切场景前调用。
    /// 边界：主线程独占；isPreloaded = true 的条目永不淘汰；单帧最多淘汰 _maxEvictPerTick 条，避免卡帧；不修改 refCount，也不删除 refCount &gt; 0 的条目。</remarks>
    internal sealed class LifecycleMgr
    {
        private readonly CacheStore _cache;
        private readonly float _cooldownSeconds;
        private readonly int _maxEntries;
        private readonly int _maxEvictPerTick;
        private int _evictedCount;

        public int EvictedCount => _evictedCount;

        public LifecycleMgr(CacheStore cache, float cooldownSeconds, int maxEntries, int maxEvictPerTick)
        {
            _cache = cache;
            _cooldownSeconds = cooldownSeconds;
            _maxEntries = maxEntries;
            _maxEvictPerTick = maxEvictPerTick;
        }

        /// <summary>每帧推进。两阶段：先标记冷却期到期（每帧全遍历 O(N)），再按需 LRU 淘汰（只在超阈值时发生）。</summary>
        public void Tick(float dt)
        {
            float now = Time.realtimeSinceStartup;

            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.refCount > 0) continue;
                if (entry.isPreloaded) continue;
                if (entry.canEvict) continue;
                if (now < entry.cooldownUntil) continue;

                entry.canEvict = true;
            }

            int overflow = _cache.Count - _maxEntries;
            if (overflow > 0)
                EvictLRU(overflow);
        }

        /// <summary>按 LRU 淘汰。调用方：Tick 阶段 2。</summary>
        /// <remarks>边界：只淘汰 canEvict && !isPreloaded；单次最多 _maxEvictPerTick 个；先收集候选再删除。</remarks>
        private void EvictLRU(int count)
        {
            List<KeyValuePair<string, CacheEntry>> candidates = null;

            foreach (var kv in _cache.AllEntries)
            {
                if (!kv.Value.canEvict) continue;
                if (kv.Value.isPreloaded) continue;

                (candidates ?? (candidates = new List<KeyValuePair<string, CacheEntry>>())).Add(kv);
            }

            if (candidates == null || candidates.Count == 0)
                return;

            candidates.Sort((a, b) => a.Value.lastAccessTime.CompareTo(b.Value.lastAccessTime));

            int toEvict = Math.Min(Math.Min(count, candidates.Count), _maxEvictPerTick);

            for (int i = 0; i < toEvict; i++)
            {
                _cache.Remove(candidates[i].Key);
                _evictedCount++;
            }

            if (toEvict > 0)
            {
                // 解除我们的引用后，让 Unity 回收「未引用资源」；不 yield 等它完成（协程会打破「GameRoot 唯一驱动」），限流靠「单帧最多淘汰 8 条 ＋ 只在真的淘汰后触发」。
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>切场景时调用（<c>AssetModule.OnSceneSwitch</c>）：上一场景的缓存不再享受冷却期保护，可被立即淘汰。</summary>
        /// <remarks>边界：isPreloaded 不动；refCount &gt; 0 不动（不强制释放）；本方法不删任何条目。</remarks>
        public void OnSceneSwitch()
        {
            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.isPreloaded) continue;
                if (entry.refCount > 0) continue;

                entry.cooldownUntil = 0f;
                entry.canEvict = true;
            }
        }
    }
}
