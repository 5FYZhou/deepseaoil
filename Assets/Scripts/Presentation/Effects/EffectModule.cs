using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Presentation.Effects.Drivers;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>特效模块。<b>纯工具</b>：谁调都行 —— 它不认识 EventBus、不认识 Logic、不认识白模。</summary>
    /// <remarks>触发路径：Logic 发 <c>EventBus&lt;EnemyHit&gt;</c> → 表现层组件订阅 → 组件自己决定 <c>EffectModule.Play(...)</c>；「何时播」永远在调用方。
    /// 生命周期（顺序不能反）：<c>AssetModule.Init</c> → <see cref="Init"/> → <see cref="Preload"/>；切场景 → <see cref="CleanAll"/>（在 <c>LoadScene</c> 之前）；<c>GameRoot.OnDestroy</c> → <see cref="Dispose"/>（在 <c>AssetModule.Dispose</c> <b>之前</b>，要经它归还引用计数）。
    /// <b>所有失败都不抛异常</b>：未 Init、未注册、资源缺失、池满，一律 LogError / LogWarning 并返回 <see cref="EffectHandle.None"/>。唯一例外是装配错误（<c>Init</c> 调两次）。</remarks>
    public static class EffectModule
    {
        /// <summary>启动时<b>同步</b>预加载的清单：进这里 = 启动多阻塞一点换 Play 零延迟，不进 = 首次 Play 走异步懒加载。</summary>
        public static readonly EffectId[] PreloadList =
        {
            EffectId.BurstSparks,
            EffectId.MudSplash,
        };

        /// <summary>单个特效在懒加载期间最多排队多少次 Play。超了丢最老的（迟到的特效比不播更糟）。</summary>
        private const int MaxQueuedPlaysPerEffect = 8;

        private const string RootName = "[Effects]";

        private static readonly Dictionary<EffectId, IEffectDriver> _drivers = new Dictionary<EffectId, IEffectDriver>();

        /// <summary>资源缺失的 EffectId：后续 Play 直接返回 None，不重试。</summary>
        private static readonly HashSet<EffectId> _missing = new HashSet<EffectId>();

        private static readonly Dictionary<EffectId, AsyncHandle<GameObject>> _loads =
            new Dictionary<EffectId, AsyncHandle<GameObject>>();

        private static readonly Dictionary<EffectId, List<EffectContext>> _pendingPlays =
            new Dictionary<EffectId, List<EffectContext>>();

        /// <summary>本模块 Retain 过的资源 Key，Dispose 时成对 Release。</summary>
        private static readonly HashSet<string> _retainedKeys = new HashSet<string>();

        private static readonly List<EffectId> _loadPollScratch = new List<EffectId>();

        private static GameObject _root;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        /// <summary>特效实例的父物体（运行期创建，DontDestroyOnLoad）；未 Init 时为 <c>null</c>。</summary>
        internal static Transform Root => _root != null ? _root.transform : null;

        /// <summary>初始化：建特效根 ＋ 按 <see cref="EffectCatalog"/> 装配全部驱动。</summary>
        /// <remarks>调用方 <c>GameRoot.Awake</c>，必须在 <c>AssetModule.Init()</c> 之后（<see cref="Preload"/> 依赖它）；重复调用只 LogError 并返回（不重置已有驱动），本方法不做资源 IO。</remarks>
        public static void Init()
        {
            if (_initialized)
            {
                Debug.LogError("[Effect] EffectModule.Init 被调用了两次，忽略第二次。");
                return;
            }

            _drivers.Clear();
            _missing.Clear();
            _loads.Clear();
            _pendingPlays.Clear();
            _retainedKeys.Clear();

            _initialized = true;

            _root = new GameObject(RootName);
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(_root);
            }

            IReadOnlyList<EffectSpec> specs = EffectCatalog.All;
            for (int i = 0; i < specs.Count; i++)
            {
                EffectSpec spec = specs[i];
                if (spec.Id == EffectId.None)
                {
                    Debug.LogError("[Effect] EffectCatalog 里出现了 None 行，已跳过。");
                    continue;
                }

                Register(spec.Id, EffectDriverFactory.Create(spec, _root.transform));
            }
        }

        /// <summary>同步预加载 <see cref="PreloadList"/>；调用方 <c>GameRoot.Awake</c>，紧随 <see cref="Init"/>。</summary>
        /// <remarks>单个资源缺失只 LogError 并标记该 EffectId 不可用（<b>不阻止游戏启动</b>）；已就位与资源缺失的驱动都跳过（不重试）。</remarks>
        public static void Preload()
        {
            if (!_initialized)
            {
                Debug.LogError("[Effect] EffectModule.Preload 在 Init 之前被调用，已忽略。");
                return;
            }

            for (int i = 0; i < PreloadList.Length; i++)
            {
                EffectId id = PreloadList[i];
                if (id == EffectId.None) continue;

                if (!_drivers.TryGetValue(id, out IEffectDriver driver) || driver == null)
                {
                    Debug.LogError($"[Effect] PreloadList 里的 {id} 没有对应驱动：EffectCatalog 缺这一行。");
                    continue;
                }

                LoadAssetSync(id, driver);
            }
        }

        /// <summary>清空所有活跃实例（全部归还各自的池），并作废排队中的 Play 请求；调用方：切场景之前。</summary>
        /// <remarks>在途的懒加载<b>不</b>取消：它完成后驱动就绪，资源引用由 <see cref="Dispose"/> 统一归还，强行丢弃句柄会让 AssetModule 的引用计数对不上。</remarks>
        public static void CleanAll()
        {
            if (!_initialized) return;

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                kv.Value?.CleanAll();
            }

            _pendingPlays.Clear();
        }

        /// <summary>每帧推进；调用方 <c>GameRoot.Update</c>（<c>Time.deltaTime</c>，暂停时自然冻结）。未 Init 时 no-op，不抛异常。</summary>
        public static void Tick(float dt)
        {
            if (!_initialized) return;

            if (_loads.Count > 0) PollPendingLoads();

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                kv.Value?.Tick(dt);
            }
        }

        /// <summary>进程/场景退出：清实例 → 释放驱动 → 销毁特效根 → 归还资源引用计数。调用方 <c>GameRoot.OnDestroy</c>，必须在 <c>AssetModule.Dispose()</c> 之前。</summary>
        public static void Dispose()
        {
            if (!_initialized) return;

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                kv.Value?.Dispose();
            }

            _drivers.Clear();
            _loads.Clear();
            _pendingPlays.Clear();
            _missing.Clear();

            DestroyRoot();

            // 顺序要求：这里要经 AssetModule 归还引用计数，所以 AssetModule 必须还活着
            if (_retainedKeys.Count > 0 && AssetModule.IsInitialized)
            {
                foreach (string key in _retainedKeys)
                {
                    AssetModule.Release(key);
                }
            }

            _retainedKeys.Clear();
            _initialized = false;
        }

        /// <summary>播放一次特效；失败（未 Init / 未注册 / 资源缺失 / 池满）一律返回 <see cref="EffectHandle.None"/>，<b>资源还在加载时也会记下这次请求</b>、到位后自动补播。</summary>
        public static EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (!_initialized)
            {
                Debug.LogError("[Effect] EffectModule.Play 在 Init 之前被调用。");
                return EffectHandle.None;
            }

            if (id == EffectId.None) return EffectHandle.None;

            if (!_drivers.TryGetValue(id, out IEffectDriver driver) || driver == null)
            {
                Debug.LogError($"[Effect] EffectId {id} 未注册：EffectCatalog 里没有这一行。");
                return EffectHandle.None;
            }

            if (_missing.Contains(id)) return EffectHandle.None;

            if (!driver.IsAssetReady)
            {
                RequestAssetAsync(id, driver);
                EnqueuePendingPlay(id, in ctx);
                return EffectHandle.None;
            }

            return driver.Play(id, in ctx);
        }

        /// <summary>更新一次<b>已经在播</b>的实例（持续型特效：位置 / 颜色 / 半径）。</summary>
        /// <remarks>返回值只代表"句柄有效"；未 Init、句柄为 <c>None</c>、句柄过期都是安全的 no-op，不报错 —— 不支持的驱动也是 no-op（接口默认实现）。</remarks>
        public static bool Update(EffectHandle handle, in EffectContext ctx)
        {
            if (!_initialized) return false;
            if (!handle.IsValid) return false;

            handle.Driver.UpdateInstance(handle, in ctx);

            return true;
        }

        /// <summary>停止一次播放。句柄无效 / 已过期 / 未 Init 时是 no-op。</summary>
        public static void Stop(EffectHandle handle)
        {
            if (!_initialized) return;
            if (!handle.IsValid) return;

            handle.Driver.Stop(handle);
        }

        /// <summary>调试快照（拉模型）。未 Init 时全 0。</summary>
        public static EffectStats GetStats()
        {
            int active = 0;
            int pooled = 0;

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                IEffectDriver d = kv.Value;
                if (d == null) continue;

                active += d.ActiveInstanceCount;
                pooled += d.PooledObjectCount;
            }

            return new EffectStats(active, _drivers.Count, pooled);
        }

        /// <summary>注册（或覆盖）一个 EffectId 的驱动。调用方 <see cref="Init"/>，以及需要自建驱动的装配代码 / 测试。</summary>
        /// <remarks>必须在 <see cref="Init"/> 之后调用：<c>Init</c> 会清空驱动表。</remarks>
        public static void Register(EffectId id, IEffectDriver driver)
        {
            if (!_initialized)
            {
                Debug.LogError("[Effect] Register 必须在 Init 之后调用（Init 会清空驱动表）。");
                return;
            }

            if (id == EffectId.None)
            {
                Debug.LogError("[Effect] 不能注册 EffectId.None。");
                return;
            }

            if (driver == null)
            {
                Debug.LogError($"[Effect] Register({id}) 的 driver 为 null。");
                return;
            }

            if (_drivers.ContainsKey(id))
            {
                Debug.LogError($"[Effect] {id} 重复注册，后覆盖前。");
            }

            _drivers[id] = driver;
        }

        private static void LoadAssetSync(EffectId id, IEffectDriver driver)
        {
            if (driver.IsAssetReady) return;
            if (_missing.Contains(id)) return;
            if (string.IsNullOrEmpty(driver.AssetKey)) return;

            if (!AssetModule.IsInitialized)
            {
                Debug.LogError($"[Effect] 预加载 {id} 失败：AssetModule 尚未 Init" +
                               "（EffectModule.Init 必须在 AssetModule.Init 之后）。已标记为不可用。");
                _missing.Add(id);
                return;
            }

            GameObject asset = AssetModule.Load<GameObject>(driver.AssetKey);
            if (asset == null)
            {
                Debug.LogError($"[Effect] 特效资源缺失：{id} → 期望 Assets/Resources/{driver.AssetKey}.prefab。" +
                               "该特效后续 Play 一律返回 None（不重试、不阻塞启动）。");
                _missing.Add(id);
                return;
            }

            _retainedKeys.Add(driver.AssetKey);
            driver.OnAssetLoaded(asset);
        }

        private static void RequestAssetAsync(EffectId id, IEffectDriver driver)
        {
            if (_loads.ContainsKey(id)) return;

            if (string.IsNullOrEmpty(driver.AssetKey))
            {
                Debug.LogError($"[Effect] {id} 的驱动报告资源未就位，但 AssetKey 为空。已标记为不可用。");
                _missing.Add(id);
                return;
            }

            if (!AssetModule.IsInitialized)
            {
                Debug.LogError($"[Effect] 懒加载 {id} 失败：AssetModule 尚未 Init。已标记为不可用。");
                _missing.Add(id);
                return;
            }

            _loads[id] = AssetModule.LoadAsync<GameObject>(driver.AssetKey);
        }

        private static void PollPendingLoads()
        {
            _loadPollScratch.Clear();
            foreach (KeyValuePair<EffectId, AsyncHandle<GameObject>> kv in _loads)
            {
                if (kv.Value != null && kv.Value.IsDone) _loadPollScratch.Add(kv.Key);
            }

            for (int i = 0; i < _loadPollScratch.Count; i++)
            {
                EffectId id = _loadPollScratch[i];

                AsyncHandle<GameObject> handle = _loads[id];
                _loads.Remove(id);

                if (!_drivers.TryGetValue(id, out IEffectDriver driver) || driver == null) continue;

                GameObject asset = handle.Asset;
                if (asset == null)
                {
                    Debug.LogError($"[Effect] 特效资源懒加载失败：{id} → {driver.AssetKey}。" +
                                   "该特效后续 Play 一律返回 None（不重试）。");

                    _missing.Add(id);
                    _pendingPlays.Remove(id);
                    continue;
                }

                _retainedKeys.Add(driver.AssetKey);
                driver.OnAssetLoaded(asset);

                ReplayPendingPlays(id, driver);
            }
        }

        private static void EnqueuePendingPlay(EffectId id, in EffectContext ctx)
        {
            if (!_pendingPlays.TryGetValue(id, out List<EffectContext> list))
            {
                list = new List<EffectContext>(4);
                _pendingPlays[id] = list;
            }

            if (list.Count >= MaxQueuedPlaysPerEffect)
            {
                list.RemoveAt(0);
            }

            list.Add(ctx);
        }

        private static void ReplayPendingPlays(EffectId id, IEffectDriver driver)
        {
            if (!_pendingPlays.TryGetValue(id, out List<EffectContext> list)) return;

            _pendingPlays.Remove(id);

            for (int i = 0; i < list.Count; i++)
            {
                EffectContext ctx = list[i];
                driver.Play(id, in ctx);
            }
        }

        private static void DestroyRoot()
        {
            if (_root == null) return;

            if (Application.isPlaying)
            {
                Object.Destroy(_root);
            }
            else
            {
                // 编辑模式（EditMode 测试）下 Object.Destroy 会打"may not be called from edit mode"警告且延迟生效
                Object.DestroyImmediate(_root);
            }

            _root = null;
        }
    }
}
