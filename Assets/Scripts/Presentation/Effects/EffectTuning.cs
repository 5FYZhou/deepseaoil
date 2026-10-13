using System;
using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>特效池容量调参：每个 EffectId 一行，配池上限与预热数</summary>
    /// <remarks>只由主程调。EffectModule 在 Init 时读一次，不进 EffectContext；缺资产只 Warning，退回代码默认值</remarks>
    [CreateAssetMenu(fileName = "EffectTuning", menuName = "DeepseaOil/Tuning/Effect")]
    public sealed class EffectTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/EffectTuning";

        [Serializable]
        public struct PoolConfig
        {
            [Tooltip("Presentation.Effects.EffectId 的数值")]
            public int Id;

            [Tooltip("池上限，超了丢弃并警告；必须 >0，填 0 的整行会被忽略")]
            public int MaxSize;

            [Tooltip("预热数")]
            public int Prewarm;
        }

        [Header("池容量")]
        [Tooltip("每个 EffectId 一行；未列出的走 EffectCatalog 的代码默认值")]
        public PoolConfig[] pools = new PoolConfig[0];

        [Header("同帧队列")]
        [Tooltip("单帧单特效最大排队 Play 数，超了丢弃最旧的")]
        public int maxQueuedPlaysPerEffect = 64;

        private static EffectTuning Cached;

        /// <summary>取调参资产；缺失时返回字段默认值实例</summary>
        /// <remarks>调用方只有 EffectModule.Init；重复调用返回同一份，兜底实例不进资源系统</remarks>
        internal static EffectTuning LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                EffectTuning loaded = AssetModule.Load<EffectTuning>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。" +
                "特效池容量将在本局退回 EffectCatalog 的代码默认值。");

            Cached = CreateInstance<EffectTuning>();

            return Cached;
        }
    }
}
