using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Presentation.Effects.Drivers;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 特效模块。<b>纯工具</b>：谁调都行——它不认识 EventBus，不认识 Logic，不认识白模。
    /// </summary>
    /// <remarks>
    /// <para><b>触发路径（写进契约）</b>：Logic 发布 <c>EventBus&lt;EnemyHit&gt;</c> →
    /// 表现层组件订阅 → 组件自己决定 <c>EffectModule.Play(...)</c>（或者什么都不播）。
    /// 「何时播」永远在调用方，删一个特效只改调用方那一处。</para>
    ///
    /// <para><b>生命周期（由 GameRoot 驱动，顺序不能反）</b>：</para>
    /// <list type="number">
    /// <item><c>GameRoot.Awake</c>：<c>AssetModule.Init()</c> 之后 → <see cref="Init"/> → <see cref="Preload"/></item>
    /// <item><c>GameRoot.Update</c>：<c>AssetModule.Tick</c> 之后 → <see cref="Tick"/></item>
    /// <item>切场景（<c>RequestChangeScene</c>）：<see cref="CleanAll"/>，在 <c>LoadScene</c> 之前</item>
    /// <item><c>GameRoot.OnDestroy</c>：<see cref="Dispose"/>，在 <c>AssetModule.Dispose()</c> <b>之前</b>（要经它归还引用计数）</item>
    /// </list>
    ///
    /// <para><b>线程约束</b>：仅主线程，无锁（与 AssetModule 一致）。</para>
    ///
    /// <para><b>所有失败都不抛异常</b>：未 Init、未注册、资源缺失、池满，一律 LogError / LogWarning
    /// 并返回 <see cref="EffectHandle.None"/>——特效不该阻塞游戏。
    /// 唯一的例外是装配错误（<c>Init</c> 调两次），那是必须被看见的。</para>
    /// </remarks>
    public static class EffectModule
    {
        // ─────────────────────────────────────────────
        // 常量：改动集中在此
        // ─────────────────────────────────────────────

        /// <summary>
        /// 启动时<b>同步</b>预加载的特效清单。
        /// </summary>
        /// <remarks>
        /// 进这里 = 启动多阻塞一点，换 Play 时零延迟；不进这里 = 首次 Play 走异步懒加载（有延迟，
        /// 但第二次起就是缓存）。
        /// <b>调优入口</b>：等 Profiler 有数据后，把「几乎每局都会播的」留在这里，把「低频的」移出去。
        /// 当前内容理由：命中火花与水球泥浆是白模里每一次投掷都会触发的两个。
        /// </remarks>
        public static readonly EffectId[] PreloadList =
        {
            EffectId.HitSpark,
            EffectId.MudSplash,
        };

        /// <summary>单个特效在懒加载期间最多排队多少次 Play。超了丢最老的（迟到的特效比不播更糟）。</summary>
        private const int MaxQueuedPlaysPerEffect = 8;

        private const string RootName = "[Effects]";

        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────

        private static readonly Dictionary<EffectId, IEffectDriver> _drivers = new Dictionary<EffectId, IEffectDriver>();

        /// <summary>资源缺失的 EffectId：后续 Play 直接返回 None，不重试。</summary>
        private static readonly HashSet<EffectId> _missing = new HashSet<EffectId>();

        /// <summary>在途的懒加载。</summary>
        private static readonly Dictionary<EffectId, AsyncHandle<GameObject>> _loads =
            new Dictionary<EffectId, AsyncHandle<GameObject>>();

        /// <summary>懒加载期间排队的 Play 请求（资源到位后重放）。</summary>
        private static readonly Dictionary<EffectId, List<EffectContext>> _pendingPlays =
            new Dictionary<EffectId, List<EffectContext>>();

        /// <summary>本模块 Retain 过的资源 Key，Dispose 时成对 Release。</summary>
        private static readonly HashSet<string> _retainedKeys = new HashSet<string>();

        /// <summary>轮询懒加载用的复用缓冲（避免每帧临时数组）。</summary>
        private static readonly List<EffectId> _loadPollScratch = new List<EffectId>();

        private static GameObject _root;
        private static bool _initialized;

        /// <summary>是否已 Init。</summary>
        public static bool IsInitialized => _initialized;

        /// <summary>特效实例的父物体（运行期创建，DontDestroyOnLoad）。未 Init 时为 null。</summary>
        internal static Transform Root => _root != null ? _root.transform : null;

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化：建特效根 + 按 <see cref="EffectCatalog"/> 装配全部驱动。
        /// </summary>
        /// <remarks>
        /// 调用方：<c>GameRoot.Awake</c>，<b>必须</b>在 <c>AssetModule.Init()</c> 之后（<see cref="Preload"/> 依赖它）。
        /// 边界：重复调用只 LogError 并返回（不重置已有驱动）；不做任何资源 IO（那是 <see cref="Preload"/> 的事）。
        /// </remarks>
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

            // 先置位：下面的 Register 走的是"正常路径"，不该被自己的守卫拦下
            _initialized = true;

            _root = new GameObject(RootName);
            if (Application.isPlaying)
            {
                // 编辑模式（含 EditMode 测试）下 DontDestroyOnLoad 无意义且会打日志，跳过
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

        /// <summary>
        /// 同步预加载 <see cref="PreloadList"/>。调用方：<c>GameRoot.Awake</c>，紧随 <see cref="Init"/>。
        /// </summary>
        /// <remarks>
        /// 边界：单个资源缺失只 LogError 并标记该 EffectId 不可用（<b>不阻止游戏启动</b>）；
        /// 已就位的驱动会跳过（幂等）；资源缺失的驱动也会跳过（不重试）。
        /// </remarks>
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

        /// <summary>
        /// 清空所有活跃实例（全部归还各自的池），并作废排队中的 Play 请求。调用方：切场景之前。
        /// </summary>
        /// <remarks>
        /// 名字用 CleanAll 而不是 StopAll，避免与 <see cref="Stop"/> 混淆。
        /// 在途的懒加载<b>不</b>取消：它完成后驱动就绪，资源引用由 <see cref="Dispose"/> 统一归还，
        /// 强行丢弃句柄会让 AssetModule 的引用计数对不上。
        /// </remarks>
        public static void CleanAll()
        {
            if (!_initialized) return;

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                kv.Value?.CleanAll();
            }

            _pendingPlays.Clear();
        }

        /// <summary>
        /// 每帧推进。调用方：<c>GameRoot.Update</c>（用 <c>Time.deltaTime</c>，暂停时自然冻结）。
        /// </summary>
        /// <remarks>边界：未 Init 时 no-op，不抛异常（装配顺序出错不该炸掉整帧）。</remarks>
        public static void Tick(float dt)
        {
            if (!_initialized) return;

            if (_loads.Count > 0) PollPendingLoads();

            foreach (KeyValuePair<EffectId, IEffectDriver> kv in _drivers)
            {
                kv.Value?.Tick(dt);
            }
        }

        /// <summary>
        /// 进程/场景退出：清实例 → 释放驱动 → 销毁特效根 → 归还资源引用计数。
        /// 调用方：<c>GameRoot.OnDestroy</c>，<b>必须</b>在 <c>AssetModule.Dispose()</c> 之前。
        /// </summary>
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

        // ─────────────────────────────────────────────
        // 对外接口
        // ─────────────────────────────────────────────

        /// <summary>
        /// 播放一次特效。
        /// </summary>
        /// <remarks>
        /// 失败（未 Init / 未注册 / 资源缺失 / 池满 / 资源还在加载）一律返回 <see cref="EffectHandle.None"/>。
        /// <b>资源还在加载时也会"记下这次请求"</b>，资源到位后自动补播——这是懒加载路径的契约，
        /// 调用方无需关心。
        /// </remarks>
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

        // ─────────────────────────────────────────────
        // 注册
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注册（或覆盖）一个 EffectId 的驱动。调用方：<see cref="Init"/>，以及需要自建驱动的装配代码 / 测试。
        /// </summary>
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

        // ─────────────────────────────────────────────
        // 内部：资源
        // ─────────────────────────────────────────────

        /// <summary>同步窄路预加载：成功则 Retain 并交给驱动，失败则标记不可用并继续。</summary>
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

        /// <summary>发起懒加载（同一 EffectId 只发一次，重复 Play 合并到同一请求）。</summary>
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

        /// <summary>轮询在途懒加载：完成的取出处理（成功 → 交资源 + 重放排队请求；失败 → 标记不可用）。</summary>
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

        /// <summary>懒加载期间排队的请求：资源到位后逐个补播。</summary>
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
