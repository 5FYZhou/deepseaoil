using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>HUD 节点抖动驱动：给锚点 RectTransform 的 anchoredPosition 叠加衰减随机偏移，到点精确归位</summary>
    /// <remarks>ctx.Anchor 必须是 RectTransform，否则返回 None。基线只在首次抓到目标时记一次：每帧回读现值会把偏移累乘进去，节点再也回不到原位。重播只重置计时不叠加，连击不会抖成两倍。缩放 dt 为 0（暂停/对话）时退回 unscaled，否则血条会冻在偏移位置上。</remarks>
    public sealed class HudShakeDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.18f;

        private const float FalloffPower = 2f;

        private sealed class ShakeState
        {
            public RectTransform Target;

            /// <summary>首次抓到目标时记下的 anchoredPosition，之后只相对它偏移</summary>
            public Vector2 BasePosition;

            public int HandleId;

            public float Elapsed;
            public float Duration;

            /// <summary>峰值幅度，锚点局部 UI 单位</summary>
            public float Amplitude;
        }

        private readonly Dictionary<RectTransform, ShakeState> _states = new Dictionary<RectTransform, ShakeState>();

        private readonly List<RectTransform> _recycleScratch = new List<RectTransform>();

        private readonly int _maxSize;

        private int _nextId;
        private int _epoch = 1;
        private bool _disposed;

        internal HudShakeDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public HudShakeDriver(int maxSize = 4)
        {
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => false;

        /// <summary>空串 = 不需要资源：不预加载、不懒加载、不算缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _states.Count;

        /// <summary>不持有 GameObject，池恒为空</summary>
        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Anchor 非 RectTransform 时返回 None；目标已在抖则重置计时与幅度，不叠加</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            RectTransform target = ctx.Anchor as RectTransform;

            if (target == null) return EffectHandle.None;

            ShakeState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            state.Elapsed = 0f;
            state.Duration = Sanitize(ctx.Duration, DefaultDuration);
            state.Amplitude = Sanitize(ctx.Value, 0f);
            state.HandleId = ++_nextId;

            ApplyAt(state, 0f);

            return new EffectHandle(state.HandleId, _epoch, this);
        }

        /// <summary>立刻归位并摘账：一次抖动跑完自然归位，Stop 只是提前结束它</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;
            if (!handle.IsValid || handle.Generation != _epoch) return;

            RectTransform hit = null;
            bool found = false;

            foreach (KeyValuePair<RectTransform, ShakeState> kv in _states)
            {
                if (kv.Value.HandleId != handle.Id) continue;

                hit = kv.Key;
                found = true;

                break;
            }

            // 出了循环再删：foreach 期间改字典会抛
            if (!found) return;

            if (hit != null) hit.anchoredPosition = _states[hit].BasePosition;

            _states.Remove(hit);
        }

        public void Tick(float dt)
        {
            if (_disposed || _states.Count == 0) return;

            float step = dt > 0f ? dt : Time.unscaledDeltaTime;

            _recycleScratch.Clear();

            foreach (KeyValuePair<RectTransform, ShakeState> kv in _states)
            {
                RectTransform target = kv.Key;
                ShakeState state = kv.Value;

                // 目标被销毁（Unity 假 null）：anchoredPosition 已无从还原，直接摘账
                if (target == null)
                {
                    _recycleScratch.Add(target);

                    continue;
                }

                state.Elapsed += step;

                float t = state.Duration > 0f ? state.Elapsed / state.Duration : 1f;

                if (t >= 1f)
                {
                    target.anchoredPosition = state.BasePosition;
                    _recycleScratch.Add(target);

                    continue;
                }

                ApplyAt(state, t);
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

            foreach (KeyValuePair<RectTransform, ShakeState> kv in _states)
            {
                if (kv.Key != null) kv.Key.anchoredPosition = kv.Value.BasePosition;
            }

            _states.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
        }

        /// <summary>幂次缓出：t=0 满幅、t=1 归零</summary>
        private static void ApplyAt(ShakeState state, float t)
        {
            float ease = Mathf.Pow(1f - t, FalloffPower);

            Vector2 offset = Random.insideUnitCircle * (state.Amplitude * ease);

            state.Target.anchoredPosition = state.BasePosition + offset;
        }

        private ShakeState GetOrCreateState(RectTransform target)
        {
            if (_states.TryGetValue(target, out ShakeState state) && state.Target != null) return state;

            if (_states.Count >= _maxSize)
            {
                Debug.LogWarning(
                    $"[HudShake] 同时抖动的节点数达上限 {_maxSize}，本次请求被丢弃。");

                return null;
            }

            state = new ShakeState
            {
                Target = target,
                BasePosition = target.anchoredPosition,
            };

            _states[target] = state;

            return state;
        }

        private static float Sanitize(float value, float fallback)
        {
            if (float.IsNaN(value) || value <= 0f) return fallback;

            return value;
        }
    }
}
