using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>一个球种的<b>取值边界</b>：合并「<c>projectile</c> 表行」与「<c>ThrowTuning</c> SO」，消费者从它无感取数。</summary>
    /// <remarks>
    /// 非法值兜底在读取处，不在表里：<c>NaN</c> 参与任何比较都是 <c>false</c>，会被一路乘进落点，球带着非数坐标消失；兜底常量只保证"拿到非法值时不崩"。生成行不对外暴露：公开它等于把"任意读一列"的口子还回去。
    /// </remarks>
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

        /// <summary>最远一投的飞行时长（秒）；近投按距离线性缩短。</summary>
        public float FlightDuration => Positive(_row.FlightDuration, FallbackFlightDuration);

        public float MaxHeight => NonNegative(_row.MaxHeight, FallbackMaxHeight);

        public float MaxThrowDistance => Positive(_row.MaxThrowDistance, FallbackMaxThrowDistance);

        /// <summary>出手点到落点的最小距离（世界单位）；下限必须严格小于上限，否则夹取区间是空的，球会被夹到一个比上限还远的点。</summary>
        public float MinThrowDistance
        {
            get
            {
                float min = Positive(_row.MinThrowDistance, FallbackMinThrowDistance);
                float max = MaxThrowDistance;

                return min < max ? min : max * 0.5f;
            }
        }

        /// <summary>本球种携带的<b>元素</b>（温度 / 湿度 / 导电 / 标签）：落地时与落点格的地形元素合成，结果决定新状态。</summary>
        /// <remarks>它是"球能改变世界"的唯一依据：旧链路的 <c>tile_state</c> 列已随上游表改版删除（球不再直接指定目标状态）。<c>Type</c> 当前不参与判定（D13）。</remarks>
        public ElementValue Element => new ElementValue(_row.Type, _row.Tags, _row.Temp, _row.Wet, _row.Conductive);

        public ThrowTuning Tuning => _tuning;

        /// <summary>本球种的本体颜色；唯一权威来源是 <c>ConfigModule.Visuals</c>，本属性只是把它按球种解析一次。</summary>
        public Color BallColor => ConfigModule.Visuals.BallColor(Type);

        /// <summary>贴地阴影色（球与掉落物的影子共用）。</summary>
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
