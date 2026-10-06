using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 投掷与瞄准的<b>程序调参</b>：观感、射程以外的半径、冲量强度。
    /// </summary>
    /// <remarks>
    /// <b>为什么这些不进 Luban：</b>数值分界规则第 1 条 —— 策划调参走表，程序调参走 SO。
    /// 这里的每一个数都是"边跑边看手感"时改的（球的视觉半径、瞄准环压扁多少、推力多大），
    /// 改它们不需要经过导表，也不该占用策划的表。
    /// <para><b>资产缺失时不是错误：</b><see cref="LoadOrDefault"/> 在没接线 / 资产不存在时
    /// 返回一份<b>字段默认值</b>的实例（默认值就是白模验收过的那一套）。于是"忘了拖资产"
    /// 的表现是"有一份能跑的默认手感"，而不是一堆 0 导致的静默异常。
    /// 这与 <c>ConfigModule</c> 的"带病数据不进运行时"是两种口径，因为这里的数据不参与判定，
    /// 只影响观感与手感。</para>
    /// <para><b>键与资产位置：</b><c>tuning/ThrowTuning</c> → <c>Assets/Resources/tuning/ThrowTuning.asset</c>。
    /// 走 <c>AssetModule</c> 的同步窄路（体量小、必须当场拿到）。</para>
    /// <para><b>颜色与排序层不在这里</b>：色值与 <c>sortingOrder</c> 是渲染约定，留在代码里
    /// （见 <c>RenderOrder</c> 与各视效件），避免"同一个颜色有两个来源"。</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "ThrowTuning", menuName = "DeepseaOil/Tuning/Throw")]
    public sealed class ThrowTuning : ScriptableObject
    {
        /// <summary>AssetModule 的 Key。</summary>
        public const string ResourceKey = "tuning/ThrowTuning";

        [Header("投掷")]
        [Tooltip("出手点相对地面的抬高量（世界单位）。只影响视觉，不参与落点与飞行时长")]
        public float originHeight = 0.3f;

        [Tooltip("屏幕点投到世界平面时给的相机深度（世界单位）。正交相机下给足量正值即可")]
        public float cameraPlaneDepth = 100f;

        [Header("球的视觉")]
        [Tooltip("球本体的视觉半径（世界单位）")]
        public float ballRadiusMeters = 0.22f;

        [Tooltip("阴影的视觉半径（世界单位）")]
        public float shadowRadiusMeters = 0.20f;

        [Tooltip("球到最高点时的阴影缩放（相对落地时）。球越高阴影越小，是高度唯一的视觉线索")]
        public float shadowScaleAtPeak = 0.7f;

        [Tooltip("阴影/指示器的贴地微偏移（世界单位）。取微小负值以压在地面装饰之上")]
        public float groundVisualOffset = -0.01f;

        [Header("瞄准")]
        [Tooltip("瞄准环的下沿半径（世界单位），也是 sprite 的基准尺寸")]
        public float aimRadiusMeters = 0.32f;

        [Tooltip("瞄准环竖直方向的压扁比例（1 = 不压扁）。这是风格选择，不是透视模拟")]
        public float aimVerticalSquash = 0.6f;

        [Tooltip("上半弧比下半弧再收窄的比例（0 = 上下对称的椭圆）")]
        public float aimPerspectiveTaper = 0.15f;

        [Tooltip("瞄准环的线宽（世界单位）")]
        public float aimRingThicknessMeters = 0.07f;

        [Header("落地")]
        [Tooltip("落地冲量生效半径（世界单位）")]
        public float impulseRadius = 1.2f;

        [Tooltip("落地冲量强度：相当于给质量为 1 的刚体增加的速度（单位/秒），不是加速度")]
        public float impulseStrength = 6f;

        [Tooltip("冲量强度换算成动量时的质量下限：防质量趋零时速度趋于无穷")]
        public float impulseMassFloor = 0.2f;

        [Tooltip("落地冲量查询碰撞体时多查的余量（世界单位）。纯优化余量，不参与判定")]
        public float pushQueryMargin = 2f;

        [Tooltip("落地瞬闪的持续时间（秒）")]
        public float landingRingDuration = 0.15f;

        /// <summary>
        /// 取调参资产；没有（未接线 / 资产不存在）时返回一份字段默认值的实例。
        /// </summary>
        /// <remarks>
        /// <b>重复调用返回同一份</b>：兜底分支会创建一个不进资源系统的 <c>ScriptableObject</c>，
        /// 而调用方不止一处（组合根与玩家组合根各要一份）—— 每次新建会攒垃圾，更糟的是
        /// "某一处改了字段、另一处看不见"。
        /// </remarks>
        public static ThrowTuning LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                ThrowTuning loaded = AssetModule.Load<ThrowTuning>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。" +
                "观感与手感调参将在本局失效。");

            Cached = CreateInstance<ThrowTuning>();

            return Cached;
        }

        /// <summary>进程内的那一份（见 <see cref="LoadOrDefault"/>）。</summary>
        private static ThrowTuning Cached;
    }
}
