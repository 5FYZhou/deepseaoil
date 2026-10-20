using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>UI 闪白驱动：在锚点下叠一张同款剪影的白图，按 alpha 衰减、到点隐藏</summary>
    /// <remarks>ctx.Anchor 必须是自带 Image 的 RectTransform：白图抄它的 sprite 与 preserveAspect，纯矩形白块会盖出硬边，所以拿不到 sprite 时整次跳过。Value=峰值 alpha（默认 0.9），Duration=时长（默认 0.1）。同锚点重播只拉满不叠加；目标销毁只摘账不抛。</remarks>
    public sealed class HudFlashDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.1f;

        private const float DefaultAlpha = 0.9f;

        private const float FadePower = 1.6f;

        private const string OverlayName = "[HudFlash]";

        private sealed class FlashState
        {
            public RectTransform Target;

            public Image Overlay;

            public int HandleId;

            public float Elapsed;
            public float Duration;

            /// <summary>本轮峰值 alpha，重播取大值，不叠加</summary>
            public float Peak;

            public bool Running;
        }

        private readonly Dictionary<RectTransform, FlashState> _states = new Dictionary<RectTransform, FlashState>();

        private readonly List<RectTransform> _recycleScratch = new List<RectTransform>();

        private readonly int _maxSize;

        private int _nextId;
        private int _running;
        private int _epoch = 1;
        private bool _warned;
        private bool _disposed;

        internal HudFlashDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public HudFlashDriver(int maxSize = 4)
        {
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => false;

        /// <summary>空串 = 不需要资源：不预加载、不懒加载、不算缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _running;

        /// <summary>白图常驻在锚点下，池恒为空</summary>
        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Anchor 非 RectTransform 或拿不到 sprite 时返回 None；重播只重置计时并拉满峰值</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            RectTransform target = ctx.Anchor as RectTransform;

            if (target == null) return EffectHandle.None;

            FlashState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            if (!state.Running)
            {
                state.Running = true;
                state.Peak = 0f;

                _running++;
            }

            state.Elapsed = 0f;
            state.Duration = Sanitize(ctx.Duration, DefaultDuration);
            state.Peak = Mathf.Max(state.Peak, Mathf.Clamp01(Sanitize(ctx.Value, DefaultAlpha)));
            state.HandleId = ++_nextId;

            state.Overlay.enabled = true;

            ApplyAt(state, 0f);

            return new EffectHandle(state.HandleId, _epoch, this);
        }

        /// <summary>立刻隐掉白图：一次闪白跑完自然隐藏，Stop 只是提前结束它</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;
            if (!handle.IsValid || handle.Generation != _epoch) return;

            foreach (KeyValuePair<RectTransform, FlashState> kv in _states)
            {
                FlashState state = kv.Value;

                if (!state.Running || state.HandleId != handle.Id) continue;

                Finish(state);

                return;
            }
        }

        public void Tick(float dt)
        {
            if (_disposed || _running == 0) return;

            float step = dt > 0f ? dt : Time.unscaledDeltaTime;

            _recycleScratch.Clear();

            foreach (KeyValuePair<RectTransform, FlashState> kv in _states)
            {
                FlashState state = kv.Value;

                // 目标被销毁（Unity 假 null）：白图已随之消失，直接摘账
                if (kv.Key == null || state.Overlay == null)
                {
                    if (state.Running)
                    {
                        state.Running = false;

                        _running--;
                    }

                    state.Peak = 0f;

                    _recycleScratch.Add(kv.Key);

                    continue;
                }

                if (!state.Running) continue;

                state.Elapsed += step;

                float t = state.Duration > 0f ? state.Elapsed / state.Duration : 1f;

                if (t >= 1f)
                {
                    Finish(state);

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

            foreach (KeyValuePair<RectTransform, FlashState> kv in _states)
            {
                FlashState state = kv.Value;

                state.Running = false;
                state.Peak = 0f;

                if (state.Overlay != null) state.Overlay.enabled = false;
            }

            _running = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();

            foreach (KeyValuePair<RectTransform, FlashState> kv in _states)
            {
                if (kv.Value.Overlay != null) DestroyOverlay(kv.Value.Overlay);
            }

            _states.Clear();
        }

        private static void ApplyAt(FlashState state, float t)
        {
            if (state.Overlay == null) return;

            float fade = Mathf.Pow(1f - t, FadePower);

            state.Overlay.color = new Color(1f, 1f, 1f, state.Peak * fade);
        }

        private void Finish(FlashState state)
        {
            if (state.Running)
            {
                state.Running = false;

                _running--;
            }

            state.Peak = 0f;

            if (state.Overlay != null) state.Overlay.enabled = false;
        }

        private FlashState GetOrCreateState(RectTransform target)
        {
            if (_states.TryGetValue(target, out FlashState state) && state.Overlay != null) return state;

            if (state == null && _states.Count >= _maxSize)
            {
                WarnOnce($"[HudFlash] 同时闪白的节点数达上限 {_maxSize}，本次请求被丢弃。");

                return null;
            }

            Image source = target.GetComponent<Image>();

            if (source == null || source.sprite == null)
            {
                WarnOnce("[HudFlash] 锚点没有 Image 或 sprite，闪白会盖成白块，本次跳过。");

                return null;
            }

            if (state == null) state = new FlashState();

            state.Target = target;
            state.Overlay = CreateOverlay(target, source);
            state.Running = false;
            state.Peak = 0f;

            _states[target] = state;

            return state;
        }

        /// <summary>白图铺满锚点矩形并复制美术参数：矩形与 sprite 一致，剪影才和底图严丝合缝</summary>
        private static Image CreateOverlay(RectTransform target, Image source)
        {
            var go = new GameObject(OverlayName, typeof(RectTransform));

            go.layer = target.gameObject.layer;

            var rect = (RectTransform)go.transform;

            rect.SetParent(target, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var overlay = go.AddComponent<Image>();

            overlay.sprite = source.sprite;
            overlay.type = source.type;
            overlay.preserveAspect = source.preserveAspect;
            overlay.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            overlay.raycastTarget = false;
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.enabled = false;

            return overlay;
        }

        private static void DestroyOverlay(Image overlay)
        {
            GameObject go = overlay.gameObject;

            if (Application.isPlaying)
            {
                Object.Destroy(go);
            }
            else
            {
                // 编辑模式下 Object.Destroy 会打警告且延迟生效
                Object.DestroyImmediate(go);
            }
        }

        private void WarnOnce(string message)
        {
            if (_warned) return;

            _warned = true;

            Debug.LogWarning(message);
        }

        private static float Sanitize(float value, float fallback)
        {
            if (float.IsNaN(value) || value <= 0f) return fallback;

            return value;
        }
    }
}
