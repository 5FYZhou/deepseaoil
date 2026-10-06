using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 瞄准格高亮的驱动：整格半透明色块，<b>创建一次、之后只更新</b>。程序生成，不需要资源。
    /// </summary>
    /// <remarks>
    /// <b>它是"持续效果"的第一个消费者。</b>与另外三个驱动不同，它不参与"播完就回收"的循环：
    /// 高亮要一直亮着，直到逻辑层说"不瞄了"（<c>Stop</c>）。所以它没有池、没有时长、
    /// 也没有 <c>Tick</c> 里的到期回收 —— 那些都是"一次性特效"的机制。
    /// <para><b>为什么整格而不是圆：</b>落点已经被吸附到格子中心，画一个圆反而让人以为落点在圆心上、
    /// 与格子无关。整格色块把"这一格会变成泥浆"这件事直接画出来（与它替代掉的那个白模件同口径）。</para>
    /// <para><b>资产</b>：现在用运行期生成的方块图元（<c>PrimitiveSprites.Square</c>）顶替，
    /// 正式美术到位后把 <see cref="Apply"/> 里的图元换成驱动自持的贴图 / 材质即可 ——
    /// 换的时候不需要动调用方（它只认识 <c>EffectId.TileHighlight</c> 与 <c>EffectContext</c>）。</para>
    /// <para><b>单例语义由本类自己保证</b>：<c>EffectModule</c> 不读 <c>IsSingleton</c>（设计如此），
    /// 重复 <c>Play</c> 时本类复用同一个物体、只递增编号。</para>
    /// </remarks>
    public sealed class TileHighlightDriver : IEffectDriver
    {
        /// <summary>色块的排序层：低于球、高于地板与格效果层。</summary>
        private const int SortingOrder = RenderOrder.Aim;

        private readonly Transform _root;
        private readonly int _maxSize;

        private GameObject _go;
        private SpriteRenderer _renderer;

        private int _nextId;
        private int _epoch;
        private bool _active;
        private bool _disposed;

        /// <summary>由装配表构造（<c>EffectDriverFactory</c> 用）。</summary>
        internal TileHighlightDriver(in EffectSpec spec, Transform root) : this(root, spec.MaxSize)
        {
        }

        /// <param name="root">特效根（实例挂在它下面，销毁根即回收）。</param>
        /// <param name="maxSize">同屏上限；本驱动只用一个实例，参数保留是为了与其它驱动同形。</param>
        public TileHighlightDriver(Transform root, int maxSize = 1)
        {
            _root = root;
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        /// <inheritdoc />
        public bool IsSingleton => true;

        /// <inheritdoc />
        /// <remarks>空串 = "不需要资源"：既不预加载也不懒加载，也不会被"没有同名预制体"判为缺失。</remarks>
        public string AssetKey => string.Empty;

        /// <inheritdoc />
        public bool IsAssetReady => true;

        /// <inheritdoc />
        public int ActiveInstanceCount => _active ? 1 : 0;

        /// <inheritdoc />
        /// <remarks>本驱动不建池；"待用"指那个已经建好但当前没显示的物体（对外读数用）。</remarks>
        public int PooledObjectCount => _go != null && !_active ? 1 : 0;

        /// <inheritdoc />
        public void OnAssetLoaded(Object asset)
        {
            // 不需要资源：本驱动永远不会被要求加载。
        }

        /// <inheritdoc />
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            if (!EnsureObject()) return EffectHandle.None;

            _active = true;
            _nextId++;

            Apply(in ctx);

            return new EffectHandle(_nextId, _epoch, this);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 句柄必须与"当前这一次播放"对上：编号或代次不符就是过期句柄，no-op ——
        /// 否则一个旧句柄会把新的高亮挪到旧位置（而那种抖动看起来像"高亮跟丢了"）。
        /// </remarks>
        public void UpdateInstance(EffectHandle handle, in EffectContext ctx)
        {
            if (_disposed || !_active) return;
            if (!handle.IsValid || handle.Id != _nextId || handle.Generation != _epoch) return;

            Apply(in ctx);
        }

        /// <inheritdoc />
        public void Stop(EffectHandle handle)
        {
            if (_disposed || !_active) return;
            if (!handle.IsValid || handle.Id != _nextId || handle.Generation != _epoch) return;

            Hide();
        }

        /// <inheritdoc />
        public void CleanAll()
        {
            _epoch++;
            _active = false;

            Hide();
        }

        /// <inheritdoc />
        /// <remarks>持续型特效不随时间推进：显隐与位置都由 <c>Play</c>/<c>UpdateInstance</c>/<c>Stop</c> 决定。</remarks>
        public void Tick(float dt)
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _active = false;

            if (_go != null) Object.Destroy(_go);

            _go = null;
            _renderer = null;
        }

        private bool EnsureObject()
        {
            if (_go != null) return true;

            if (_maxSize <= 0) return false;

            _go = new GameObject("瞄准格高亮");

            _go.layer = RenderOrder.OverlayLayer;

            if (_root != null) _go.transform.SetParent(_root, false);

            _renderer = _go.AddComponent<SpriteRenderer>();

            // 单位方块图元（1×1 世界单位），尺寸由 Apply 按 ctx.Radius 缩放
            PrimitiveSprites.Configure(
                _renderer,
                PrimitiveSprites.Square,
                Color.white,
                SortingOrder,
                1f);

            _go.SetActive(false);

            return true;
        }

        /// <summary>摆位与着色：位置 = <c>ctx.Position</c>，边长 = <c>ctx.Radius × 2</c>。</summary>
        /// <remarks><c>ctx.Radius</c> 的语义与落地环一致（半径），所以高亮传进来的应该是"半格"。</remarks>
        private void Apply(in EffectContext ctx)
        {
            if (_go == null) return;

            Vector2 position = ctx.Position;

            _go.transform.position = new Vector3(position.x, position.y, 0f);

            float radius = Mathf.Max(ctx.Radius, 1e-3f);
            float diameter = radius * 2f;

            _go.transform.localScale = new Vector3(diameter, diameter, 1f);

            if (_renderer != null) _renderer.color = ctx.Tint;

            if (!_go.activeSelf) _go.SetActive(true);
        }

        private void Hide()
        {
            if (_go != null && _go.activeSelf) _go.SetActive(false);
        }
    }
}
