using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>实体颜色栈驱动：color 只写「持续态 × 基线」，瞬态闪白改走材质插值</summary>
    /// <remarks>基线是首次抓到渲染器时记下的现值：纯白乘数在乘法法则下永远变不白。句柄 Id 自增，不复用目标实例 ID，也从不返回 0。目标销毁按 Unity 假 null 摘账，不抛异常。</remarks>
    public sealed class ActorTintDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.12f;

        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        /// <summary>一个目标的色栈账本；BaseColor 捕获自渲染器现值，裸写 Color.white 会洗掉美术上的色</summary>
        private sealed class ColorState
        {
            public SpriteRenderer Renderer;

            public Color BaseColor = Color.white;

            public Color PersistentTint = Color.white;

            public Color FlashColor = Color.white;

            public float FlashDuration = DefaultDuration;

            public float FlashRemaining;

            /// <summary>材质上有没有 _FlashAmount：没有就退化成乘法闪白，别把闪白悄悄吞掉</summary>
            public bool FlashOn;

            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            /// <summary>持续态是否在生效：白乘数也是合法垫色，光看颜色判不出"有没有"</summary>
            public bool SlowOn;

            public int SlowHandleId;
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

        /// <summary>空串 = "不需要资源"：既不预加载也不懒加载，也不会被判为缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _states.Count;

        /// <summary>本驱动不持有 GameObject，池恒为空</summary>
        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Follow 为空、或目标上抓不到 SpriteRenderer 时返回 None：给错目标只静默不播，不报错</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            Transform target = ctx.Follow;

            if (target == null) return EffectHandle.None;

            ColorState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            if (id == EffectId.Flash)
            {
                state.FlashColor = ctx.Tint;
                state.FlashDuration = ctx.Duration > 0f ? ctx.Duration : DefaultDuration;
                state.FlashRemaining = state.FlashDuration;

                ApplyFlash(state, 1f);
            }
            else if (id == EffectId.SlowTint)
            {
                state.PersistentTint = ctx.Tint;
                state.SlowOn = true;
                state.SlowHandleId = ++_nextId;

                ApplyPersistentTint(state);

                return new EffectHandle(state.SlowHandleId, _epoch, this);
            }
            else
            {
                return EffectHandle.None;
            }

            // 闪白与持续态用同一个自增空间：编号不撞车，Stop 就不会误停对方的账
            return new EffectHandle(++_nextId, _epoch, this);
        }

        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;

            if (!handle.IsValid || handle.Generation != _epoch) return;

            foreach (KeyValuePair<Transform, ColorState> kv in _states)
            {
                ColorState state = kv.Value;

                if (state.SlowHandleId != handle.Id) continue;

                state.SlowOn = false;
                state.SlowHandleId = 0;
                state.PersistentTint = Color.white;

                // 闪白是独立通道，持续态退场不必等它，也绝不掐掉它
                ApplyPersistentTint(state);

                return;
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

                // 目标被销毁（Unity 假 null）：无从还原，直接摘账，不抛异常
                if (target == null || state.Renderer == null)
                {
                    _recycleScratch.Add(target);

                    continue;
                }

                if (state.FlashRemaining > 0f)
                {
                    state.FlashRemaining -= dt;

                    bool expired = state.FlashRemaining <= 0f;

                    // 必须逐帧重提交：只写一次的话会一直挂在纯白不淡出
                    ApplyFlash(state, expired ? 0f : state.FlashRemaining / state.FlashDuration);

                    if (expired) ApplyPersistentTint(state);
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

                // 材质是共享的，属性块却按渲染器隔离：不清这一块，下一个用同材质的角色会永久纯白
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

            Color effective = !state.FlashOn && state.FlashRemaining > 0f ? state.FlashColor : state.PersistentTint;

            state.Renderer.color = effective * state.BaseColor;
        }

        /// <summary>闪白强度写进属性块；无 _FlashAmount 的材质退回 color 乘法</summary>
        private void ApplyFlash(ColorState state, float amount)
        {
            if (!state.FlashOn || state.Renderer == null) return;

            state.Block.SetFloat(FlashAmountId, amount);
            state.Block.SetColor(FlashColorId, state.FlashColor);
            state.Renderer.SetPropertyBlock(state.Block);
        }

        /// <summary>抓一次真身渲染器并缓存：不缓存的话每次瞬态都要重下探子树；抓不到返回 null 表示这一拍不播、不记账</summary>
        private ColorState GetOrCreateState(Transform target)
        {
            if (_states.TryGetValue(target, out ColorState state))
            {
                if (state.Renderer != null) return state;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();

            // 自身没有才下探子树，且跳过影子件：写的是真身渲染器，脚底的 ShadowView 不能跟着变白
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

            // 渲染器是新建的就重立基线，别把上一具身体的颜色带过来；垫色状态原样保留
            state.Renderer = renderer;
            state.BaseColor = renderer.color;

            // 只读 sharedMaterial 探一次：写 renderer.material 会逐实例克隆材质
            state.FlashOn = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(FlashAmountId);

            return state;
        }
    }
}
