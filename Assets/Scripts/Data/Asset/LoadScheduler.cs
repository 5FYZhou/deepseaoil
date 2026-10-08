using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>加载调度器：并发 + 队列</summary>
    /// <remarks>Enqueue 在 AssetModule.LoadAsync 未命中时调。主线程独占，用 int 计数；重试立即重新入队，耗尽后交 AssetModule 降级</remarks>
    internal sealed class LoadScheduler
    {
        private const int MAX_RETRY = 2;

        private readonly AssetRegistry _registry;
        private readonly int _maxConcurrent;
        private readonly Queue<LoadRequest> _queue = new Queue<LoadRequest>();

        private int _loadingCount;
        private int _completedCount;
        private int _failedCount;

        // 供 DataMetrics
        public int LoadingCount => _loadingCount;
        public int QueuedCount => _queue.Count;
        public int CompletedCount => _completedCount;
        public int FailedCount => _failedCount;

        public LoadScheduler(AssetRegistry registry, int maxConcurrent)
        {
            _registry = registry;
            _maxConcurrent = maxConcurrent;
        }

        /// <summary>入队，调用方 AssetModule</summary>
        public void Enqueue(LoadRequest req) => _queue.Enqueue(req);

        /// <summary>每帧推进，单帧只启动到并发上限</summary>
        public void Tick(float dt)
        {
            while (_queue.Count > 0 && _loadingCount < _maxConcurrent)
                StartLoad(_queue.Dequeue());
        }

        private void StartLoad(LoadRequest req)
        {
            _loadingCount++;

            string path = _registry.ResolvePath(req.key);

            // 版本留口：切 Addressables 时换 API
            var request = Resources.LoadAsync(path, req.type);
            request.completed += _ => OnLoadCompleted(req, request);
        }

        private void OnLoadCompleted(LoadRequest req, ResourceRequest request)
        {
            _loadingCount--;

            var asset = request.asset;

            if (asset == null)
            {
                HandleFailure(req, "asset not found: " + _registry.ResolvePath(req.key));
                return;
            }

            if (!_registry.IsTypeMatch(req.type, asset))
            {
                HandleFailure(req, $"type mismatch: expected {req.type.Name}, got {asset.GetType().Name}");
                return;
            }

            _completedCount++;
            req.onDone?.Invoke(asset);
        }

        private void HandleFailure(LoadRequest req, string reason)
        {
            req.retryCount++;

            if (req.retryCount <= MAX_RETRY)
            {
                _queue.Enqueue(req);
                return;
            }

            _failedCount++;
            req.onFail?.Invoke(reason);
        }

        /// <summary>清空队列</summary>
        public void Clear() => _queue.Clear();
    }

    internal sealed class LoadRequest
    {
        public string key;
        public Type type;
        public Action<UnityEngine.Object> onDone;
        public Action<string> onFail;
        public int retryCount;
    }
}
