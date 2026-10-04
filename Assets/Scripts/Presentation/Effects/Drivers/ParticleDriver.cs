using System.Collections.Generic;
using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 粒子驱动：管理一个 <see cref="EffectId"/> 对应粒子预制体的播放、回收与池化。
    /// </summary>
    /// <remarks>
    /// <para><b>语义</b>：单例型（<see cref="IsSingleton"/>）重复 <c>Play</c> 时合并到当前实例
    /// （取最大强度，<b>不</b>重置计时）；多实例型每次都新建一个。</para>
    ///
    /// <para><b>支持复合预制体</b>：一次 <c>Play</c> 会驱动实例<b>全部</b>
    /// <c>ParticleSystem</c>（含未激活的子物体）。Unity 里"一个特效 = 父物体 + 几个子粒子系统"
    /// 是最常见的搭法，只认根上的那一个会让这类预制体直接报"没有 ParticleSystem"。</para>
    ///
    /// <para><b>池满策略</b>：<c>CreateOrDrop</c>——空闲区空了就现场实例化（不超过 <c>maxSize</c>），
    /// 到上限就丢弃本次请求 + 节流 LogWarning。<b>不</b>用 <c>DropSilently</c>：
    /// 那个策略在"没预热"时会把第一次 Play 也丢掉（一个永远播不出来的特效最难查）。</para>
    ///
    /// <para><b>回收判定</b>：估算时长（各自 <c>duration + startLifetime</c> 的最大值）到点后，
    /// 才向引擎问一次 <c>IsAlive(false)</c>；还活着就继续留，直到超过 <c>+5s</c> 强制回收并警告一次。
    /// 之所以不是每帧问 <c>IsAlive</c>：它会遍历粒子，是文档点名过的性能坑。
    /// 之所以不是纯计时：<c>startLifetime.constantMax</c> 只在 Constant / TwoConstants 模式下有效，
    /// Curve 模式下取不到峰值，纯计时会提前掐断特效。</para>
    ///
    /// <para><b>位置与缩放</b>：写世界坐标（特效根在原点，两者等价），<b>保留</b>预制体自带的 z；
    /// 缩放按 <c>预制体缩放 × ctx.Scale</c>，不会把作者调的缩放抹掉。
    /// <b>朝向</b>：本驱动<b>不</b>按 <c>ctx.Direction</c> 旋转——粒子是不是有向的由作者决定，
    /// 强加一个旋转约定会把径向火花也转歪。需要时可在这里加一行
    /// <c>Quaternion.FromToRotation(Vector3.right, dir)</c>。</para>
    ///
    /// <para><b>池对象不逐个 Destroy</b>：实例都是特效根的子物体，销毁根即回收；
    /// 逐对象 <c>Object.Destroy</c> 在编辑模式下会打警告，也会和根销毁重复。</para>
    /// </remarks>
    public sealed class ParticleDriver : IEffectDriver
    {
        /// <summary>
        /// Intensity = 0 时粒子量与大小的缩放，<b>相对预制体作者值</b>（Intensity = 1 时 = 原样播）。
        /// </summary>
        private const float MinIntensityScale = 0.4f;

        /// <summary>估算时长之后最多再等多久就强制回收（防"粒子寿命无限"把池位占死）。</summary>
        private const float MaxLifeOverrun = 5f;

        /// <summary>池满警告的最小间隔（秒），避免刷屏。</summary>
        private const float DropWarnInterval = 1f;

        // ── 装配参数（构造后不变）──
        private readonly string _assetKey;
        private readonly string _tag;
        private readonly bool _isSingleton;
        private readonly Transform _root;
        private readonly int _maxSize;
        private readonly int _prewarm;

        // ── 运行期状态 ──
        private readonly Dictionary<int, ParticleInstance> _active = new Dictionary<int, ParticleInstance>();
        private readonly List<int> _recycleScratch = new List<int>();

        private GameObject _prefab;
        private Vector3 _prefabScale = Vector3.one;

        /// <summary>
        /// 预制体上<b>作者授权</b>的乘数，按 <c>GetComponentsInChildren</c> 的顺序与实例一一对应。
        /// </summary>
        /// <remarks>
        /// <b>为什么必须存下来</b>：<c>startSizeMultiplier</c> / <c>rateOverTimeMultiplier</c> 是
        /// <b>作者的值</b>，不是"驱动自己的旋钮"。直接写 <c>= k</c> 会把作者在
        /// Curve / Random Between Two Curves 模式下填的 "Multiplier" 抹成 1 ——
        /// 表现为"预制体里 Start Size 明明设好了，进游戏却是默认大小"（作者值住在乘数里时）。
        /// 正确做法是<b>只做相对缩放</b>：<c>作者值 × k</c>。
        /// </remarks>
        private float[] _authoredSizeMul;
        private float[] _authoredRateMul;
        private bool _warnedAuthoredMismatch;
        private Pool<GameObject> _pool;
        private ParticleInstance _singleton;
        private int _nextInstanceId;
        private int _epoch = 1;

        private int _droppedSinceWarn;
        private float _nextDropWarnTime;
        private bool _warnedNoParticleSystem;
        private bool _warnedLifeOverrun;
        private bool _disposed;

        /// <param name="prefab">粒子预制体。懒加载路径下先传 null，等 <see cref="OnAssetLoaded"/>。</param>
        /// <param name="root">实例的父物体（特效根）。</param>
        /// <param name="isSingleton">是否单例型。</param>
        /// <param name="maxSize">池上限（同时存在的实例数上限）。</param>
        /// <param name="prewarm">预热数。</param>
        /// <param name="assetKey">资源 Key，只用于日志。</param>
        public ParticleDriver(
            GameObject prefab,
            Transform root,
            bool isSingleton = false,
            int maxSize = 16,
            int prewarm = 0,
            string assetKey = null)
        {
            _root = root;
            _isSingleton = isSingleton;
            _maxSize = maxSize > 0 ? maxSize : 1;
            _prewarm = prewarm < 0 ? 0 : prewarm;
            _assetKey = assetKey;

            string name = prefab != null ? prefab.name : assetKey;
            _tag = "ParticleDriver[" + (string.IsNullOrEmpty(name) ? "?" : name) + "]";

            if (prefab != null) BuildPool(prefab);
        }

        /// <summary>由 <see cref="EffectDriverFactory"/> 调用：从装配表建驱动（资源稍后到位）。</summary>
        internal ParticleDriver(in EffectSpec spec, Transform root)
            : this(null, root, spec.IsSingleton, spec.MaxSize, spec.Prewarm, spec.Key)
        {
        }

        // ─────────────────────────────────────────────
        // IEffectDriver
        // ─────────────────────────────────────────────

        public bool IsSingleton => _isSingleton;

        public string AssetKey => _assetKey;

        /// <summary>池建好了 = 资源到位了。</summary>
        public bool IsAssetReady => _pool != null;

        public int ActiveInstanceCount => _active.Count;

        public int PooledObjectCount => _pool != null ? _pool.IdleCount : 0;

        /// <summary>资源到位：拿到预制体，建池并按 <c>prewarm</c> 预热。幂等。</summary>
        public void OnAssetLoaded(Object asset)
        {
            if (_disposed || _pool != null) return;

            var prefab = asset as GameObject;
            if (prefab == null)
            {
                Debug.LogError($"{_tag} 拿到的资源不是 GameObject（key={_assetKey}），该特效不可用。");
                return;
            }

            BuildPool(prefab);
        }

        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            if (_pool == null)
            {
                // EffectModule 会先拦「资源未就位」，走到这里说明装配方式不对
                Debug.LogError($"{_tag} 资源未就位就 Play 了 {id}。");
                return EffectHandle.None;
            }

            // 单例：已在播 → 合并（取最大强度，不重置计时）
            if (_isSingleton && _singleton != null)
            {
                if (ctx.Intensity > _singleton.Intensity) _singleton.Intensity = ctx.Intensity;
                return HandleOf(_singleton);
            }

            if (!_pool.TryGet(out GameObject go))
            {
                WarnPoolFull(id);
                return EffectHandle.None;
            }

            if (go == null)
            {
                Debug.LogError($"{_tag} 池返回了 null 对象（factory 有问题）。");
                return EffectHandle.None;
            }

            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                if (!_warnedNoParticleSystem)
                {
                    _warnedNoParticleSystem = true;
                    Debug.LogError($"{_tag} 预制体（含子物体）上没有 ParticleSystem，{id} 无法播放。" +
                                   $"请检查 Assets/Resources/{_assetKey}.prefab。本驱动只报一次。");
                }

                _pool.Release(go);
                return EffectHandle.None;
            }

            ApplyPlacement(go, in ctx);
            ApplyIntensity(systems, ctx.Intensity);

            // 池化复用：先清掉上一轮的残留粒子，再从头播
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(false);
                systems[i].Play(false);
            }

            var inst = new ParticleInstance
            {
                Id = ++_nextInstanceId,
                Epoch = _epoch,
                Go = go,
                Systems = systems,
                Follow = ctx.Follow,
                FollowRequested = ctx.FollowRequested,
                Duration = EstimateDuration(systems),
                Intensity = ctx.Intensity,
            };

            _active[inst.Id] = inst;
            if (_isSingleton) _singleton = inst;

            return HandleOf(inst);
        }

        public void Stop(EffectHandle handle)
        {
            if (!handle.IsValid) return;

            // 过期句柄（CleanAll 之前发的）：实例早已回收，直接忽略
            if (handle.Generation != _epoch) return;

            if (!_active.TryGetValue(handle.Id, out ParticleInstance inst)) return;

            Recycle(inst);
        }

        public void CleanAll()
        {
            // 让此前发出的所有句柄失效
            _epoch++;

            if (_active.Count > 0)
            {
                _recycleScratch.Clear();
                foreach (KeyValuePair<int, ParticleInstance> kv in _active)
                {
                    _recycleScratch.Add(kv.Key);
                }

                for (int i = 0; i < _recycleScratch.Count; i++)
                {
                    if (_active.TryGetValue(_recycleScratch[i], out ParticleInstance inst)) Recycle(inst);
                }
            }

            _singleton = null;
        }

        public void Tick(float dt)
        {
            if (_active.Count == 0) return;

            _recycleScratch.Clear();

            foreach (KeyValuePair<int, ParticleInstance> kv in _active)
            {
                ParticleInstance inst = kv.Value;
                inst.Elapsed += dt;

                if (inst.FollowRequested)
                {
                    if (inst.Follow == null)
                    {
                        // 跟随目标被销毁（Unity 的假 null）：立刻回收，否则会飘在原地
                        _recycleScratch.Add(inst.Id);
                        continue;
                    }

                    FollowTarget(inst);
                }

                if (IsFinished(inst)) _recycleScratch.Add(inst.Id);
            }

            for (int i = 0; i < _recycleScratch.Count; i++)
            {
                if (_active.TryGetValue(_recycleScratch[i], out ParticleInstance inst)) Recycle(inst);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CleanAll();

            _pool?.Dispose();
            _pool = null;
            _prefab = null;
        }

        // ─────────────────────────────────────────────
        // 内部：装配
        // ─────────────────────────────────────────────

        private void BuildPool(GameObject prefab)
        {
            _prefab = prefab;
            _prefabScale = prefab.transform.localScale;

            // 作者授权的乘数：趁"没有任何东西改过它"的时刻，从预制体资源上抓一次。
            // 之后每次播放都写回 作者值 × 强度 —— 于是"预制体即基准"这条契约为恒真。
            ParticleSystem[] authored = prefab.GetComponentsInChildren<ParticleSystem>(true);
            _authoredSizeMul = new float[authored.Length];
            _authoredRateMul = new float[authored.Length];

            for (int i = 0; i < authored.Length; i++)
            {
                _authoredSizeMul[i] = authored[i].main.startSizeMultiplier;
                _authoredRateMul[i] = authored[i].emission.rateOverTimeMultiplier;
            }

            _pool = new Pool<GameObject>(
                factory: () =>
                {
                    GameObject go = Object.Instantiate(prefab, _root);
                    go.SetActive(false);
                    return go;
                },
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: _tag,
                maxSize: _maxSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                onDestroy: null);   // 实例都是特效根的子物体，销毁根即回收

            if (_prewarm > 0) _pool.Prewarm(_prewarm);
        }

        private void ApplyPlacement(GameObject go, in EffectContext ctx)
        {
            Transform t = go.transform;
            Vector2 p = ctx.Position;

            // 保留预制体自带的 z：2D 排序看 Sorting Layer，但把作者的 z 抹成 0 是隐性坑
            t.position = new Vector3(p.x, p.y, t.position.z);
            t.localScale = _prefabScale * ctx.Scale;
        }

        private static void FollowTarget(ParticleInstance inst)
        {
            Transform t = inst.Go.transform;
            Vector3 f = inst.Follow.position;
            t.position = new Vector3(f.x, f.y, t.position.z);
        }

        /// <summary>
        /// 强度映射：<b>在作者值的基础上</b>做相对缩放（<c>intensity = 1</c> 时 = 原样播）。
        /// </summary>
        /// <remarks>
        /// <para>乘数属性对<b>所有</b> <c>MinMaxCurve</c> 模式都合法，但它<b>属于作者</b>：
        /// Curve / Random Between Two Curves 模式下，Inspector 曲线下方的 "Multiplier" 就是它
        /// （作者的整个幅度可能都住在里面）。直接写 <c>= k</c> 会把它抹成 1 ——
        /// 预制体里 Start Size 设得再小，进游戏也会"变成默认大小"。所以一律写 <c>作者值 × k</c>。</para>
        /// <para><c>startSize.curveMultiplier</c>（曲线自带的乘数）<b>刻意不碰</b>：它与本乘数相乘，
        /// 两个都乘 k 会变成 k²，强度越小缩得越狠。只缩放其中一个，总量恒等于"作者值 × k"。</para>
        /// <para>实例的系统数与预制体不一致时（理论上不该发生）：<b>宁可不缩放</b>也不写坏作者值，
        /// 只报一次警告。</para>
        /// </remarks>
        private void ApplyIntensity(ParticleSystem[] systems, float intensity)
        {
            float k = Mathf.Lerp(MinIntensityScale, 1f, Mathf.Clamp01(intensity));

            if (_authoredSizeMul == null || _authoredSizeMul.Length != systems.Length)
            {
                if (!_warnedAuthoredMismatch)
                {
                    _warnedAuthoredMismatch = true;
                    Debug.LogWarning($"{_tag} 实例的粒子系统数（{systems.Length}）与预制体（" +
                                     $"{(_authoredSizeMul == null ? 0 : _authoredSizeMul.Length)}）不一致，" +
                                     "本次不做强度缩放（保留预制体原样），以免写坏作者值。");
                }

                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.startSizeMultiplier = _authoredSizeMul[i] * k;

                ParticleSystem.EmissionModule emission = systems[i].emission;
                emission.rateOverTimeMultiplier = _authoredRateMul[i] * k;
            }
        }

        /// <summary>
        /// 估算一次播放的总时长（秒）：各粒子系统 <c>duration + startLifetime</c> 的最大值。
        /// 任一系统是循环型就返回 +∞（靠 Stop / CleanAll / 跟随丢失回收）。
        /// </summary>
        private static float EstimateDuration(ParticleSystem[] systems)
        {
            float max = 0f;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;

                if (main.loop) return float.PositiveInfinity;

                float lifetime;
                switch (main.startLifetime.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        lifetime = main.startLifetime.constant;
                        break;

                    case ParticleSystemCurveMode.TwoConstants:
                        lifetime = main.startLifetime.constantMax;
                        break;

                    default:
                        // Curve / TwoCurves：curveMultiplier 是"乘数"而非曲线峰值，可能偏小。
                        // 偏小不要紧——到期后还会问一次 IsAlive（见 IsFinished），不会提前掐断。
                        lifetime = main.startLifetime.curveMultiplier;
                        break;
                }

                float total = main.duration + Mathf.Max(0f, lifetime);
                if (total > max) max = total;
            }

            return max;
        }

        // ─────────────────────────────────────────────
        // 内部：回收
        // ─────────────────────────────────────────────

        private bool IsFinished(ParticleInstance inst)
        {
            if (inst.Go == null || inst.Systems == null) return true;

            // 还没到估算时长：不问引擎（IsAlive 会遍历粒子）
            if (inst.Elapsed < inst.Duration) return false;

            if (inst.Elapsed < inst.Duration + MaxLifeOverrun && HasLiveParticles(inst)) return false;

            if (!_warnedLifeOverrun && inst.Elapsed >= inst.Duration + MaxLifeOverrun)
            {
                _warnedLifeOverrun = true;
                Debug.LogWarning($"{_tag} 有实例超过估算时长 {MaxLifeOverrun}s 仍未结束，已强制回收。" +
                                 "常见原因：粒子的 Start Lifetime 设成了无限。");
            }

            return true;
        }

        /// <summary>实例里是否还有活着的粒子。用 <c>IsAlive(false)</c> 逐个问（不递归，避免重复计数）。</summary>
        private static bool HasLiveParticles(ParticleInstance inst)
        {
            ParticleSystem[] systems = inst.Systems;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps != null && ps.IsAlive(false)) return true;
            }

            return false;
        }

        private void Recycle(ParticleInstance inst)
        {
            _active.Remove(inst.Id);

            if (ReferenceEquals(_singleton, inst)) _singleton = null;

            if (inst.Systems != null)
            {
                // 归还前停干净：否则复用时能看到上一轮的残留粒子
                for (int i = 0; i < inst.Systems.Length; i++)
                {
                    ParticleSystem ps = inst.Systems[i];
                    if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                inst.Systems = null;
            }

            if (inst.Go != null && _pool != null)
            {
                _pool.Release(inst.Go);
            }
        }

        private EffectHandle HandleOf(ParticleInstance inst) => new EffectHandle(inst.Id, inst.Epoch, this);

        private void WarnPoolFull(EffectId id)
        {
            _droppedSinceWarn++;

            float now = Time.unscaledTime;
            if (now < _nextDropWarnTime) return;

            _nextDropWarnTime = now + DropWarnInterval;
            Debug.LogWarning($"{_tag} 池已满（上限 {_maxSize}），丢弃 {id} 的播放请求。" +
                             $"距上次警告累计丢弃 {_droppedSinceWarn} 次——调大 EffectCatalog 里这一行的 maxSize 即可。");
            _droppedSinceWarn = 0;
        }

        /// <summary>一次播放的账本。用类而不是结构体：要能在字典里被 Tick 原地修改。</summary>
        private sealed class ParticleInstance
        {
            public int Id;
            public int Epoch;
            public GameObject Go;
            public ParticleSystem[] Systems;
            public Transform Follow;
            public bool FollowRequested;
            public float Elapsed;
            public float Duration;
            public float Intensity;
        }
    }
}
