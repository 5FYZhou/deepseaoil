using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>归一化均在读取处做：Direction 零向量=Vector2.up；Scale/Radius（世界单位）≤0=1；Intensity 读时 Clamp01；Tint alpha=0=白色；Duration（秒）不归一化，≤0 由驱动取默认时长。Intensity 语义由 Driver 解释（EffectModule 只传递）：ScreenShake 取最大值合并，HitSpark 缩放粒子量与大小，参考 ParticleDriver.ApplyIntensity。</remarks>
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

        /// <summary>着色，未设置（alpha=0）时读作白色</summary>
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

        /// <summary>跟随目标播一次（满强度）；目标 null 退化为原点，不抛异常</summary>
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
