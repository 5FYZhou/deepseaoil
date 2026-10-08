using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>球种取值边界，合并 projectile 表行与 ThrowTuning SO</summary>
    /// <remarks>非法值在读取处兜底；生成行不对外暴露</remarks>
    public sealed class ProjectileSpec
    {
        private const float FallbackFlightDuration = 0.6f;

        private const float FallbackMaxHeight = 2f;

        private const float FallbackMaxThrowDistance = 5f;

        private const float FallbackMinThrowDistance = 0.4f;

        private const float FallbackBallRadius = 0.22f;

        private readonly Projectile _row;
        private readonly ThrowTuning _tuning;

        public ProjectileSpec(Projectile row, ThrowTuning tuning)
        {
            _row = row;
            _tuning = tuning;
        }

        public BallType Type => _row.Id;

        public string Name => _row.Name;

        /// <summary>最远一投飞行时长，秒，近投按距离线性缩短</summary>
        public float FlightDuration => Positive(_row.FlightDuration, FallbackFlightDuration);

        public float MaxHeight => NonNegative(_row.MaxHeight, FallbackMaxHeight);

        public float MaxThrowDistance => Positive(_row.MaxThrowDistance, FallbackMaxThrowDistance);

        /// <summary>出手点到落点最小距离，世界单位，须小于上限</summary>
        public float MinThrowDistance
        {
            get
            {
                float min = Positive(_row.MinThrowDistance, FallbackMinThrowDistance);
                float max = MaxThrowDistance;

                return min < max ? min : max * 0.5f;
            }
        }

        /// <summary>本球种元素，落地时与落点格地形元素合成决定新状态</summary>
        /// <remarks>球改变世界的唯一依据；Type 当前不参与判定</remarks>
        public ElementValue Element => new ElementValue(_row.Type, _row.Tags, _row.Temp, _row.Wet, _row.Conductive);

        public ThrowTuning Tuning => _tuning;

        /// <remarks>取 ConfigModule.Visuals</remarks>
        public Color BallColor => ConfigModule.Visuals.BallColor(Type);

        public Color ShadowColor => ConfigModule.Visuals.shadow;

        public float BallRadius => _tuning != null ? _tuning.ballRadiusMeters : FallbackBallRadius;

        private static float Positive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
        }

        private static float NonNegative(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? fallback : value;
        }
    }
}
