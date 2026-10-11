using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>通用 GameObject 闪白驱动：按 Follow 取目标身上的 SpriteRenderer，瞬间刷成 Tint，到点还原原色</summary>
    /// <remarks>写的是目标 SpriteRenderer.color 本身，与实体底色共用一条通道，故底色方必须每帧重写它。目标中途被销毁按 Unity 假 null 摘账，不抛异常。发出的句柄其 Id 是目标实例 ID，不是自增编号。</remarks>
    public sealed class FlashDriver : IEffectDriver
    {
        private const float DefaultDuration = 0.12f;

        private readonly Dictionary<int, FlashInstance> _active = new Dictionary<int, FlashInstance>();

        private readonly List<int> _recycleScratch = new List<int>();

        /// <summary>同时闪白数上限，超了丢弃并警告</summary>
        private readonly int _maxSize;

        private int _epoch = 1;
        private bool _disposed;

        internal FlashDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public FlashDriver(int maxSize = 32)
        {
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => false;

        /// <summary>空串 = "不需要资源"：既不预加载也不懒加载，也不会被判为缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount => _active.Count;

        /// <summary>本驱动不持有 GameObject，池恒为空</summary>
        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Follow 为空、或目标上抓不到 SpriteRenderer 时返回 None</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            Transform target = ctx.Follow;

            if (target == null)
            {
                Debug.LogError($"[Flash] {id} 没给 ctx.Follow：闪白没有目标可写，本次不播。");

                return EffectHandle.None;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();

            if (renderer == null) return EffectHandle.None;

            float duration = ctx.Duration > 0f ? ctx.Duration : DefaultDuration;
            Color tint = ctx.Tint;
            int key = target.GetInstanceID();

            if (_active.TryGetValue(key, out FlashInstance existing))
            {
                existing.Elapsed = 0f;
                existing.Duration = duration;

                // 渲染器换过（对象复用重建）：旧渲染器按基准色还原，新渲染器立基准
                if (!ReferenceEquals(existing.Renderer, renderer))
                {
                    if (existing.Renderer != null) existing.Renderer.color = existing.BaseColor;

                    existing.BaseColor = renderer.color;
                }

                existing.Renderer = renderer;

                renderer.color = tint;

                return new EffectHandle(key, existing.Epoch, this);
            }

            if (_active.Count >= _maxSize)
            {
                Debug.LogWarning(
                    $"[Flash] 同时闪白数已达上限 {_maxSize}，丢弃 {id} 的播放请求。调大 EffectCatalog 里这一行的 maxSize 即可。");

                return EffectHandle.None;
            }

            var inst = new FlashInstance
            {
                Epoch = _epoch,
                Target = target,
                Renderer = renderer,
                BaseColor = renderer.color,
                Duration = duration,
            };

            renderer.color = tint;

            _active[key] = inst;

            return new EffectHandle(key, inst.Epoch, this);
        }

        /// <summary>提前还原原色并收回实例；句柄无效或已过期时是 no-op</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;

            if (handle.Generation != _epoch) return;

            if (!_active.TryGetValue(handle.Id, out FlashInstance inst)) return;

            Recycle(handle.Id, inst);
        }

        public void CleanAll()
        {
            // 让此前发出的句柄全部失效
            _epoch++;

            _recycleScratch.Clear();

            foreach (KeyValuePair<int, FlashInstance> kv in _active)
            {
                _recycleScratch.Add(kv.Key);
            }

            for (int i = 0; i < _recycleScratch.Count; i++)
            {
                if (_active.TryGetValue(_recycleScratch[i], out FlashInstance inst)) Recycle(_recycleScratch[i], inst);
            }
        }

        public void Tick(float dt)
        {
            if (_active.Count == 0) return;

            _recycleScratch.Clear();

            foreach (KeyValuePair<int, FlashInstance> kv in _active)
            {
                FlashInstance inst = kv.Value;

                inst.Elapsed += dt;

                // 目标被销毁（Unity 假 null）：无从还原，直接摘账，不抛异常
                if (inst.Target == null || inst.Elapsed >= inst.Duration) _recycleScratch.Add(kv.Key);
            }

            for (int i = 0; i < _recycleScratch.Count; i++)
            {
                if (_active.TryGetValue(_recycleScratch[i], out FlashInstance inst)) Recycle(_recycleScratch[i], inst);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
        }

        // 回收即还原：到期、被 Stop、被 CleanAll、目标被销毁四条路径都汇到这里
        private void Recycle(int key, FlashInstance inst)
        {
            _active.Remove(key);

            if (inst.Renderer != null) inst.Renderer.color = inst.BaseColor;

            inst.Target = null;
            inst.Renderer = null;
        }

        /// <summary>一次闪白的账本，键是目标的实例 ID（句柄的 Id 就是它）</summary>
        private sealed class FlashInstance
        {
            public int Epoch;

            public Transform Target;
            public SpriteRenderer Renderer;

            /// <summary>目标自己的底色，还原时写回它</summary>
            public Color BaseColor;

            public float Elapsed;
            public float Duration;
        }
    }
}
