using System.Collections.Generic;
using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 落地环驱动：在落点画一个从满尺寸缩到消失的贴地圆环。<b>程序生成，不需要任何资源</b>。
    /// </summary>
    /// <remarks>
    /// <b>它不是特效，是仪表。</b>落地的作用范围在屏幕上原本不可见 —— 没有这个圈，
    /// 试冲量强度就只能靠反复猜。所以半径来自 <c>ctx.Radius</c>（那个数就是实际生效范围），
    /// 颜色来自 <c>ctx.Tint</c>（球种色 / 格心色）。
    /// <para><b>为什么环的底图固定按 1 米烘：</b>让"缩放 = 直径"这条换算只有一个来源。
    /// 曾经按"实际半径"烘、又在摆位时乘直径 —— 那份缩放里混进了一个已经烘进贴图的量，
    /// 改一个参数就会让两个系数相乘、圈大出一个数量级（而且不报错）。</para>
    /// <para><b>形状常量是驱动自己的，不读 <c>ThrowTuning</c>：</b>驱动在
    /// <c>EffectModule.Init</c>（<c>GameRoot.Awake</c>）时构造，那时组合根还没装配、
    /// 拿不到注入的调参实例；为两个常量去引一条"特效 → 调参资产"的运行时依赖不划算。
    /// 代价是"贴地感"的形状参数在瞄准环与落地环各有一份，两份都在明处。</para>
    /// <para>对象走池（<c>Pool&lt;GameObject&gt;</c>）：落地是热路径，每次 <c>Instantiate</c>
    /// 会给每次投掷都留一份垃圾。</para>
    /// </remarks>
    public sealed class LandingRingDriver : IEffectDriver
    {
        /// <summary>底图烘制用的基准半径（世界单位）。</summary>
        private const float BakedRadius = 1f;

        /// <summary>贴地形状：竖直压扁比例（与瞄准环同一份观感）。</summary>
        private const float VerticalSquash = 0.6f;

        /// <summary>贴地形状：上半弧收窄比例。</summary>
        private const float PerspectiveTaper = 0.15f;

        /// <summary>环的线宽（世界单位）。</summary>
        private const float RingThicknessMeters = 0.07f;

        /// <summary>默认时长（秒）；<c>ctx.Duration</c> 未给时用它。</summary>
        private const float DefaultDuration = 0.15f;

        /// <summary>时长上限（秒）：挡住调用方传进来的离谱值，免得一个环留在场上。</summary>
        private const float MaxDuration = 5f;

        /// <summary>环的透明度系数（相对于传入颜色的 alpha）。</summary>
        private const float AlphaScale = 0.75f;

        /// <summary>单帧最多回收几个（防一次 <c>Tick</c> 里集中归还造成尖峰）。</summary>
        private const int MaxRecyclePerTick = 32;

        private sealed class ActiveRing
        {
            public int Id;
            public int Epoch;
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Elapsed;
            public float Duration;
            public float Radius;
            public float Alpha;
        }

        private readonly Transform _root;
        private readonly int _maxSize;
        private readonly Pool<GameObject> _pool;
        private readonly List<ActiveRing> _active = new List<ActiveRing>();
        private readonly List<ActiveRing> _recycleScratch = new List<ActiveRing>();

        private Sprite _ringSprite;
        private int _nextId;
        private int _epoch;
        private bool _disposed;

        /// <summary>
        /// 由装配表构造（<c>EffectDriverFactory</c> 用）。
        /// </summary>
        /// <remarks>
        /// <c>internal</c> 是因为 <c>EffectSpec</c> 本身是 internal —— 公开它会让"装配参数"变成对外 API。
        /// 需要手工构造时用下面那个公开重载。
        /// </remarks>
        internal LandingRingDriver(in EffectSpec spec, Transform root) : this(root, spec.MaxSize)
        {
        }

        /// <param name="root">特效根（实例都挂在它下面，销毁根即回收）。</param>
        /// <param name="maxSize">同屏上限；<c>&le; 0</c> 视为 1。</param>
        public LandingRingDriver(Transform root, int maxSize = 16)
        {
            _root = root;
            _maxSize = maxSize > 0 ? maxSize : 1;

            _pool = new Pool<GameObject>(
                factory: CreateRingObject,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "LandingRing",
                maxSize: _maxSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                // 实例都挂在特效根下，销毁根即回收，所以这里不需要逐个 Destroy。
                onDestroy: null);
        }

        /// <inheritdoc />
        public bool IsSingleton => false;

        /// <inheritdoc />
        /// <remarks>空串 = "不需要资源"：<c>EffectModule</c> 既不预加载也不懒加载，也不会报缺失。</remarks>
        public string AssetKey => string.Empty;

        /// <inheritdoc />
        public bool IsAssetReady => true;

        /// <inheritdoc />
        public int ActiveInstanceCount => _active.Count;

        /// <inheritdoc />
        public int PooledObjectCount => _pool != null ? _pool.IdleCount : 0;

        /// <inheritdoc />
        public void OnAssetLoaded(Object asset)
        {
            // 不需要资源：本驱动永远不会被要求加载。
        }

        /// <inheritdoc />
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            if (!_pool.TryGet(out GameObject go) || go == null)
            {
                Debug.LogWarning($"[Effect] LandingRing 池已满（上限 {_maxSize}），本次不播。");
                return EffectHandle.None;
            }

            var ring = new ActiveRing
            {
                Id = ++_nextId,
                Epoch = _epoch,
                Go = go,
                Renderer = go.GetComponent<SpriteRenderer>(),
                Elapsed = 0f,
                Duration = Sanitize(ctx.Duration),
                Radius = Mathf.Max(ctx.Radius, 1e-3f),
                Alpha = Mathf.Clamp01(ctx.Tint.a) * AlphaScale,
            };

            Vector2 position = ctx.Position;

            go.transform.position = new Vector3(position.x, position.y, 0f);

            Color color = ctx.Tint;
            color.a = ring.Alpha;

            if (ring.Renderer != null)
            {
                ring.Renderer.color = color;
                ring.Renderer.sortingOrder = RenderOrder.LandingRing;
            }

            // 满尺寸起步：任何时刻看到的外沿都不会超过 ctx.Radius ——
            // 也就是"圈只会比生效范围小，永远不会比它大"，不会出现"圈住了却没生效"。
            ApplyScale(ring, 1f);

            _active.Add(ring);

            return new EffectHandle(ring.Id, ring.Epoch, this);
        }

        /// <inheritdoc />
        public void Stop(EffectHandle handle)
        {
            if (!handle.IsValid) return;
            if (handle.Generation != _epoch) return;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != handle.Id) continue;

                Recycle(_active[i]);
                _active.RemoveAt(i);
                return;
            }
        }

        /// <inheritdoc />
        public void CleanAll()
        {
            _epoch++;

            for (int i = 0; i < _active.Count; i++)
            {
                Recycle(_active[i]);
            }

            _active.Clear();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_disposed || _active.Count == 0) return;

            _recycleScratch.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                ActiveRing ring = _active[i];

                ring.Elapsed += dt;

                if (ring.Elapsed >= ring.Duration)
                {
                    _recycleScratch.Add(ring);
                    continue;
                }

                ApplyScale(ring, 1f - ring.Elapsed / ring.Duration);
            }

            if (_recycleScratch.Count == 0) return;

            for (int i = 0; i < _recycleScratch.Count && i < MaxRecyclePerTick; i++)
            {
                ActiveRing ring = _recycleScratch[i];

                Recycle(ring);
                _active.Remove(ring);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();

            // 池里剩下的对象都是特效根的子物体，由 EffectModule 销毁根时一并回收。
            _pool.Dispose();
            _ringSprite = null;
        }

        /// <summary>按进度摆尺寸与透明度：进度 1 = 满尺寸，0 = 消失。</summary>
        private static void ApplyScale(ActiveRing ring, float progress)
        {
            if (ring.Go == null) return;

            progress = Mathf.Clamp01(progress);

            float diameter = ring.Radius * progress * 2f;

            ring.Go.transform.localScale = new Vector3(diameter, diameter, 1f);

            if (ring.Renderer == null) return;

            Color color = ring.Renderer.color;
            color.a = ring.Alpha * progress;

            ring.Renderer.color = color;
        }

        private void Recycle(ActiveRing ring)
        {
            if (ring.Go == null) return;

            _pool.Release(ring.Go);
        }

        private static float Sanitize(float duration)
        {
            if (float.IsNaN(duration) || duration <= 0f) return DefaultDuration;

            return Mathf.Min(duration, MaxDuration);
        }

        private GameObject CreateRingObject()
        {
            var go = new GameObject("落地环");

            go.layer = RenderOrder.OverlayLayer;

            if (_root != null) go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();

            // 与瞄准环、球阴影共用同一份贴地画法：三者对"贴地"的理解必须一致。
            PrimitiveSprites.ConfigureGround(
                renderer,
                RingSprite(),
                Color.white,
                RenderOrder.LandingRing,
                BakedRadius,
                BakedRadius * 2f);

            go.SetActive(false);

            return go;
        }

        private Sprite RingSprite()
        {
            if (_ringSprite == null)
            {
                _ringSprite = PrimitiveSprites.GroundDiscOrRing(
                    BakedRadius,
                    VerticalSquash,
                    PerspectiveTaper,
                    RingThicknessMeters,
                    solid: false);
            }

            return _ringSprite;
        }
    }
}
