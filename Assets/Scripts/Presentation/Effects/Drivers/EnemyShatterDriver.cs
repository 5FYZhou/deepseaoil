using System.Collections.Generic;
using DeepseaOil.Foundation;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 敌人碎裂驱动：耐久归零时飞出的几块碎片，沿 <c>ctx.Direction</c> 扇形散开。<b>程序生成，不需要资源</b>。
    /// </summary>
    /// <remarks>
    /// <b>它不是特效，是读数。</b>"三下打碎"这条规则如果没有可见的表现，就只能在 Console 里靠日志确认。
    /// 同 <see cref="LandingRingDriver"/>：需求把"音效、粒子、拖尾"列为不做，本类不属于那一类 ——
    /// 它不表达情绪，只暴露"这个敌人死了"。
    /// <para><b>碎片的形状是确定性算出来的，不读随机数。</b>均匀扇形 ＋ 一个固定的俯仰系数：
    /// 三块碎片每次都在同样的相对位置上飞出去。随机数会让"同一个现象能否再出现一次"变成赌博，
    /// 而排查全靠重现。</para>
    /// <para>碎片各自走池：一次碎裂占 <see cref="PieceCount"/> 个对象，归还时逐个还回去。</para>
    /// </remarks>
    public sealed class EnemyShatterDriver : IEffectDriver
    {
        /// <summary>碎片数。</summary>
        private const int PieceCount = 3;

        /// <summary>碎片半径（世界单位）。</summary>
        private const float PieceRadiusMeters = 0.13f;

        /// <summary>碎片飞行速度（单位/秒）。</summary>
        private const float PieceSpeed = 3.5f;

        /// <summary>扇形的总张角（度）。</summary>
        private const float SpreadDegrees = 140f;

        /// <summary>默认时长（秒）；<c>ctx.Duration</c> 未给时用它。</summary>
        private const float DefaultDuration = 0.35f;

        /// <summary>时长上限（秒）。</summary>
        private const float MaxDuration = 5f;

        /// <summary>单帧最多回收几个碎裂（防尖峰）。</summary>
        private const int MaxRecyclePerTick = 16;

        /// <summary>一块在飞的碎片。</summary>
        private sealed class Shard
        {
            public GameObject Go;
            public Vector2 Velocity;
        }

        /// <summary>一次碎裂（若干碎片的集合）。</summary>
        private sealed class Burst
        {
            public int Id;
            public int Epoch;
            public readonly List<Shard> Shards = new List<Shard>(PieceCount);
            public float Elapsed;
            public float Duration;
        }

        private readonly Transform _root;
        private readonly int _maxSize;
        private readonly Pool<GameObject> _piecePool;
        private readonly List<Burst> _active = new List<Burst>();
        private readonly List<Burst> _recycleScratch = new List<Burst>();

        private int _nextId;
        private int _epoch;
        private bool _disposed;

        /// <summary>
        /// 由装配表构造（<c>EffectDriverFactory</c> 用）。
        /// </summary>
        /// <remarks>
        /// <c>internal</c> 是因为 <c>EffectSpec</c> 本身是 internal —— 公开它会让"装配参数"变成对外 API。
        /// 需要手工构造时用下面那个公开重载。
        /// </remarks>
        internal EnemyShatterDriver(in EffectSpec spec, Transform root) : this(root, spec.MaxSize)
        {
        }

        /// <param name="root">特效根（实例都挂在它下面，销毁根即回收）。</param>
        /// <param name="maxSize">同屏碎裂次数上限；<c>&le; 0</c> 视为 1。</param>
        public EnemyShatterDriver(Transform root, int maxSize = 16)
        {
            _root = root;
            _maxSize = maxSize > 0 ? maxSize : 1;

            _piecePool = new Pool<GameObject>(
                factory: CreatePieceObject,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "EnemyShatterPiece",
                maxSize: _maxSize * PieceCount,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                onDestroy: null);
        }

        /// <inheritdoc />
        public bool IsSingleton => false;

        /// <inheritdoc />
        public string AssetKey => string.Empty;

        /// <inheritdoc />
        public bool IsAssetReady => true;

        /// <inheritdoc />
        public int ActiveInstanceCount => _active.Count;

        /// <inheritdoc />
        public int PooledObjectCount => _piecePool != null ? _piecePool.IdleCount : 0;

        /// <inheritdoc />
        public void OnAssetLoaded(Object asset)
        {
            // 不需要资源。
        }

        /// <inheritdoc />
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            var burst = new Burst
            {
                Id = ++_nextId,
                Epoch = _epoch,
                Elapsed = 0f,
                Duration = Sanitize(ctx.Duration),
            };

            Vector2 forward = ctx.Direction;

            // 受击方向为零时 <c>ctx.Direction</c> 已经归一为 up，这里只需要角度。
            float baseAngle = Mathf.Atan2(forward.y, forward.x);

            float spread = SpreadDegrees;
            int count = Mathf.Max(1, PieceCount);

            Color color = ctx.Tint;
            float scale = Mathf.Max(ctx.Scale, 1e-3f);

            for (int i = 0; i < count; i++)
            {
                if (!_piecePool.TryGet(out GameObject go) || go == null)
                {
                    // 池满：已经借出的碎片照常飞完，本次少几块 —— 不抛异常、不吞掉整次播放。
                    Debug.LogWarning($"[Effect] EnemyShatter 碎片池已满（上限 {_maxSize * PieceCount}），本次少飞 {count - i} 块。");
                    break;
                }

                // 均匀铺在 [-spread/2, +spread/2] 上；count = 1 时正好沿受击方向。
                float offset = count == 1 ? 0f : -spread * 0.5f + spread * i / (count - 1);

                float radians = baseAngle + offset * Mathf.Deg2Rad;

                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                var renderer = go.GetComponent<SpriteRenderer>();

                if (renderer != null)
                {
                    // 用正圆而不是贴地形状：碎片是**飞在空中**的物体，压扁它会让它看起来像躺在地上。
                    PrimitiveSprites.Configure(
                        renderer,
                        PrimitiveSprites.Circle,
                        color,
                        RenderOrder.ShatterPiece,
                        PieceRadiusMeters * 2f * scale);
                }

                go.transform.position = new Vector3(ctx.Position.x, ctx.Position.y, 0f);

                burst.Shards.Add(new Shard { Go = go, Velocity = direction * PieceSpeed });
            }

            _active.Add(burst);

            return new EffectHandle(burst.Id, burst.Epoch, this);
        }

        /// <inheritdoc />
        public void Stop(EffectHandle handle)
        {
            if (!handle.IsValid) return;
            if (handle.Generation != _epoch) return;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != handle.Id) continue;

                Recycle(_active[i]);
                _active.RemoveAt(i);
                return;
            }
        }

        /// <inheritdoc />
        public void CleanAll()
        {
            _epoch++;

            for (int i = 0; i < _active.Count; i++)
            {
                Recycle(_active[i]);
            }

            _active.Clear();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_disposed || _active.Count == 0) return;

            _recycleScratch.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                Burst burst = _active[i];

                burst.Elapsed += dt;

                if (burst.Elapsed >= burst.Duration)
                {
                    _recycleScratch.Add(burst);
                    continue;
                }

                MoveShards(burst, dt);
            }

            if (_recycleScratch.Count == 0) return;

            for (int i = 0; i < _recycleScratch.Count && i < MaxRecyclePerTick; i++)
            {
                Burst burst = _recycleScratch[i];

                Recycle(burst);
                _active.Remove(burst);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();

            _piecePool.Dispose();
        }

        /// <summary>
        /// 碎片直线飞行，恒速、不衰减 —— 碎片是"散出去"，不是"滑出去"。
        /// </summary>
        /// <remarks>
        /// 写 <c>transform.position</c> 而不是给刚体加速度：碎片不需要碰撞、不需要被推，
        /// 一个刚体只会让它去参与物理解算。
        /// </remarks>
        private static void MoveShards(Burst burst, float dt)
        {
            for (int i = 0; i < burst.Shards.Count; i++)
            {
                Shard shard = burst.Shards[i];

                if (shard.Go == null) continue;

                Vector3 p = shard.Go.transform.position;

                shard.Go.transform.position = new Vector3(
                    p.x + shard.Velocity.x * dt,
                    p.y + shard.Velocity.y * dt,
                    p.z);
            }
        }

        private void Recycle(Burst burst)
        {
            for (int i = 0; i < burst.Shards.Count; i++)
            {
                GameObject go = burst.Shards[i].Go;

                if (go == null) continue;

                _piecePool.Release(go);
            }

            burst.Shards.Clear();
        }

        private static float Sanitize(float duration)
        {
            if (float.IsNaN(duration) || duration <= 0f) return DefaultDuration;

            return Mathf.Min(duration, MaxDuration);
        }

        private GameObject CreatePieceObject()
        {
            var go = new GameObject("碎片");

            go.layer = RenderOrder.OverlayLayer;

            if (_root != null) go.transform.SetParent(_root, false);

            go.AddComponent<SpriteRenderer>();
            go.SetActive(false);

            return go;
        }
    }
}
