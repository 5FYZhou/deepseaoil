using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Data
{
    /// <summary>战斗表现件颜色表；观感参数一律走 SO</summary>
    // 球种色只有一份，三个消费者共用；敌人两态色显式写出，不做插值

    [CreateAssetMenu(fileName = "VisualPalette", menuName = "DeepseaOil/Settings/VisualPalette")]
    public sealed class VisualPalette : ScriptableObject
    {
        public const string ResourceKey = "tuning/VisualPalette";

        [Header("球种")]
        [Tooltip("水球色")]
        public Color waterBall = new(0.20f, 0.55f, 1.00f, 1f);

        [Tooltip("土球色")]
        public Color earthBall = new(0.55f, 0.36f, 0.18f, 1f);

        [Tooltip("未知球种的兜底色（不会看不见，也不会误导成某一颗球）")]
        public Color unknownBall = Color.white;

        [Header("敌人身体 · 两态")]
        [Tooltip("正常（偏暖的红，与玩家黄、水球蓝、土球棕一眼分开）")]
        public Color enemyBodyNormal = new(0.86f, 0.30f, 0.28f, 1f);

        [Tooltip("踩在减速格里（比正常色明显更深，色相不变）")]
        public Color enemyBodySlowed = new(0.34f, 0.12f, 0.11f, 1f);

        [Header("瞄准高亮 · 两态")]
        [Tooltip("可投时（白，半透明：它是提示不是物体）")]
        public Color highlightAvailable = new(1f, 1f, 1f, 0.32f);

        [Tooltip("不可投时（红）")]
        public Color highlightBlocked = new(1f, 0.25f, 0.2f, 0.42f);

        [Header("贴地件")]
        [Tooltip("球阴影色：存在感来自它，不走球种色（阴影是光不是材质）")]
        public Color shadow = new(0f, 0f, 0f, 0.35f);

        /// <summary>取观感颜色表；缺失时返回字段默认值实例</summary>
        /// <remarks>消费者经 ConfigModule.Visuals 取，观感唯一权威入口</remarks>
        internal static VisualPalette LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                VisualPalette loaded = AssetModule.Load<VisualPalette>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。观感颜色将在本局退回白模那一套。");

            Cached = CreateInstance<VisualPalette>();

            return Cached;
        }

        private static VisualPalette Cached;

        public Color BallColor(BallType type)
        {
            switch (type)
            {
                case BallType.Water: return waterBall;
                case BallType.Earth: return earthBall;
                default: return unknownBall;
            }
        }

        /// <summary>掉落物颜色；与球同色（掉落物就是材料本身），种子件走 unknownBall 以免误导</summary>
        public Color DropColor(DropType type)
        {
            switch (type)
            {
                case DropType.Water: return waterBall;
                case DropType.Earth: return earthBall;
                default: return unknownBall;
            }
        }

        /// <remarks>slowMultiplier 本帧减速系数，小于 1 即被减速；受击闪白不在这里，由 EffectId.Flash 覆盖</remarks>
        public Color EnemyBodyColor(float slowMultiplier)
        {
            return slowMultiplier < 1f ? enemyBodySlowed : enemyBodyNormal;
        }
    }
}
