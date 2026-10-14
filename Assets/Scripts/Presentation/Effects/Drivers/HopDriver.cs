using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>击退伪高度：Anchor.localPosition.y 抬一次抛物线再归零</summary>
    /// <remarks>只写 localPosition.y，不碰 Root 与 localScale；与 ScalePunchDriver 同帧不冲突（不同轴）。Value 语义 = v0 / hopReferenceSpeed，NaN/Inf/≤0 拒播，不 clamp 上限。</remarks>
    public sealed class HopDriver : IEffectDriver
    {
        private const float DropWarnInterval = 1f;

        private sealed class HopState
        {
            public Transform Target;

            /// <summary>首次抓到目标时记下的现值，之后只相对它抬升</summary>
            public float BaseY;

            public float Elapsed;
            public float Duration;
            public float Peak;
            public int HandleId;
        }

        private readonly Dictionary<Transform, HopState> _states = new Dictionary<Transform, HopState>();

        private readonly List<Transform> _recycleScratch = new List<Transform>();

        private readonly int _maxSize;

        private int _nextId;
        private int _epoch = 1;
        private int _droppedSinceWarn;
        private float _nextDropWarnTime;
        private bool _disposed;

        internal HopDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public HopDriver(int maxSize = 32)
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

        /// <summary>ctx.Anchor 必填；Value 非法（NaN/Inf/≤0）拒播；目标已在抬升则重置计时与峰值，基线不动</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            Transform target = ctx.Anchor;

            if (target == null) return EffectHandle.None;

            // 非数/无穷写进 localPosition 会污染整棵子树的世界坐标；负值等于向下穿地
            float value = ctx.Value;

            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return EffectHandle.None;

            VisualFeelTuning feel = ConfigModule.VisualFeel;

            HopState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            state.Elapsed = 0f;
            state.Duration = ctx.Duration > 0f ? ctx.Duration : feel.hopDuration;
            state.Peak = feel.hopPeakHeight * value;
            state.HandleId = ++_nextId;

            ApplyAt(state, 0f);

            return new EffectHandle(state.HandleId, _epoch, this);
        }

        /// <summary>提前落地：立刻还原基线并摘账，与抛物线自然跑完是同一条还原路径</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;
            if (!handle.IsValid || handle.Generation != _epoch) return;

            Transform hit = null;
            bool found = false;

            foreach (KeyValuePair<Transform, HopState> kv in _states)
            {
                if (kv.Value.HandleId != handle.Id) continue;

                Restore(kv.Value);

                hit = kv.Key;
                found = true;

                break;
            }

            // 出了循环再删：foreach 期间改字典会抛
            if (found) _states.Remove(hit);
        }

        public void Tick(float dt)
        {
            if (_states.Count == 0) return;

            _recycleScratch.Clear();

            foreach (KeyValuePair<Transform, HopState> kv in _states)
            {
                Transform target = kv.Key;

                // 目标被销毁（Unity 假 null）：写 localPosition 会刷报错，也无从还原
                if (target == null)
                {
                    _recycleScratch.Add(target);

                    continue;
                }

                HopState state = kv.Value;

                state.Elapsed += dt;

                float t = state.Duration > 0f ? state.Elapsed / state.Duration : 1f;

                if (t >= 1f)
                {
                    Restore(state);
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

            foreach (KeyValuePair<Transform, HopState> kv in _states)
            {
                Restore(kv.Value);
            }

            _states.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
        }

        /// <summary>只改 y：x / z 原样写回，同帧还有别的驱动在动这一节点</summary>
        private static void ApplyAt(HopState state, float t)
        {
            float arc = Ballistics.ArcHeight01(Mathf.Clamp01(t));

            Vector3 p = state.Target.localPosition;
            p.y = state.BaseY + state.Peak * arc;
            state.Target.localPosition = p;
        }

        private static void Restore(HopState state)
        {
            if (state.Target == null) return;

            Vector3 p = state.Target.localPosition;
            p.y = state.BaseY;
            state.Target.localPosition = p;
        }

        private void WarnPoolFull(EffectId id)
        {
            _droppedSinceWarn++;

            float now = Time.unscaledTime;
            if (now < _nextDropWarnTime) return;

            _nextDropWarnTime = now + DropWarnInterval;
            Debug.LogWarning($"[Hop] 已抬升目标数达上限 {_maxSize}，丢弃 {id} 的播放请求。" +
                             $"距上次警告累计丢弃 {_droppedSinceWarn} 次——调大 EffectCatalog 里这一行的 maxSize 即可。");
            _droppedSinceWarn = 0;
        }

        private HopState GetOrCreateState(Transform target)
        {
            if (_states.TryGetValue(target, out HopState state)) return state;

            if (_states.Count >= _maxSize)
            {
                WarnPoolFull(EffectId.Hop);

                return null;
            }

            // 基线只抓一次：重复受击若重抓现值，会把自己上一轮的抬升当成地面，越打越高
            state = new HopState
            {
                Target = target,
                BaseY = target.localPosition.y,
            };

            _states[target] = state;

            return state;
        }
    }
}
