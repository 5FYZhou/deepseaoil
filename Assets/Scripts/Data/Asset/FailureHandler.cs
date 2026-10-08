using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>降级资源注册与失败记录</summary>
    /// <remarks>RegisterFallback 由业务调用，其余经 onFail</remarks>
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

        /// <summary>未注册返回 null</summary>
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
