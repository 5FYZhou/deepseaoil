using System.Collections.Generic;
using DeepseaOil.Presentation.Primitive;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>HUD 溅射粒子驱动：在锚点下用固定数量的 Image 环形复用，重力下坠 + 缩小淡出</summary>
    /// <remarks>Overlay 画布下粒子渲染器永远排在 UI 之下，只能走 UI 元素。Value=粒子数，Modulation.x=初速（局部单位/秒），Modulation.y=重力，Duration=寿命，Tint=颜色。数量上限就是 rig 容量：同寿命内再次受击会覆盖最旧的粒子，既不丢请求也不告警。</remarks>
    public sealed class HudSparkDriver : IEffectDriver
    {
        private const float DefaultLife = 0.4f;

        private const float DefaultSpeed = 420f;

        private const float DefaultGravity = 1200f;

        private const float DefaultSize = 14f;

        private const float DefaultCount = 8f;

        private const float UpBias = 0.35f;

        private const float Shrink = 0.65f;

        private const string RigName = "[HudSparks]";

        private sealed class Spark
        {
            public Image Image;
            public RectTransform Rect;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Gravity;
            public float Size;
            public float Elapsed;
            public float Life;
            public int PlayId;
        }

        private readonly List<Spark> _sparks = new List<Spark>();

        private readonly int _maxSize;

        private RectTransform _rigRoot;

        private int _nextIndex;
        private int _nextId;
        private int _epoch = 1;
        private bool _disposed;

        internal HudSparkDriver(in EffectSpec spec) : this(spec.MaxSize)
        {
        }

        public HudSparkDriver(int maxSize = 24)
        {
            _maxSize = maxSize > 0 ? maxSize : 1;
        }

        public bool IsSingleton => false;

        /// <summary>空串 = 不需要资源：不预加载、不懒加载、不算缺失</summary>
        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        public int ActiveInstanceCount
        {
            get
            {
                int active = 0;

                for (int i = 0; i < _sparks.Count; i++)
                {
                    if (_sparks[i].Image != null && _sparks[i].Image.enabled) active++;
                }

                return active;
            }
        }

        /// <summary>rig 常驻且数量固定，池恒为空</summary>
        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset)
        {
        }

        /// <summary>ctx.Anchor 非 RectTransform 时返回 None；粒子从锚点矩形中心起跳，挂在锚点下随其一起动</summary>
        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            if (_disposed) return EffectHandle.None;

            RectTransform anchor = ctx.Anchor as RectTransform;

            if (anchor == null) return EffectHandle.None;

            EnsureRig(anchor);

            if (_sparks.Count == 0) return EffectHandle.None;

            int count = Mathf.Clamp(Mathf.RoundToInt(ctx.Value > 0f ? ctx.Value : DefaultCount), 1, _sparks.Count);

            float speed = Sanitize(ctx.Modulation.x, DefaultSpeed);
            float gravity = Sanitize(ctx.Modulation.y, DefaultGravity);
            float life = Sanitize(ctx.Duration, DefaultLife);

            Color tint = ctx.Tint;

            float radius = Mathf.Min(anchor.rect.width, anchor.rect.height) * 0.3f;

            int playId = ++_nextId;

            for (int i = 0; i < count; i++)
            {
                Spark spark = _sparks[_nextIndex];

                _nextIndex = (_nextIndex + 1) % _sparks.Count;

                Vector2 direction = Random.insideUnitCircle;

                direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.up;

                spark.Position = Random.insideUnitCircle * radius;
                spark.Velocity = (direction + Vector2.up * UpBias).normalized * (speed * Random.Range(0.7f, 1.2f));
                spark.Gravity = gravity;
                spark.Size = DefaultSize;
                spark.Elapsed = 0f;
                spark.Life = life;
                spark.PlayId = playId;

                spark.Image.color = tint;
                spark.Image.enabled = true;

                ApplyAt(spark, 0f);
            }

            return new EffectHandle(playId, _epoch, this);
        }

        /// <summary>立刻熄灭本次的粒子：一次溅射跑完自然回收，Stop 只是提前结束它</summary>
        public void Stop(EffectHandle handle)
        {
            if (_disposed) return;
            if (!handle.IsValid || handle.Generation != _epoch) return;

            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];

                if (spark.PlayId != handle.Id) continue;

                spark.PlayId = 0;

                if (spark.Image != null) spark.Image.enabled = false;
            }
        }

        public void Tick(float dt)
        {
            if (_disposed || _sparks.Count == 0) return;

            float step = dt > 0f ? dt : Time.unscaledDeltaTime;

            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];

                if (spark.Image == null || !spark.Image.enabled) continue;

                spark.Elapsed += step;

                float t = spark.Life > 0f ? spark.Elapsed / spark.Life : 1f;

                if (t >= 1f)
                {
                    spark.Image.enabled = false;
                    spark.PlayId = 0;

                    continue;
                }

                spark.Velocity.y -= spark.Gravity * step;
                spark.Position += spark.Velocity * step;

                ApplyAt(spark, t);
            }
        }

        public void CleanAll()
        {
            // 让此前发出的句柄全部失效
            _epoch++;

            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];

                spark.PlayId = 0;

                if (spark.Image != null) spark.Image.enabled = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CleanAll();
            DestroyRig();
        }

        private static void ApplyAt(Spark spark, float t)
        {
            if (spark.Rect == null || spark.Image == null) return;

            float size = spark.Size * (1f - Shrink * t);

            spark.Rect.sizeDelta = new Vector2(size, size);
            spark.Rect.anchoredPosition = spark.Position;

            Color color = spark.Image.color;

            color.a = 1f - t;

            spark.Image.color = color;
        }

        /// <summary>锚点换了或 rig 被销毁（面板重建）就重建：旧 rig 的对象已随锚点一起没了</summary>
        private void EnsureRig(RectTransform anchor)
        {
            if (_rigRoot != null && _rigRoot.parent == anchor) return;

            DestroyRig();

            var go = new GameObject(RigName, typeof(RectTransform));

            go.layer = anchor.gameObject.layer;

            _rigRoot = (RectTransform)go.transform;
            _rigRoot.SetParent(anchor, false);
            _rigRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _rigRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _rigRoot.pivot = new Vector2(0.5f, 0.5f);
            _rigRoot.sizeDelta = Vector2.zero;
            _rigRoot.anchoredPosition = Vector2.zero;

            for (int i = 0; i < _maxSize; i++)
            {
                _sparks.Add(CreateSpark(_rigRoot));
            }
        }

        private void DestroyRig()
        {
            _sparks.Clear();

            if (_rigRoot == null)
            {
                _rigRoot = null;

                return;
            }

            GameObject go = _rigRoot.gameObject;

            _rigRoot = null;

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

        private static Spark CreateSpark(RectTransform parent)
        {
            var go = new GameObject("spark", typeof(RectTransform));

            go.layer = parent.gameObject.layer;

            var rect = (RectTransform)go.transform;

            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(DefaultSize, DefaultSize);

            var image = go.AddComponent<Image>();

            image.sprite = PrimitiveSprites.Circle;
            image.raycastTarget = false;
            image.enabled = false;

            return new Spark { Image = image, Rect = rect };
        }

        private static float Sanitize(float value, float fallback)
        {
            if (float.IsNaN(value) || value <= 0f) return fallback;

            return value;
        }
    }
}
