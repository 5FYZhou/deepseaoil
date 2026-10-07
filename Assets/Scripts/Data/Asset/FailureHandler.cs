using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 失败处理：降级资源注册 + 失败记录；RegisterFallback 由业务代码启动时调用，其余由 AssetModule 的 onFail 调用。
    /// 不参与重试（重试在 LoadScheduler.HandleFailure）；不抛异常，只记录；不创建 Unity 资源（降级资源由业务代码注册）。
    /// </summary>
    internal sealed class FailureHandler
    {
        private const int MAX_RECENT_RECORDS = 32;

        private readonly Dictionary<Type, UnityEngine.Object> _fallbacks =
            new Dictionary<Type, UnityEngine.Object>();
        private readonly List<FailureRecord> _recentFailures = new List<FailureRecord>();
        private int _failedCount;

        public int FailedCount => _failedCount;
        public IReadOnlyList<FailureRecord> RecentFailures => _recentFailures;

        public void RegisterFallback<T>(T fallback) where T : UnityEngine.Object
        {
            if (fallback == null)
            {
                Debug.LogWarning($"[Asset] RegisterFallback<{typeof(T).Name}> ignored: null");
                return;
            }

            _fallbacks[typeof(T)] = fallback;
        }

        /// <summary>
        /// 未注册返回 null，调用方需处理 null（例如完全放弃显示）。
        /// </summary>
        public T GetFallback<T>() where T : UnityEngine.Object
        {
            return _fallbacks.TryGetValue(typeof(T), out var fb) ? fb as T : null;
        }

        public void RecordFailure(string key, string reason)
        {
            _failedCount++;
            _recentFailures.Add(new FailureRecord
            {
                key = key,
                reason = reason,
                time = Time.realtimeSinceStartup,
            });

            if (_recentFailures.Count > MAX_RECENT_RECORDS)
                _recentFailures.RemoveAt(0);

            Debug.LogError($"[Asset] load failed: {key} | {reason}");
        }
    }

    internal struct FailureRecord
    {
        public string key;
        public string reason;
        public float time;
    }
}
