using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>加载调度器。并发控制 + 等待队列 + 失败重试。</summary>
    /// <remarks>Enqueue 由 <c>AssetModule.LoadAsync</c> 未命中时调用；<c>Tick</c> 由 <c>GameRoot</c> 每帧驱动（顺序表 step ③）。主线程独占，用普通 int 计数（不用 <c>SemaphoreSlim</c>：<c>Wait()</c> 会阻塞主线程，且主线程独占时信号量多余）。
    /// 重试是「立即重新入队」，不延时。失败策略不在这里：重试耗尽后交回 <c>AssetModule</c> 决定降级。</remarks>
    internal sealed class LoadScheduler
    {
        private const int MAX_RETRY = 2;

        private readonly AssetRegistry _registry;
        private readonly int _maxConcurrent;
        private readonly Queue<LoadRequest> _queue = new Queue<LoadRequest>();

        private int _loadingCount;
        private int _completedCount;
        private int _failedCount;

        // 供 DataMetrics 读取
        public int LoadingCount => _loadingCount;
        public int QueuedCount => _queue.Count;
        public int CompletedCount => _completedCount;
        public int FailedCount => _failedCount;

        public LoadScheduler(AssetRegistry registry, int maxConcurrent)
        {
            _registry = registry;
            _maxConcurrent = maxConcurrent;
        }

        /// <summary>入队。调用方：AssetModule.LoadAsync 未命中缓存、AssetModule.Preload。</summary>
        public void Enqueue(LoadRequest req) => _queue.Enqueue(req);

        /// <summary>每帧推进。调用方：GameRoot 顺序表 step ③；边界：单帧最多启动到并发上限为止，不在一帧内爆发。</summary>
        public void Tick(float dt)
        {
            while (_queue.Count > 0 && _loadingCount < _maxConcurrent)
                StartLoad(_queue.Dequeue());
        }

        private void StartLoad(LoadRequest req)
        {
            _loadingCount++;

            string path = _registry.ResolvePath(req.key);

            // 版本留口：切 Addressables 时此处换成 Addressables.LoadAssetAsync
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

        /// <summary>清空队列。调用方：AssetModule.Dispose。边界：已启动的请求无法取消（D3 决策）。</summary>
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
