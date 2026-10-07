using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>归一化口径（全在读取处做，所以 <c>default(EffectContext)</c> 与只写一部分字段的对象初始化器都安全）：<see cref="Direction"/> 零向量读作 <c>Vector2.up</c>；<see cref="Scale"/> / <see cref="Radius"/>（世界单位）<c>&lt;= 0</c> 读作 1；<see cref="Intensity"/> 读时 <c>Clamp01</c>，<b>未显式赋值即 0</b>（要满强度用 <see cref="At(Vector2)"/>）；<see cref="Tint"/> alpha 为 0 读作白色；<see cref="Duration"/>（秒）<b>不归一化</b>，<c>&lt;= 0</c> 由驱动取自己的默认时长。
    /// <b>Intensity 的语义由 Driver 解释</b>（<c>EffectModule</c> 只传递）：<c>ScreenShake</c> 取最大值合并、<c>HitSpark</c> 缩放粒子量与大小；参考实现 <c>ParticleDriver.ApplyIntensity</c>。</remarks>
    public struct EffectContext
    {
        public Vector2 Position;

        private Vector2 _direction;

        public Vector2 Direction
        {
            get => _direction.sqrMagnitude > 0f ? _direction.normalized : Vector2.up;
            set => _direction = value;
        }

        private float _scale;

        public float Scale
        {
            get => _scale > 0f ? _scale : 1f;
            set => _scale = value;
        }

        private float _intensity;

        public float Intensity
        {
            get => Mathf.Clamp01(_intensity);
            set => _intensity = value;
        }

        private Color _tint;

        /// <summary>着色。<b>未设置（alpha 为 0）时读取为白色</b>：透明 = 什么也看不见，且不报错。</summary>
        public Color Tint
        {
            get => _tint.a > 0f ? _tint : Color.white;
            set => _tint = value;
        }

        private float _radius;

        public float Radius
        {
            get => _radius > 0f ? _radius : 1f;
            set => _radius = value;
        }

        private float _duration;

        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        /// <summary>跟随目标，可空；非空时每帧把特效挪到它身上，目标被销毁则自动停止。</summary>
        public Transform Follow;

        public bool FollowRequested => Follow != null;

        public static EffectContext At(Vector2 position)
            => new EffectContext
            {
                Position = position,
                Direction = Vector2.up,
                Scale = 1f,
                Intensity = 1f,
                Follow = null,
            };

        public static EffectContext At(Vector2 position, Vector2 direction)
            => new EffectContext
            {
                Position = position,
                Direction = direction,
                Scale = 1f,
                Intensity = 1f,
                Follow = null,
            };

        /// <summary>跟随一个 Transform 播一次（满强度）。目标为 null 时退化为原点，不抛异常。</summary>
        public static EffectContext OnTarget(Transform target)
            => new EffectContext
            {
                Position = target != null ? (Vector2)target.position : Vector2.zero,
                Direction = Vector2.up,
                Scale = 1f,
                Intensity = 1f,
                Follow = target,
            };

        public static EffectContext Default => At(Vector2.zero);

        public override string ToString()
            => $"EffectContext(pos=({Position.x:F2}, {Position.y:F2}) dir=({Direction.x:F2}, {Direction.y:F2}) " +
               $"scale={Scale:F2} intensity={Intensity:F2} tint={Tint} radius={Radius:F2} duration={Duration:F2} " +
               $"follow={(Follow != null ? Follow.name : "null")})";
    }
}
