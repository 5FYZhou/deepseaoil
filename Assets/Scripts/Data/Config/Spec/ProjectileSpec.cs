using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个球种的<b>取值边界</b>：合并「<c>projectile</c> 表行」与「<c>ThrowTuning</c> SO」，
    /// 消费者从它无感取数，不必关心某个语义来自表还是 SO。
    /// </summary>
    /// <remarks>
    /// <b>它不复制表列。</b>本类只持有<b>生成行的引用 ＋ SO 的引用</b>，对外暴露的是
    /// <b>被消费的语义点</b>（按需暴露）—— 没人消费的列不进这里。
    /// 于是"表里加一列"对本类零影响，"换一个数据来源"（表 ↔ SO）只改这一处。
    /// <para><b>生成行不对外暴露。</b>公开它等于把"任意读一列"的口子又还回去，
    /// 那本类的存在理由就消失了。</para>
    /// <para><b>非法值兜底在读取处，不在表里。</b><c>NaN</c> 参与任何比较都是 <c>false</c>，
    /// 会被一路乘进落点，球带着非数坐标消失（白模 W7 钉过这条）。兜底常量<b>不是</b>配置来源：
    /// 正常路径永远由表值填充，这里只保证"拿到非法值时能看出不对但不崩"。</para>
    /// </remarks>
    public sealed class ProjectileSpec
    {
        /// <summary>飞行时长的兜底值（秒）。</summary>
        private const float FallbackFlightDuration = 0.6f;

        /// <summary>弧高的兜底值（世界单位）。</summary>
        private const float FallbackMaxHeight = 2f;

        /// <summary>射程上限的兜底值（世界单位）。</summary>
        private const float FallbackMaxThrowDistance = 5f;

        /// <summary>射程下限的兜底值（世界单位）。</summary>
        private const float FallbackMinThrowDistance = 0.4f;

        /// <summary>观感与冲量的兜底半径（世界单位）；与 <c>ThrowTuning</c> 的字段默认值一致。</summary>
        private const float FallbackBallRadius = 0.22f;

        private readonly Projectile _row;
        private readonly ThrowTuning _tuning;

        /// <param name="row">表行（<c>projectile</c>）。</param>
        /// <param name="tuning">观感与冲量调参；为 <c>null</c> 时全部走代码兜底值。</param>
        /// <param name="visuals">观感颜色表；为 <c>null</c> 时表现层走自己的兜底（白色 / 默认阴影）。</param>
        public ProjectileSpec(Projectile row, ThrowTuning tuning, VisualPalette visuals = null)
        {
            _row = row;
            _tuning = tuning;
            Visuals = visuals;
        }

        /// <summary>球种（= 表主键）。</summary>
        public BallType Type => _row.Id;

        /// <summary>显示名。</summary>
        public string Name => _row.Name;

        /// <summary>最远一投的飞行时长（秒）；近投按距离线性缩短。</summary>
        public float FlightDuration => Positive(_row.FlightDuration, FallbackFlightDuration);

        /// <summary>抛物线视觉最高点（世界单位）。</summary>
        public float MaxHeight => NonNegative(_row.MaxHeight, FallbackMaxHeight);

        /// <summary>出手点到落点的最大距离（世界单位）。</summary>
        public float MaxThrowDistance => Positive(_row.MaxThrowDistance, FallbackMaxThrowDistance);

        /// <summary>
        /// 出手点到落点的最小距离（世界单位）。
        /// </summary>
        /// <remarks><b>下限必须严格小于上限</b>：否则夹取区间是空的，球会被夹到一个比上限还远的点。</remarks>
        public float MinThrowDistance
        {
            get
            {
                float min = Positive(_row.MinThrowDistance, FallbackMinThrowDistance);
                float max = MaxThrowDistance;

                return min < max ? min : max * 0.5f;
            }
        }

        /// <summary>落地后目标格转成的状态；<see cref="TileStateType.Normal"/> = 不改格子。</summary>
        /// <remarks>
        /// <b>"土球现在没有世界效果"是一条配置事实</b>，不是表现层里的一个 <c>if</c>：
        /// 消费者判 <c>TileState != TileStateType.Normal</c> 即可（判据只此一处，两边不会不一致）。
        /// </remarks>
        public TileStateType TileState => _row.TileState;

        /// <summary>本球种的观感与冲量调参（球实体默认吃这一份）。</summary>
        public ThrowTuning Tuning => _tuning;

        /// <summary>观感颜色表（球色 / 阴影色）。表现层从它取色，逻辑层不认识它。</summary>
        public VisualPalette Visuals { get; }

        /// <summary>球本体的视觉半径（世界单位）；调参缺失时退化为代码默认值。</summary>
        public float BallRadius => _tuning != null ? _tuning.ballRadiusMeters : FallbackBallRadius;

        /// <summary>落地是否真的改变世界状态。</summary>
        public bool HasLandingEffect => TileState != TileStateType.Normal;

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
