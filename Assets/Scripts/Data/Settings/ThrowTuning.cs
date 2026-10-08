using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>投掷与瞄准的程序调参</summary>
    /// <remarks>程序调参走 SO，不经导表；资产丢失时返回字段默认值实例而非 0；渲染约定见 RenderOrder</remarks>
    [CreateAssetMenu(fileName = "ThrowTuning", menuName = "DeepseaOil/Tuning/Throw")]
    public sealed class ThrowTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/ThrowTuning";

        [Header("投掷")]
        [Tooltip("出手点相对地面的抬高量（世界单位）。只影响视觉，不参与落点与飞行时长")]
        public float originHeight = 0.3f;

        [Tooltip("屏幕点投到世界平面时给的相机深度（世界单位）。正交相机下给足量正值即可")]
        public float cameraPlaneDepth = 100f;

        [Tooltip("从出手到落点的基准飞行时长（秒）")]
        public float flightDuration = 0.6f;

        [Tooltip("抛物线视觉最高点（世界单位）")]
        public float maxHeight = 2f;

        [Tooltip("最远投掷距离（世界单位）")]
        public float maxThrowDistance = 5f;

        [Tooltip("最小投掷距离（世界单位）")]
        public float minThrowDistance = 0.4f;

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

        private static ThrowTuning Cached;

        /// <summary>取调参资产；缺失时返回字段默认值实例</summary>
        /// <remarks>调用方只有 ConfigModule.BindAssets；消费者经 ConfigModule.GetXxx 拿合并件；重复调用返回同一份，兜底实例不进资源系统</remarks>
        internal static ThrowTuning LoadOrDefault()
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
    }
}
