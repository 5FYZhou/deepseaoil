using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 抛物线几何：<b>两端精确贴地、顶点恰好为 1</b>的归一化弧高。
    /// </summary>
    /// <remarks>
    /// <b>为什么值得放进地基：</b>这个式子在两处各写过一遍（球的飞行 <c>BallData.SampleHeight01</c>
    /// 与掉落物的抛物线 <c>WaterBallDrop</c>），而它们的语义必须一致 ——
    /// "两端恰好为 0"是"落地那一刻高度精确归零"的依据，两份实现漂了就会出现
    /// "球看起来浮在地上"或"掉落物陷进地面"这类只有肉眼能发现的偏差。
    /// <para><b>为什么用 <c>4t(1−t)</c> 而不是 <c>sin(πt)</c>：</b>两者都是过两端、顶峰为 1 的对称曲线，
    /// 但前者是多项式，与 <c>Vector2.Lerp</c> 的线性项同为代数式，测试能做精确断言，
    /// 不会因浮点三角函数实现差异出现假红。</para>
    /// <para><b>它不夹 <c>t</c></b>：越界那一帧由调用方处理（球是"直接结算落地"、
    /// 掉落物是"落到落点"）。夹在这里会掩盖调用方的越界，而那种掩盖不报错。</para>
    /// </remarks>
    public static class Ballistics
    {
        /// <summary>归一化弧高：<c>t = 0.5</c> 恰好为 1，两端恰好为 0。</summary>
        /// <param name="t">已飞比例（调用方负责夹到 0..1）。</param>
        public static float ArcHeight01(float t)
        {
            return 4f * t * (1f - t);
        }
    }
}
