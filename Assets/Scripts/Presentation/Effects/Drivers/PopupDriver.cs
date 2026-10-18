using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Foundation;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>伤害飘字驱动：一次 Play 借一个世界空间弹窗，上飘淡出到点归还池</summary>
    /// <remarks>瞬态，不跟随 ctx.Anchor；位置在 Play 那一刻定死</remarks>
    public sealed class PopupDriver : IEffectDriver
    {
        private const float DropWarnInterval = 1f;

        private readonly string _assetKey;
        private readonly string _tag;
        private readonly Transform _root;
        private readonly int _maxSize;
        private readonly int _prewarm;

        /// <summary>飘字自己的随机流：借全局 Rng 会把 CombatDirector 刷怪散布的随机序列整体平移</summary>
        private readonly IRng _jitterRng = new DefaultRng();

        private readonly Dictionary<int, PopupInstance> _active = new Dictionary<int, PopupInstance>();
        private readonly List<int> _recycleScratch = new List<int>();

        private Pool<GameObject> _pool;
        private int _nextId;
        private int _epoch = 1;

        private int _droppedSinceWarn;
        private float _nextDropWarnTime;
        private bool _warnedNoView;
        private bool _warnedNoLabel;
        private bool _disposed;

        internal PopupDriver(in EffectSpec spec, Transform root)
        {
            _root = root;
            _maxSize = spec.MaxSize > 0 ? spec.MaxSize : 1;
            _prewarm = spec.Prewarm < 0 ? 0 : spec.Prewarm;
            _assetKey = spec.Key;
            _tag = "PopupDriver[" + (string.IsNullOrEmpty(spec.Key) ? "?" : spec.Key) + "]";
        }

        public bool IsSingleton => false;

        public string AssetKey => _assetKey;

        /// <summary>池建好了 = 资源到位</summary>
        public bool IsAssetReady => _pool != null;

        public int ActiveInstanceCount => _active.Count;

        public int PooledObjectCount => _pool != null ? _pool.IdleCount : 0;

        /// <summary>资源到位，拿到预制体后建池并按 prewarm 预热，幂等</summary>
        public void OnAssetLoaded(Object asset)
        {
            if (_disposed || _pool != null) return;

            var prefab = asset as GameObject;
            if (prefab == null)
            {
                Debug.LogError($"{_tag} 拿到的资源不是 GameObject（key={_assetKey}），伤害飘字不可用。");
                return;
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
                onDestroy: null);   // 实例都是特效根子物体，销毁根即回收

            if (_prewarm > 0) _pool.Prewarm(_prewarm);
        }

        /// <summary>落点＝锚点＋头顶高度＋±随机偏移；unitSquare 两轴由调用方给，取值域 [-1,1]</summary>
        public static Vector2 ResolveSpawn(Vector2 anchor, float anchorHeight, float jitterX, float jitterY, Vector2 unitSquare)
        {
            return new Vector2(
                anchor.x + unitSquare.x * jitterX,
                anchor.y + anchorHeight + unitSquare.y * jitterY);
        }

        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            if (_pool == null)
            {
                // EffectModule 会先拦资源未就位，走到这里说明装配方式不对
                Debug.LogError($"{_tag} 资源未就位就 Play 了 {id}。");
                return EffectHandle.None;
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

            PopupView view = go.GetComponent<PopupView>();

            if (view == null)
            {
                if (!_warnedNoView)
                {
                    _warnedNoView = true;
                    Debug.LogError($"{_tag} 预制体根节点上没有 PopupView，伤害飘字无法播放。" +
                                   $"请检查 Assets/Resources/{_assetKey}.prefab。本驱动只报一次。");
                }

                _pool.Release(go);
                return EffectHandle.None;
            }

            Renderer label = view.GetComponentInChildren<Renderer>(true);

            if (label == null)
            {
                if (!_warnedNoLabel)
                {
                    _warnedNoLabel = true;
                    Debug.LogError($"{_tag} 预制体上没有带渲染器的 TMP_Text（PopupView.label 未接），伤害飘字无法播放。" +
                                   $"请检查 Assets/Resources/{_assetKey}.prefab。本驱动只报一次。");
                }

                _pool.Release(go);
                return EffectHandle.None;
            }

            VisualFeelTuning feel = ConfigModule.VisualFeel;

            // <=0 是 Duration 约定的"取驱动默认"；表里也填 0 时落到常量，否则 Tick 除零
            float duration = ctx.Duration > 0f ? ctx.Duration : feel.popupDuration;
            if (duration <= 0f) duration = 0.6f;

            // 锚点是物理体位置：抬到头顶与落点抖动都在这里一次性算进 StartPos
            Vector2 anchor = ctx.Position;

            Vector2 pos = ResolveSpawn(
                anchor,
                feel.popupAnchorHeight,
                feel.popupJitterX,
                feel.popupJitterY,
                new Vector2(_jitterRng.Value01() * 2f - 1f, _jitterRng.Value01() * 2f - 1f));

            var inst = new PopupInstance
            {
                Go = go,
                View = view,
                StartPos = pos,
                Tint = ctx.Tint,
                Elapsed = 0f,
                Duration = duration,
                HandleId = ++_nextId,
            };

            view.SetStyle(feel.popupOutlineWidth, feel.popupOutlineColor, feel.popupFontScale);
            view.SetValue(Mathf.RoundToInt(ctx.Value));
            view.SetColor(Color.Lerp(inst.Tint, Color.white, feel.popupWhiten));
            view.SetAlpha(1f);

            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // 排序只在 Play 算一次：按锚点档位，落点抖动不该让同一只怪的飘字跳档
            label.sortingOrder = RenderOrder.ActorOrder(anchor.y) + feel.popupSortingOffset;

            _active[inst.HandleId] = inst;

            return new EffectHandle(inst.HandleId, _epoch, this);
        }

        public void Stop(EffectHandle handle)
        {
            if (!handle.IsValid) return;

            // CleanAll 之前发出的句柄：实例早已回收
            if (handle.Generation != _epoch) return;

            if (!_active.TryGetValue(handle.Id, out PopupInstance inst)) return;

            Recycle(inst);
        }

        public void CleanAll()
        {
            // 让此前发出的句柄全部失效
            _epoch++;

            if (_active.Count > 0)
            {
                _recycleScratch.Clear();
                foreach (KeyValuePair<int, PopupInstance> kv in _active)
                {
                    _recycleScratch.Add(kv.Key);
                }

                for (int i = 0; i < _recycleScratch.Count; i++)
                {
                    if (_active.TryGetValue(_recycleScratch[i], out PopupInstance inst)) Recycle(inst);
                }
            }

            _recycleScratch.Clear();
        }

        public void Tick(float dt)
        {
            if (_disposed || _active.Count == 0) return;

            VisualFeelTuning feel = ConfigModule.VisualFeel;

            _recycleScratch.Clear();

            foreach (KeyValuePair<int, PopupInstance> kv in _active)
            {
                PopupInstance inst = kv.Value;

                // 实例被外部销毁（切换场景/清场）后写 transform 会刷报错
                if (inst.Go == null)
                {
                    _recycleScratch.Add(inst.HandleId);
                    continue;
                }

                inst.Elapsed += dt;

                float t = inst.Elapsed / inst.Duration;

                if (t >= 1f)
                {
                    _recycleScratch.Add(inst.HandleId);
                    continue;
                }

                float y = inst.StartPos.y + feel.popupRiseHeight * (1f - Mathf.Pow(1f - t, feel.popupRiseEase));
                float fade = Mathf.InverseLerp(feel.popupHold, 1f, t);

                inst.View.SetAlpha(1f - Mathf.Pow(fade, feel.popupFadeEase));

                inst.Go.transform.position = new Vector3(inst.StartPos.x, y, 0f);
            }

            if (_recycleScratch.Count == 0) return;

            for (int i = 0; i < _recycleScratch.Count; i++)
            {
                if (_active.TryGetValue(_recycleScratch[i], out PopupInstance inst)) Recycle(inst);
            }

            _recycleScratch.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CleanAll();

            _pool?.Dispose();
            _pool = null;
        }

        private void Recycle(PopupInstance inst)
        {
            _active.Remove(inst.HandleId);

            if (inst.Go != null && _pool != null) _pool.Release(inst.Go);
        }

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

        /// <summary>一次飘字的账本，用类而非结构体以便在字典里被 Tick 原地修改</summary>
        private sealed class PopupInstance
        {
            public GameObject Go;
            public PopupView View;
            public Vector2 StartPos;
            public Color Tint;
            public float Elapsed;
            public float Duration;
            public int HandleId;
        }
    }
}
