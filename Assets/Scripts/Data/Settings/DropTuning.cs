using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>资产 Assets/Resources/tuning/DropTuning.asset 必须在仓库；缺资源表现为加载失败并计入 DataMetrics，不靠兜底掩盖</summary>
    [CreateAssetMenu(fileName = "DropTuning", menuName = "DeepseaOil/Tuning/Drop")]
    public sealed class DropTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/DropTuning";

        [Header("抛物线")]
        [Tooltip("从生成点飞到落点的时长（秒）")]
        public float flightDuration = 1f;

        [Tooltip("抛物线弧高（世界单位）")]
        public float arcHeight = 2f;

        [Header("飞向玩家")]
        [Tooltip("落点 → 玩家的飞行速度（单位/秒）")]
        public float homingSpeed = 8f;

        [Tooltip("判定「够到玩家了」的距离（世界单位）")]
        public float reachDistance = 0.05f;

        [Header("领取")]
        [Tooltip("领取一次给几个")]
        public int amount = 1;

        [Header("本体")]
        [Tooltip("本体视觉直径（世界单位）")]
        public float bodyDiameter = 0.3f;

        [Tooltip("触发半径（世界单位）")]
        public float triggerRadius = 0.15f;

        /// <summary>取调参资产；取不到时返回字段默认值实例；调用方只有 ConfigModule.BindAssets，消费者经 ConfigModule.GetDrop 取 DropSpec</summary>
        internal static DropTuning LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                DropTuning loaded = AssetModule.Load<DropTuning>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。" +
                "掉落物的抛物线手感将在本局失效。");

            Cached = CreateInstance<DropTuning>();

            return Cached;
        }

        private static DropTuning Cached;
    }
}
