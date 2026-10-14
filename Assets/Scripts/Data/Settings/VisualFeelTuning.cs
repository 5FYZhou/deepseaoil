using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Data.Settings
{
    /// <summary>战斗观感调参：受击反馈、震屏、扬尘的数值集中地</summary>
    /// <remarks>只由主程调。EffectModule 不读本 SO：调用方读值填进 EffectContext，它只传递</remarks>
    [CreateAssetMenu(fileName = "VisualFeelTuning", menuName = "DeepseaOil/Tuning/VisualFeel")]
    public sealed class VisualFeelTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/VisualFeelTuning";

        [Header("受击")]
        [Tooltip("受击顿帧（秒）：定格动画，CoHitStop 用")]
        public float hitStopSeconds = 0.06f;

        [Tooltip("闪白时长（秒），填入 EffectContext.Duration")]
        public float flashDuration = 0.12f;

        [Tooltip("挤压形变兜底时长（秒）：EnemyTuning 条目里 normalDuration 填 0 时用")]
        public float squashDuration = 0.10f;

        [Tooltip("致死一击挤压形变兜底时长（秒）：EnemyTuning 条目里 deathDuration 填 0 时用")]
        public float squashDurationLethal = 0.14f;

        [Tooltip("挤压强度兜底（EffectContext.Value）：EnemyTuning 条目里 maxAmount 填 0 时用")]
        public float squashIntensity = 0.18f;

        [Header("死亡")]
        [Tooltip("碎裂增强层强度（ctx.Value）：与动画帧打架时调低")]
        public float shatterIntensity = 1.0f;

        [Tooltip("死亡回收兜底延迟（秒）：事件没到（未挂 AnimationEvent 或无 Animator）到点即收口；挂好事件时它只作保险，须大于死亡剪辑时长")]
        public float deathFallbackSeconds = 1.0f;

        [Header("震屏")]
        [Tooltip("玩家受创震屏幅度（EffectContext.Value）")]
        public float hitShakeAmplitude = 1f;

        [Tooltip("玩家受创震屏时长（秒）")]
        public float hitShakeDuration = 0.14f;

        [Tooltip("蒸汽爆轰震屏最小幅度（EffectContext.Value），对应半径区间下限")]
        public float blastShakeAmplitudeMin = 0.4f;

        [Tooltip("蒸汽爆轰震屏最大幅度（EffectContext.Value），对应半径区间上限")]
        public float blastShakeAmplitudeMax = 1f;

        [Tooltip("蒸汽爆轰震屏时长（秒）")]
        public float blastShakeDuration = 0.16f;

        [Header("扬尘")]
        [Tooltip("低于此速度（单位/秒）不扬尘：避免静止抖动喷粒子")]
        public float dustSpeedThreshold = 0.8f;

        [Tooltip("满速时的发射节拍（秒）")]
        public float dustInterval = 0.14f;

        [Tooltip("节拍按速度缩放的基准速度（单位/秒），一般取 PlayerConfig.moveSpeed")]
        public float dustReferenceSpeed = 8f;

        [Header("伤害飘字")]
        [Tooltip("上飘高度（世界单位）")] public float popupRiseHeight = 0.6f;

        [Tooltip("总时长（秒）")] public float popupDuration = 0.6f;

        [Tooltip("相对 ActorOrder 的排序偏移")] public int popupSortingOffset = 100;

        [Tooltip("位移缓出指数")] public float popupRiseEase = 2.0f;

        [Tooltip("alpha 淡出指数")] public float popupFadeEase = 1.5f;

        [Header("Z-Hop 伪高度")]
        [Tooltip("基准峰值高度（世界单位），Value=1 时的上飘高度")]
        public float hopPeakHeight = 0.25f;

        [Tooltip("滞空时长（秒）")]
        public float hopDuration = 0.28f;

        [Tooltip("v0 归一化基准速度（单位/秒）：Value = v0 / 本值")]
        public float hopReferenceSpeed = 8f;

        private static VisualFeelTuning Cached;

        /// <summary>取调参资产；缺失时返回字段默认值实例</summary>
        /// <remarks>调用方只有 ConfigModule.BindAssets；消费者经 ConfigModule.VisualFeel 取</remarks>
        internal static VisualFeelTuning LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                VisualFeelTuning loaded = AssetModule.Load<VisualFeelTuning>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。" +
                "战斗观感调参将在本局失效。");

            Cached = CreateInstance<VisualFeelTuning>();

            return Cached;
        }
    }
}
