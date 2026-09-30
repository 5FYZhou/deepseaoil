using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 生命周期管理。冷却期标记 + LRU 淘汰 + 淘汰后的资源回收。
    /// 调用时机：Tick 由 GameRoot 每帧驱动；OnSceneSwitch 由 SceneService 切场景前调用。
    /// 边界：
    ///   - 主线程独占
    ///   - isPreloaded = true 的条目永不淘汰
    ///   - 单帧最多淘汰 _maxEvictPerTick 条，避免卡帧
    ///   - 不修改 refCount；不删除 refCount &gt; 0 的条目
    /// </summary>
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

        /// <summary>
        /// 每帧推进。两阶段：先标记冷却期到期，再按需 LRU 淘汰。
        /// 分离理由：标记是每帧全遍历（O(N)），淘汰只在超阈值时发生。
        /// </summary>
        public void Tick(float dt)
        {
            float now = Time.realtimeSinceStartup;

            // 阶段 1：冷却期到期 → 标记 canEvict（只标记，不删除）
            foreach (var kv in _cache.AllEntries)
            {
                var entry = kv.Value;

                if (entry.refCount > 0) continue;
                if (entry.isPreloaded) continue;
                if (entry.canEvict) continue;
                if (now < entry.cooldownUntil) continue;

                entry.canEvict = true;
            }

            // 阶段 2：超阈值 → 淘汰最久未访问的
            int overflow = _cache.Count - _maxEntries;
            if (overflow > 0)
                EvictLRU(overflow);
        }

        /// <summary>
        /// 按 LRU 淘汰。调用方：Tick 阶段 2。
        /// 边界：只淘汰 canEvict && !isPreloaded；单次最多 _maxEvictPerTick 个；
        ///       先收集候选再删除（不能在遍历 CacheStore 时直接 Remove）。
        /// </summary>
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
                // D2：解除了我们的引用之后，让 Unity 回收成为「未引用资源」的那部分。
                // 不 yield 等它完成（协程会打破「GameRoot 唯一驱动」）；
                // 靠「单帧最多淘汰 8 条 + 只在真的淘汰后触发」限流。
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// 切场景时调用。调用方：AssetModule.OnSceneSwitch。
        /// 语义：上一场景的缓存不再享受冷却期保护，可被立即淘汰。
        /// 边界：isPreloaded 不动；refCount &gt; 0 不动（不强制释放）；本方法不删任何条目。
        /// </summary>
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
