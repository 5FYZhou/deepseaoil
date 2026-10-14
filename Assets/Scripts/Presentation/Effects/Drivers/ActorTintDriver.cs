using System.Collections.Generic;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>实体颜色栈驱动：color 只写「持续态 × 基线」，瞬态闪白改走材质插值</summary>
            /// <remarks>基线是首次抓到渲染器时记下的现值：纯白乘数在乘法法则下永远变不白。</remarks>
    public sealed class ActorTintDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.12f;

        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private sealed class ColorState
        {
            public SpriteRenderer Renderer;

            public Color BaseColor = Color.white;

            public Color PersistentTint = Color.white;

            public Color FlashColor = Color.white;

            public float FlashDuration = DefaultDuration;

            public float FlashRemaining;

            public bool FlashOn;

            /// <summary>上一次钉进材质的精灵贴图；逐帧只做一次引用比较</summary>
            public Texture BoundTexture;

            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            /// <summary>持续态是否在生效：白乘数也是合法垫色，判不出"有没有"</summary>
            public bool SlowOn;

            public int SlowHandleId;

            /// <summary>尸体淡出通道：alpha 与 SlowOn 正交</summary>
            public bool AlphaOn;

            public int AlphaHandleId;

            public ActorAnimationView AlphaSource;
        }

        private readonly Dictionary<Transform, ColorState> _states = new Dictionary<Transform, ColorState>();

        private readonly List<Transform> _recycleScratch = new List<Transform>();

        private readonly int _maxSize;

        private int _nextId;
        private int _epoch = 1;
        private bool _disposed;

        internal ActorTintDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public ActorTintDriver(int maxSize = 32)
        {
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => false;

        /// <summary>空串 = 不需要资源：不预加载、不懒加载、不算缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _states.Count;

        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Anchor 空、或抓不到 SpriteRenderer 时返回 None：给错目标只静默不播</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            Transform target = ctx.Anchor;

            if (target == null) return EffectHandle.None;

            ColorState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            if (id == EffectId.Flash)
            {
                state.FlashColor = ctx.Tint;
                state.FlashDuration = ctx.Duration > 0f ? ctx.Duration : DefaultDuration;
                state.FlashRemaining = state.FlashDuration;

                EnsureSpriteTexture(state);
                ApplyFlash(state, 1f);
            }
            else if (id == EffectId.SlowTint)
            {
                // 重复 Play 必须复用旧句柄：发新句柄的话调用方手里那个就永远 Stop 不掉
                if (state.SlowOn && state.PersistentTint == ctx.Tint) return new EffectHandle(state.SlowHandleId, _epoch, this);

                state.PersistentTint = ctx.Tint;
                state.SlowOn = true;
                state.SlowHandleId = ++_nextId;

                ApplyPersistentTint(state);

                return new EffectHandle(state.SlowHandleId, _epoch, this);
            }
            else if (id == EffectId.AlphaTint)
            {
                // 只往父级找：Animator 挂在渲染子节点的父链上
                ActorAnimationView source = target.GetComponentInParent<ActorAnimationView>();

                if (state.AlphaOn && state.AlphaSource == source) return new EffectHandle(state.AlphaHandleId, _epoch, this);

                state.AlphaSource = source;
                state.AlphaOn = true;
                state.AlphaHandleId = ++_nextId;

                ApplyPersistentTint(state);

                return new EffectHandle(state.AlphaHandleId, _epoch, this);
            }
            else
            {
                return EffectHandle.None;
            }

            // 闪白与持续态共用一个自增空间：编号不撞车，Stop 不会误停对方的账
            return new EffectHandle(++_nextId, _epoch, this);
        }

        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;

            if (!handle.IsValid || handle.Generation != _epoch) return;

            foreach (KeyValuePair<Transform, ColorState> kv in _states)
            {
                ColorState state = kv.Value;

                if (state.SlowHandleId == handle.Id)
                {
                    state.SlowOn = false;
                    state.SlowHandleId = 0;
                    state.PersistentTint = Color.white;

                    // 闪白是独立通道，持续态退场不必等它，也绝不掐掉它
                    ApplyPersistentTint(state);

                    return;
                }

                if (state.AlphaHandleId == handle.Id)
                {
                    state.AlphaOn = false;
                    state.AlphaHandleId = 0;
                    state.AlphaSource = null;

                    ApplyPersistentTint(state);

                    return;
                }
            }
        }

        public void Tick(float dt)
        {
            if (_states.Count == 0) return;

            _recycleScratch.Clear();

            foreach (KeyValuePair<Transform, ColorState> kv in _states)
            {
                Transform target = kv.Key;
                ColorState state = kv.Value;

                // 目标被销毁（Unity 假 null）：无从还原，直接摘账
                if (target == null || state.Renderer == null)
                {
                    _recycleScratch.Add(target);

                    continue;
                }

                // 动画换帧会换精灵：不跟着换贴图的话闪白会画成上一帧的剪影
                EnsureSpriteTexture(state);

                if (state.FlashRemaining > 0f)
                {
                    state.FlashRemaining -= dt;

                    bool expired = state.FlashRemaining <= 0f;

                    // 必须逐帧重提交：只写一次会一直挂在纯白不淡出
                    ApplyFlash(state, expired ? 0f : state.FlashRemaining / state.FlashDuration);

                    if (expired) ApplyPersistentTint(state);
                }
                else if (state.AlphaOn)
                {
                    // alpha 与 RGB 一次写掉：分两次写会后者盖前者
                    ApplyPersistentTint(state);
                }
                else if (!state.SlowOn && state.PersistentTint == Color.white)
                {
                    // 既无瞬态也无持续态：摘掉账，保证零遍历开销
                    _recycleScratch.Add(target);
                }
            }

            for (int i = 0; i < _recycleScratch.Count; i++)
            {
                _states.Remove(_recycleScratch[i]);
            }
        }

        public void CleanAll()
        {
            // 让此前发出的句柄全部失效
            _epoch++;

            foreach (KeyValuePair<Transform, ColorState> kv in _states)
            {
                SpriteRenderer renderer = kv.Value.Renderer;

                if (renderer == null) continue;

                renderer.color = kv.Value.BaseColor;

                // 属性块按渲染器隔离：不清这一块，下一个用同材质的角色会永久纯白
                if (kv.Value.FlashOn) ApplyFlash(kv.Value, 0f);
            }

            _states.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
        }

        private void ApplyPersistentTint(ColorState state)
        {
            if (state.Renderer == null) return;

            Color final = state.PersistentTint * state.BaseColor;

            // 淡出只乘系数：BaseColor.a 是美术基线 alpha
            final.a = state.BaseColor.a * (state.AlphaOn && state.AlphaSource != null
                ? state.AlphaSource.FadeAlpha
                : 1f);

            state.Renderer.color = final;
        }

        /// <summary>闪白强度写进属性块；无 _FlashAmount 的材质退回 color 乘法</summary>
        private void ApplyFlash(ColorState state, float amount)
        {
            if (state.Renderer == null) return;

            // 乘法无渐变：整段瞬态写满白，到点还原
            if (!state.FlashOn)
            {
                state.Renderer.color = amount > 0f ? Color.white : state.PersistentTint * state.BaseColor;

                return;
            }

            state.Block.SetFloat(FlashAmountId, amount);
            state.Block.SetColor(FlashColorId, state.FlashColor);
            state.Renderer.SetPropertyBlock(state.Block);
        }

        /// <summary>把精灵贴图钉进材质：_MainTex 留空时 Shader 采到的是默认白，闪白会变成一整块白剪影</summary>
        /// <remarks>必须写 renderer.material：写 sharedMaterial 会把贴图共享给同材质角色。</remarks>
        private static void EnsureSpriteTexture(ColorState state)
        {
            Sprite sprite = state.Renderer.sprite;

            if (sprite == null) return;

            Texture texture = sprite.texture;

            if (state.BoundTexture == texture) return;

            state.Renderer.material.mainTexture = texture;
            state.BoundTexture = texture;
        }

        /// <summary>抓一次真身渲染器并缓存：不缓存的话每次瞬态都要重下探子树</summary>
        private ColorState GetOrCreateState(Transform target)
        {
            if (_states.TryGetValue(target, out ColorState state))
            {
                if (state.Renderer != null) return state;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();

            // 自身没有才下探子树，且跳过影子件：脚底的 ShadowView 不能跟着变白
            if (renderer == null)
            {
                SpriteRenderer[] children = target.GetComponentsInChildren<SpriteRenderer>(true);

                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].gameObject.name.Contains("Shadow")) continue;

                    renderer = children[i];

                    break;
                }
            }

            if (renderer == null)
            {
                _states.Remove(target);

                return null;
            }

            if (state == null)
            {
                if (_states.Count >= _maxSize)
                {
                    Debug.LogWarning(
                        $"[Tint] 已着色目标数达上限 {_maxSize}，本次请求被丢弃。调大 EffectCatalog 里这一行的 maxSize 即可。");

                    return null;
                }

                state = new ColorState();

                _states[target] = state;
            }

            // 渲染器是新建的就重立基线，别把上一具身体的颜色带过来
            state.Renderer = renderer;
            state.BaseColor = renderer.color;

            // 只读 sharedMaterial 探一次：写 renderer.material 会逐实例克隆材质
            state.FlashOn = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(FlashAmountId);

            return state;
        }
    }
}
