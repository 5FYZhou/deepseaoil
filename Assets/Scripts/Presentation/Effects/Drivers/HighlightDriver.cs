using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>瞄准格高亮驱动，整格半透明色块，创建一次后只更新，持续型无池无时长，只由 Stop 关闭</summary>
    /// <remarks>色源是观感表 ConfigModule.Visuals，程序生成，换正式美术只改 Apply 里的图元。</remarks>
    public sealed class HighlightDriver : IEffectDriver
    {
        private const int SortingOrder = RenderOrder.Aim;

        private readonly Transform _root;
        private readonly int _maxSize;

        private GameObject _go;
        private SpriteRenderer _renderer;

        private int _nextId;
        private int _epoch;
        private bool _active;
        private bool _disposed;

        internal HighlightDriver(in EffectSpec spec, Transform root) : this(root, spec.MaxSize)
        {
        }

        public HighlightDriver(Transform root, int maxSize = 1)
        {
            _root = root;
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => true;
        /// <remarks>空串 = "不需要资源"：既不预加载也不懒加载，也不会被判为缺失。</remarks>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _active ? 1 : 0;

        public int PooledObjectCount => _go != null && !_active ? 1 : 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            if (!EnsureObject()) return EffectHandle.None;

            _active = true;
            _nextId++;

            Apply(in ctx);

            return new EffectHandle(_nextId, _epoch, this);
        }

        public void UpdateInstance(EffectHandle handle, in EffectContext ctx)
        {
            if (_disposed || !_active) return;
            if (!handle.IsValid || handle.Id != _nextId || handle.Generation != _epoch) return;

            Apply(in ctx);
        }

        public void Stop(EffectHandle handle)
        {
            if (_disposed || !_active) return;
            if (!handle.IsValid || handle.Id != _nextId || handle.Generation != _epoch) return;

            Hide();
        }

        public void CleanAll()
        {
            _epoch++;
            _active = false;

            Hide();
        }

        public void Tick(float dt)
        {
        }

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

            _go = new GameObject("AimHighlight");

            _go.layer = RenderOrder.OverlayLayer;

            if (_root != null) _go.transform.SetParent(_root, false);

            _renderer = _go.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                _renderer,
                PrimitiveSprites.Square,
                Color.white,
                SortingOrder,
                1f);

            _go.SetActive(false);

            return true;
        }

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
