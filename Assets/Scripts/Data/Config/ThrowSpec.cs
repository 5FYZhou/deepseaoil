using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一次投掷的<b>数值</b>（飞行时长 / 弧高 / 距离上下限）。纯数据，由 Luban 的 <c>projectile</c> 行填充。
    /// </summary>
    /// <remarks>
    /// <b>为什么四个数打包成一个结构体：</b>它们是同一次投掷的一组参数（数值分界规则第 2 条
    /// "紧耦合的数值必须同源"），拆成四个 <c>float</c> 参数传进 <c>BallData</c> 时，
    /// 漏一个或传反一个都不会报错，只会让球"飞得莫名地久"。
    /// <para><b>构造时兜底非法值：</b><c>NaN</c> 参与任何比较都是 <c>false</c>，会被一路乘进落点，
    /// 球带着非数坐标消失（白模 W7 钉过这条）。兜底常量<b>不是</b>配置来源 ——
    /// 正常路径永远由表值填充，这里只保证"调用方给出非法值时能看出不对但不崩"。</para>
    /// </remarks>
    public readonly struct ThrowSpec
    {
        /// <summary>最远一投的飞行时长（秒）；近投按距离线性缩短。</summary>
        public readonly float FlightDuration;

        /// <summary>抛物线视觉最高点（世界单位）。</summary>
        public readonly float MaxHeight;

        /// <summary>出手点到落点的最大距离（世界单位）。</summary>
        public readonly float MaxThrowDistance;

        /// <summary>出手点到落点的最小距离（世界单位）；防"鼠标压在脚下"时方向为零产生 NaN。</summary>
        public readonly float MinThrowDistance;

        public ThrowSpec(float flightDuration, float maxHeight, float maxThrowDistance, float minThrowDistance)
        {
            FlightDuration = Positive(flightDuration, 0.6f);
            MaxHeight = NonNegative(maxHeight, 2.0f);
            MaxThrowDistance = Positive(maxThrowDistance, 5.0f);
            MinThrowDistance = Positive(minThrowDistance, 0.4f);

            // 下限必须严格小于上限，否则夹取区间是空的（球会被夹到一个比上限还远的点）。
            if (MinThrowDistance >= MaxThrowDistance) MinThrowDistance = MaxThrowDistance * 0.5f;
        }

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
