using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 一颗在飞的球的全部状态：起点、落点、已飞比例、总时长、弧高。**不可变**。
    /// </summary>
    /// <remarks>
    /// <b>本类型不吃 MonoBehaviour、不吃 Time、不吃 Camera</b>，所以 EditMode 测试能直接调
    /// <see cref="SampleGround"/> / <see cref="SampleVisual"/> 把"抛物线起点终点精确落地""最高点等于 MAX_HEIGHT"
    /// 钉在代码上而不是钉在注释上。
    /// <para><b>伪高度是什么：</b>球没有物理体、没有重力，世界坐标里它始终在 <see cref="SampleGround"/>
    /// 那条<b>直线</b>上（这就是"逻辑位置"，阴影走这条线）；抛物线只体现在
    /// <see cref="SampleVisual"/> 多出来的那个 y 偏移上（这就是"视觉位置"，球体走这条线）。
    /// 于是"落地判定"是一次纯数学的 <c>t ≥ 1</c>，不需要碰撞检测，也不需要射线。</para>
    /// </remarks>
    public readonly struct BallData
    {
        /// <summary>球的种类（决定颜色与后续效果）。</summary>
        public readonly BallType Type;

        /// <summary>出手点的<b>贴地逻辑位置</b>（<b>不含</b> <see cref="ThrowConstants.THROW_ORIGIN_HEIGHT"/>）。</summary>
        public readonly Vector2 Start;

        /// <summary>落点的<b>贴地逻辑位置</b>（<b>不含</b>出手抬高量）。这是唯一可信的落点真值。</summary>
        public readonly Vector2 End;

        /// <summary>飞行总时长（秒）。已按飞行距离缩放，见构造函数。</summary>
        public readonly float Duration;

        /// <summary>抛物线视觉最高点（相对 <see cref="SampleGround"/> 的抬升量）。</summary>
        public readonly float MaxHeight;

        /// <summary>
        /// 由"出手点 + 目标点 + 飞行距离"构造。
        /// </summary>
        /// <param name="origin">出手点（玩家位置，不含抬高量）。</param>
        /// <param name="target">目标点（鼠标位置，不含抬高量）。会被夹到
        /// [<see cref="ThrowConstants.MIN_THROW_DISTANCE"/>, +∞) 上，见下。</param>
        /// <param name="distance">
        /// 本次飞行的实际距离，由调用方用<b>同一个</b> clamp 函数算出
        /// （<c>ThrowSpawner.ClampThrowPoint</c> 的 <c>out</c> 值）后传进来。
        /// </param>
        /// <remarks>
        /// <b>为什么时长按距离缩放：</b>如果无论投多远都固定 <see cref="ThrowConstants.FLIGHT_DURATION"/> 秒，
        /// 贴脸投的球会以极慢的速度飘出去。"每颗球都飞出同样的速度、远投就是飞得久"才符合手感直觉，
        /// 也让玩家能靠飞行时间预判落点。
        /// <para><b>为什么距离要由调用方传、而不是本类型自己算：</b>距离若在这里重算一遍，就会出现
        /// "指示器 clamp 了、球没 clamp"或反过来（两处 clamp 不一致）的可能。让调用方把已经算好的距离递进来，
        /// 就只有一处 clamp，时长与落点必然自洽。</para>
        /// <para><b>为什么目标点仍有下限：</b>鼠标压在玩家身上时目标点等于出手点，方向向量为零，
        /// 归一化会产生 NaN（球会消失或闪成非数坐标）。下限取
        /// <see cref="ThrowConstants.MIN_THROW_DISTANCE"/>，表现为"往脚前扔"。这条保底与调用方无关，
        /// 因为它是本类型的构造函数契约，任何调用方都得遵守。</para>
        /// <para><b>出手抬高量不在这里叠加：</b><see cref="Start"/> / <see cref="End"/> 是贴地的逻辑位置
        /// （阴影贴的就是它们），抬高量由 <c>BallDriver</c> 画球时加到视觉位置上。
        /// 曾经把它烘进这两个字段，结果阴影跟着球一起离地并随抛物线起伏 —— 见
        /// <see cref="ThrowConstants.THROW_ORIGIN_HEIGHT"/> 的注释。</para>
        /// </remarks>
        public BallData(BallType type, Vector2 origin, Vector2 target, float distance)
        {
            Vector2 towards = target - origin;
            float actual = towards.magnitude;

            if (actual < ThrowConstants.MIN_THROW_DISTANCE)
            {
                // 距离为 0 时方向取"右"：给一个确定的方向，比留 NaN 或让球原地不动都更好排查。
                Vector2 direction = actual > 1e-6f ? towards / actual : Vector2.right;
                target = origin + direction * ThrowConstants.MIN_THROW_DISTANCE;
                distance = ThrowConstants.MIN_THROW_DISTANCE;
            }

            Type = type;
            Start = origin;
            End = target;
            MaxHeight = ThrowConstants.MAX_HEIGHT;

            // 先除以 MAX_THROW_DISTANCE 归一化：最远一投恰好是 FLIGHT_DURATION 秒，其余按比例缩短。
            // 最后一个 max 是防"传进来的距离为 0 或负"把时长变成 0（那会让球在首帧就落地）。
            Duration = ThrowConstants.FLIGHT_DURATION
                * (Mathf.Max(distance, ThrowConstants.MIN_THROW_DISTANCE) / ThrowConstants.MAX_THROW_DISTANCE);
        }

        /// <summary>
        /// 逻辑位置：起点到落点的<b>直线</b>插值。阴影贴这条线走。
        /// </summary>
        /// <param name="t">已飞比例；调用方负责夹到 0..1（<c>BallDriver</c> 在越界那一帧直接结算落地）。</param>
        public Vector2 SampleGround(float t)
        {
            return Vector2.Lerp(Start, End, t);
        }

        /// <summary>
        /// 视觉位置：逻辑位置 + 弧高。
        /// </summary>
        /// <remarks>
        /// <c>t = 0</c> 与 <c>t = 1</c> 的高度都<b>恰好</b>为 0（<c>4t(1−t)</c> 在两端的值都是 0），
        /// 所以"球落地的瞬间"视觉高度精确归零，不会浮在空中，也不会陷进地面。
        /// </remarks>
        public Vector2 SampleVisual(float t)
        {
            return SampleGround(t) + Vector2.up * (MaxHeight * SampleHeight01(t));
        }

        /// <summary>
        /// 归一化弧高：<c>t = 0.5</c> 时恰好为 1，两端恰好为 0。
        /// </summary>
        /// <remarks>
        /// 用 <c>4t(1−t)</c> 而不是 <c>sin(πt)</c>：两者都是过两端、顶峰为 1 的对称曲线，但前者是多项式，
        /// 与 <c>Vector2.Lerp</c> 的线性项同为代数式，测试里能对 <c>4×0.25×0.75 = 0.75</c> 这类值做精确断言，
        /// 不会因为浮点三角函数实现差异出现假红。
        /// </remarks>
        public static float SampleHeight01(float t)
        {
            return 4f * t * (1f - t);
        }
    }
}
