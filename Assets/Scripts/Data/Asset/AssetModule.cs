using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 资源模块。Data 层的资源查询与生命周期管理入口。
    /// 调用时机：
    ///   Init          — GameRoot.Awake，紧随 ConfigModule.Init 之后
    ///   Tick          — GameRoot 顺序表 step ②，每帧
    ///   OnSceneSwitch — SceneService.Load（必须在 LoadScene 之前）
    ///   Dispose       — GameRoot.OnDestroy
    ///   Load / LoadAsync / TryGet / Release / Preload / RegisterFallback — 调用方任意时机
    /// 边界：
    ///   - 所有方法仅主线程调用（Unity 资源 API 限制）
    ///   - 不发布/不订阅任何事件（观测走 DataMetrics 拉模型）
    ///   - 不感知调用方身份与业务语义；不知道 Luban 存在
    ///   - Init 失败（重复调用）直接抛异常：装配错误应当在启动时暴露
    /// </summary>
    public static class AssetModule
    {
        // ── 常量：改动集中在此 ──
        private const float COOLDOWN_SECONDS    = 60f;
        private const int   MAX_CACHE_ENTRIES   = 100;
        private const int   MAX_CONCURRENT_LOAD = 4;
        private const int   MAX_EVICT_PER_TICK  = 8;

        // ── 微块实例 ──
        private static AssetRegistry  _registry;
        private static CacheStore     _cache;
        private static RefCounter     _refCounter;
        private static LoadScheduler  _scheduler;
        private static LifecycleMgr   _lifecycle;
        private static FailureHandler _failure;

        // ── D1：同一 Key 并发请求合并 ──
        private static readonly Dictionary<string, List<Action<UnityEngine.Object>>> _pendingLoads =
            new Dictionary<string, List<Action<UnityEngine.Object>>>();

        // ── 统计（仅计数，不参与业务逻辑）──
        private static int _cacheHits;
        private static int _cacheMisses;

        private static bool _initialized;

        // 供 DataMetrics 只读访问（同程序集内可见）
        internal static bool IsInitialized => _initialized;
        internal static CacheStore Cache => _cache;
        internal static LoadScheduler Scheduler => _scheduler;
        internal static LifecycleMgr Lifecycle => _lifecycle;
        internal static FailureHandler Failure => _failure;
        internal static int CacheHits => _cacheHits;
        internal static int CacheMisses => _cacheMisses;

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化。调用方：GameRoot.Awake，必须在 ConfigModule.Init 之后。
        /// 边界：重复调用抛异常；不创建任何 Unity 资源（降级资源由业务注册）。
        /// </summary>
        public static void Init()
        {
            if (_initialized)
                throw new InvalidOperationException("[Asset] AssetModule.Init called twice");

            _registry   = new AssetRegistry();
            _cache      = new CacheStore();
            _refCounter = new RefCounter(_cache, COOLDOWN_SECONDS);
            _scheduler  = new LoadScheduler(_registry, MAX_CONCURRENT_LOAD);
            _lifecycle  = new LifecycleMgr(_cache, COOLDOWN_SECONDS, MAX_CACHE_ENTRIES, MAX_EVICT_PER_TICK);
            _failure    = new FailureHandler();

            _pendingLoads.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            _initialized = true;
        }

        /// <summary>
        /// 每帧推进。调用方：GameRoot 顺序表 step ②。
        /// 边界：未 Init 时 no-op 不抛异常（防装配顺序出错时整帧炸掉）。
        ///       只驱动 Scheduler 与 Lifecycle；FailureHandler 不需要每帧推进（重试在 Scheduler 内）。
        /// </summary>
        public static void Tick(float dt)
        {
            if (!_initialized) return;

            _scheduler.Tick(dt);
            _lifecycle.Tick(dt);
        }

        /// <summary>
        /// 切场景时调用。调用方：SceneService.Load（必须在 LoadScene 之前）。
        /// 边界：不清挂起请求、不动合并列表（可能是新场景的预加载）；
        ///       不强制释放 refCount &gt; 0 的资源；保留 isPreloaded 条目。
        /// </summary>
        public static void OnSceneSwitch()
        {
            if (!_initialized) return;

            _lifecycle.OnSceneSwitch();
        }

        /// <summary>进程退出时调用。调用方：GameRoot.OnDestroy。</summary>
        public static void Dispose()
        {
            if (!_initialized) return;

            _scheduler.Clear();
            _cache.Clear();
            _pendingLoads.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            _initialized = false;
        }

        // ─────────────────────────────────────────────
        // 主路径：异步加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 异步加载资源。调用方：任何层（Logic 或 Presentation）。
        /// 边界：
        ///   - 命中缓存：refCount++，返回**已完成**句柄（await 不挂起）
        ///   - 未命中：入队并返回未完成句柄，由 Tick 推进；同一 Key 的并发请求合并为一个 IO
        ///   - 加载成功：Put 缓存 → Retain → Complete（顺序严格，见 Docs/分层设计/数据层.md §2「主路径语义」）
        ///   - 失败：重试 2 次后返回降级资源（可能为 null）
        ///   - 调用方拿到句柄后必须成对调用 Release
        /// </summary>
        public static AsyncHandle<T> LoadAsync<T>(string key) where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] LoadAsync before Init");

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[Asset] LoadAsync with empty key");
                return AsyncHandle<T>.Completed(null);
            }

            // ── 命中缓存 ──
            if (_cache.TryGet<T>(key, out var entry))
            {
                _cacheHits++;
                _cache.Touch(key);
                _refCounter.Retain(key);
                return AsyncHandle<T>.Completed(entry.asset as T);
            }

            _cacheMisses++;

            // ── D1：同一 Key 已在加载中 → 合并，不重复入队 ──
            if (_pendingLoads.TryGetValue(key, out var waiters))
            {
                var merged = AsyncHandle<T>.Create();
                waiters.Add(asset => merged.Complete(asset as T));
                return merged;
            }

            // ── 未命中且无在途请求：入队 ──
            var handle = AsyncHandle<T>.Create();
            var list = new List<Action<UnityEngine.Object>> { asset => handle.Complete(asset as T) };
            _pendingLoads[key] = list;

            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(T),
                onDone = asset =>
                {
                    // 顺序严格：先写缓存 → 再 Retain → 最后 Complete
                    // 否则调用方 await 后立即 TryGet/Release 会撞上「句柄已 resolve 但缓存未写入」的窗口
                    _cache.Put(key, asset, isPreloaded: false);
                    _refCounter.Retain(key);
                    DispatchPending(key, asset);
                },
                onFail = reason =>
                {
                    // 重试已用尽：记录 + 降级。fallback 可能为 null，调用方需处理
                    _failure.RecordFailure(key, reason);
                    DispatchPending(key, _failure.GetFallback<T>());
                },
            });

            return handle;
        }

        // ─────────────────────────────────────────────
        // 同步加载（ResMgr.Load<T> 的平替路径）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 同步加载资源。调用方：需要"当场拿到"且体量确定很小的资源（UI 基建 prefab 等）。
        /// 边界：
        ///   - **阻塞主线程**：只用于小资源，不要用于面板/场景级资源
        ///   - 命中缓存：refCount++，直接返回（与 LoadAsync 命中路径一致）
        ///   - 未命中：Resources.Load 同步 IO → Put 缓存 → Retain
        ///   - 失败：记一次失败并返回降级资源（可能为 null）；**不走重试**（重试属于异步路径）
        ///   - 与 LoadAsync 共用同一份缓存与引用计数，同一 Key 可混用两种方式
        /// </summary>
        public static T Load<T>(string key) where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] Load before Init");

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[Asset] Load with empty key");
                return null;
            }

            if (_cache.TryGet<T>(key, out var entry))
            {
                _cacheHits++;
                _cache.Touch(key);
                _refCounter.Retain(key);
                return entry.asset as T;
            }

            _cacheMisses++;

            var asset = Resources.Load<T>(_registry.ResolvePath(key));
            if (asset == null)
            {
                _failure.RecordFailure(key, "sync load returned null");
                return _failure.GetFallback<T>();
            }

            // 顺序与异步路径一致：先写缓存 → 再 Retain → 最后交给调用方
            _cache.Put(key, asset, isPreloaded: false);
            _refCounter.Retain(key);
            return asset;
        }

        // ─────────────────────────────────────────────
        // 观测路径：查缓存（不触发加载）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 尝试从缓存取资源。调用方：任何层，用于避免不必要的异步开销。
        /// 边界：不触发加载、不改变引用计数、命中时更新访问时间。
        /// </summary>
        public static bool TryGet<T>(string key, out T asset) where T : UnityEngine.Object
        {
            asset = null;

            if (!_initialized || string.IsNullOrEmpty(key))
                return false;

            if (_cache.TryGet<T>(key, out var entry))
            {
                _cache.Touch(key);
                asset = entry.asset as T;
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        // 释放
        // ─────────────────────────────────────────────

        /// <summary>
        /// 释放一次引用。调用方：LoadAsync 的持有者。
        /// 边界：未知 Key / 重复释放记警告不抛异常；refCount 归零后进冷却期，不立即卸载。
        /// </summary>
        public static void Release(string key)
        {
            if (!_initialized || string.IsNullOrEmpty(key))
                return;

            if (!_refCounter.Release(key))
                Debug.LogWarning($"[Asset] Release unknown or over-released key: {key}");
        }

        // ─────────────────────────────────────────────
        // 预加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 预加载资源并标记为常驻。
        /// 调用方：Loading 阶段 / 启动阶段。
        /// 边界：
        ///   - 走独立轻量路径，不创建 AsyncHandle（没有调用方在 await）
        ///   - 完成后 isPreloaded = true，永不淘汰；不计入引用计数
        ///   - 用 typeof(UnityEngine.Object) 做类型，不约束具体类型
        ///     已知待验证项：Resources.LoadAsync 传基类时类型过滤是否生效（见 Docs/框架蓝图.md §9.1 开放项 O13）
        /// </summary>
        public static void Preload(string key)
        {
            if (!_initialized || string.IsNullOrEmpty(key))
                return;

            // 已在缓存：直接升格为预加载
            if (_cache.TryGetEntry(key, out var existing))
            {
                existing.isPreloaded = true;
                existing.canEvict = false;
                existing.cooldownUntil = 0f;
                return;
            }

            // 在途：挂一个升格回调，不重复入队
            if (_pendingLoads.TryGetValue(key, out var waiters))
            {
                waiters.Add(asset =>
                {
                    if (_cache.TryGetEntry(key, out var e))
                    {
                        e.isPreloaded = true;
                        e.canEvict = false;
                    }
                });
                return;
            }

            var list = new List<Action<UnityEngine.Object>>
            {
                asset => _cache.Put(key, asset, isPreloaded: true)
            };
            _pendingLoads[key] = list;

            _scheduler.Enqueue(new LoadRequest
            {
                key = key,
                type = typeof(UnityEngine.Object),
                onDone = asset => DispatchPending(key, asset),
                onFail = reason =>
                {
                    _failure.RecordFailure(key, reason);
                    _pendingLoads.Remove(key);   // 预加载失败：丢弃合并列表
                },
            });
        }

        // ─────────────────────────────────────────────
        // 降级资源注册（转发给 FailureHandler）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注册降级资源。调用方：业务代码启动时。
        /// 边界：Data 层不创建资源，由业务传入（URP 下占位 Sprite 需 Sprite.Create，属于业务细节）。
        /// </summary>
        public static void RegisterFallback<T>(T fallback) where T : UnityEngine.Object
        {
            if (!_initialized)
                throw new InvalidOperationException("[Asset] RegisterFallback before Init");

            _failure.RegisterFallback(fallback);
        }

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        /// <summary>把结果分发给同一 Key 的所有等待者，然后清掉合并列表。</summary>
        private static void DispatchPending(string key, UnityEngine.Object asset)
        {
            if (!_pendingLoads.TryGetValue(key, out var waiters))
                return;

            _pendingLoads.Remove(key);

            for (int i = 0; i < waiters.Count; i++)
                waiters[i]?.Invoke(asset);
        }
    }
}
