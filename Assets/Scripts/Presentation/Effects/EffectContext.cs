using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>归一化均在读取处做：Direction 零向量=Vector2.up；Tint alpha=0=白色；Duration ≤0 由驱动取默认时长；Value 与 Modulation 原值直传，上下限由各驱动自持。</remarks>
    public struct EffectContext
    {
        public Vector2 Position;

        private Vector2 _direction;

        public Vector2 Direction
        {
            get => _direction.sqrMagnitude > 0f ? _direction.normalized : Vector2.up;
            set => _direction = value;
        }

        private Color _tint;

        /// <summary>着色，未设置读作白</summary>
        public Color Tint
        {
            get => _tint.a > 0f ? _tint : Color.white;
            set => _tint = value;
        }

        private float _duration;

        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        private float _value;

        /// <summary>主数值，驱动自解释语义，读时不做归一</summary>
        public float Value
        {
            get => _value;
            set => _value = value;
        }

        private Vector2 _modulation;

        /// <summary>主数值的修饰量，x/y 语义按驱动，读时不做归一</summary>
        public Vector2 Modulation
        {
            get => _modulation;
            set => _modulation = value;
        }

        public Transform Anchor;

        public static EffectContext At(Vector2 position)
            => new EffectContext
            {
                Position = position,
                Direction = Vector2.up,
                Value = 1f,
                Anchor = null,
            };

        public static EffectContext At(Vector2 position, Vector2 direction)
            => new EffectContext
            {
                Position = position,
                Direction = direction,
                Value = 1f,
                Anchor = null,
            };

        /// <summary>跟随目标播一次；目标 null 退化为原点，不抛异常</summary>
        public static EffectContext OnTarget(Transform target)
            => new EffectContext
            {
                Position = target != null ? (Vector2)target.position : Vector2.zero,
                Direction = Vector2.up,
                Value = 1f,
                Anchor = target,
            };

        public static EffectContext Default => At(Vector2.zero);

        public override string ToString()
            => $"EffectContext(pos=({Position.x:F2}, {Position.y:F2}) dir=({Direction.x:F2}, {Direction.y:F2}) " +
               $"value={Value:F2} modulation=({Modulation.x:F2}, {Modulation.y:F2}) tint={Tint} duration={Duration:F2} " +
               $"anchor={(Anchor != null ? Anchor.name : "null")})";
    }
}
