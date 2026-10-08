using cfg.dso;
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

        // 抛物线与投掷距离已从表移交 ThrowTuning SO：手感是程序调参，改一次不该导表。
        // 属性名与类型对上层保持不变，屏蔽这条边界的就是这四个转发。

        /// <summary>最远一投飞行时长，秒，近投按距离线性缩短</summary>
        /// <remarks>取 ThrowTuning.flightDuration；非法值（NaN/Inf/&lt;=0）退回本文件常量</remarks>
        public float FlightDuration => _tuning != null ? Positive(_tuning.flightDuration, FallbackFlightDuration) : FallbackFlightDuration;

        /// <summary>抛物线视觉最高点，世界单位</summary>
        /// <remarks>取 ThrowTuning.maxHeight；负值退回本文件常量</remarks>
        public float MaxHeight => _tuning != null ? NonNegative(_tuning.maxHeight, FallbackMaxHeight) : FallbackMaxHeight;

        /// <summary>最远投掷距离，世界单位，也是下落时长的距离上限</summary>
        /// <remarks>取 ThrowTuning.maxThrowDistance；非法值退回本文件常量</remarks>
        public float MaxThrowDistance => _tuning != null ? Positive(_tuning.maxThrowDistance, FallbackMaxThrowDistance) : FallbackMaxThrowDistance;

        /// <summary>出手点到落点最小距离，世界单位，须小于上限</summary>
        /// <remarks>取 ThrowTuning.minThrowDistance；不小于上限时折半上限，避免除法趋零</remarks>
        public float MinThrowDistance
        {
            get
            {
                float min = _tuning != null ? Positive(_tuning.minThrowDistance, FallbackMinThrowDistance) : FallbackMinThrowDistance;
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
