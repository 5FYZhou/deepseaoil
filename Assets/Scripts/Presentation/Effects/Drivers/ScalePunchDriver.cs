using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>受击挤压驱动：目标 Transform 的 localScale 按 Y 拉伸、X 压缩，按幂次缓出回弹</summary>
    /// <remarks>不像 Tint 那样下探渲染器：ctx.Follow 必须是 View 节点，给 Root 会把朝向镜像一起缩。挂在 View 下的子节点（影子/血条）会继承这份缩放，是节点拓扑决定的。连续受击重置不叠加，目标销毁自动摘账。压缩比与回弹曲线由 ctx 带进来（EnemyTuning 那一行），驱动自己不留硬编码手感。</remarks>
    public sealed class ScalePunchDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.1f;

        private sealed class PunchState
        {
            public Transform Target;

            /// <summary>首次抓到目标时记下的现值，之后只相对它缩放</summary>
            public Vector3 BaseScale;

            public int HandleId;

            public float Elapsed;
            public float Duration;
            public float Amount;

            /// <summary>本次形变的胖瘦与回弹曲线：逐目标存，免得两次播放互相串</summary>
            public float Ratio;

            public float Power;
        }

        private readonly Dictionary<Transform, PunchState> _states = new Dictionary<Transform, PunchState>();

        private readonly List<Transform> _recycleScratch = new List<Transform>();

        private readonly int _maxSize;

        private int _nextId;
        private int _epoch = 1;
        private bool _disposed;

        internal ScalePunchDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public ScalePunchDriver(int maxSize = 32)
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

        /// <summary>ctx.Follow 为空时返回 None；目标已在形变则重置计时与强度，不叠加</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            Transform target = ctx.Follow;

            if (target == null) return EffectHandle.None;

            PunchState state = GetOrCreateState(target);

            if (state == null) return EffectHandle.None;

            state.Elapsed = 0f;
            state.Duration = ctx.Duration > 0f ? ctx.Duration : DefaultDuration;

            // 上限与兜底都在这里收口：调用方给什么，驱动就只信一段合法区间
            state.Amount = Mathf.Clamp(ctx.Intensity, 0f, 1f);
            state.Ratio = Mathf.Clamp(ctx.SquashRatio, 0.05f, 3f);
            state.Power = Mathf.Clamp(ctx.SquashPower, 0.25f, 8f);
            state.HandleId = ++_nextId;

            ApplyAt(state, 0f);

            return new EffectHandle(state.HandleId, _epoch, this);
        }

        /// <summary>立刻还原基线并摘账：一次形变跑完自然归位，Stop 只是提前结束它</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;
            if (!handle.IsValid || handle.Generation != _epoch) return;

            Transform hit = null;
            bool found = false;

            foreach (KeyValuePair<Transform, PunchState> kv in _states)
            {
                if (kv.Value.HandleId != handle.Id) continue;

                if (kv.Key != null) kv.Key.localScale = kv.Value.BaseScale;

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

            foreach (KeyValuePair<Transform, PunchState> kv in _states)
            {
                Transform target = kv.Key;
                PunchState state = kv.Value;

                // 目标被销毁（Unity 假 null）：localScale 已无从还原，直接摘账
                if (target == null)
                {
                    _recycleScratch.Add(target);

                    continue;
                }

                state.Elapsed += dt;

                float t = state.Duration > 0f ? state.Elapsed / state.Duration : 1f;

                if (t >= 1f)
                {
                    target.localScale = state.BaseScale;
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

            foreach (KeyValuePair<Transform, PunchState> kv in _states)
            {
                if (kv.Key != null) kv.Key.localScale = kv.Value.BaseScale;
            }

            _states.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
        }

        /// <summary>幂次缓出：t=0 满力、t=1 归零；Power 越大回弹越脆</summary>
        private static void ApplyAt(PunchState state, float t)
        {
            float ease = Mathf.Pow(1f - t, state.Power);

            float sy = state.BaseScale.y * (1f + state.Amount * ease);
            float sx = state.BaseScale.x * (1f - state.Amount * state.Ratio * ease);

            state.Target.localScale = new Vector3(sx, sy, state.BaseScale.z);
        }

        private PunchState GetOrCreateState(Transform target)
        {
            if (_states.TryGetValue(target, out PunchState state) && state.Target != null) return state;

            if (_states.Count >= _maxSize)
            {
                Debug.LogWarning(
                    $"[Squash] 已形变目标数达上限 {_maxSize}，本次请求被丢弃。调大 EffectCatalog 里这一行的 maxSize 即可。");

                return null;
            }

            // 基线只抓一次：每帧读回现值会逐帧累乘，形变再也归不了位
            state = new PunchState
            {
                Target = target,
                BaseScale = target.localScale,
            };

            _states[target] = state;

            return state;
        }
    }
}
