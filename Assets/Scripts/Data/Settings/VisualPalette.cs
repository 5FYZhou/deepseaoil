using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 战斗表现件的<b>颜色表</b>。观感参数一律走 SO（审查已定），给程序调、不对策划暴露。
    /// </summary>
    /// <remarks>
    /// <b>为什么球种色必须只有一份：</b>同一个球种的颜色有三个消费者 —— 球本体、落地环、
    /// （将来的）HUD 图标。三处各写一份十六进制的话，"水球是蓝的"这件事就再也没有唯一答案了。
    /// <para><b>为什么敌人四态色也在这里：</b>收口前它们在逻辑层的 <c>EnemyVisual</c> 里 ——
    /// 那是"减速生效了没有"这个<b>判定</b>的邻居，但颜色本身是纯观感。
    /// 现在数值在 SO、<b>实现方在表现层</b>（"这一帧画什么"由 <c>EnemyBody</c> 决定）。</para>
    /// <para><b>四态显式写出来，不做乘法或插值</b>：<c>Color.Lerp</c> 或 <c>body * 0.4f</c>
    /// 会让"这一帧到底该是什么色"变成一个算不出来的数，既没法断言也没法单独调，
    /// 而"减速时颜色变深"本身就是一个设计决定，它值得有自己的字段。</para>
    /// <para><b>兜底口径与两份 Tuning 相同</b>：丢了资产时 <c>ConfigModule</c> 会给一份字段默认值的实例
    /// ＋ 一条 Warning（观感参数不参与判定，不能因为缺资产把游戏卡死）。</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "VisualPalette", menuName = "DeepseaOil/Settings/VisualPalette")]
    public sealed class VisualPalette : ScriptableObject
    {
        /// <summary>AssetModule 的 Key：<c>Assets/Resources/tuning/VisualPalette.asset</c>。</summary>
        public const string ResourceKey = "tuning/VisualPalette";

        [Header("球种")]
        [Tooltip("水球色")]
        public Color waterBall = new(0.20f, 0.55f, 1.00f, 1f);

        [Tooltip("土球色")]
        public Color earthBall = new(0.55f, 0.36f, 0.18f, 1f);

        [Tooltip("未知球种的兜底色（不会看不见，也不会误导成某一颗球）")]
        public Color unknownBall = Color.white;

        [Header("敌人身体 · 四态")]
        [Tooltip("正常（偏暖的红，与玩家黄、水球蓝、土球棕都能一眼分开）")]
        public Color enemyBodyNormal = new(0.86f, 0.30f, 0.28f, 1f);

        [Tooltip("踩在减速格里（比正常色明显更深，色相不变）")]
        public Color enemyBodySlowed = new(0.34f, 0.12f, 0.11f, 1f);

        [Tooltip("受击闪烁的亮色（比正常色亮，但仍是暖色，不像换了个敌人）")]
        public Color enemyFlash = new(1f, 0.92f, 0.90f, 1f);

        [Tooltip("踩在减速格里、且正在闪")]
        public Color enemyFlashSlowed = new(0.62f, 0.42f, 0.40f, 1f);

        [Header("瞄准高亮 · 两态")]
        [Tooltip("可投时（白，半透明：它是提示不是物体）")]
        public Color highlightAvailable = new(1f, 1f, 1f, 0.32f);

        [Tooltip("不可投时（红）")]
        public Color highlightBlocked = new(1f, 0.25f, 0.2f, 0.42f);

        [Header("贴地件")]
        [Tooltip("球阴影色：贴地件的“存在感”来自它，不走球种色（阴影是光，不是材质）")]
        public Color shadow = new(0f, 0f, 0f, 0.35f);

        /// <summary>
        /// 取观感颜色表；没有（未接线 / 资产不存在）时返回一份字段默认值的实例。
        /// </summary>
        /// <remarks>
        /// <b>调用方只有 <c>ConfigModule.BindAssets</c> 一处</b>：消费者经
        /// <c>ConfigModule.Visuals</c> 拿到它 —— 那是观感取值的<b>单一权威入口</b>
        /// （收口前调色板有三个入口：敌人的、球的、掉落物的取值边界各持一份，外加 <c>ConfigModule</c> 自己）。
        /// <para>兜底是安全网而不是常态：丢了资产时颜色退回"白模验收过的那一套"，
        /// 而不是一堆 alpha 为 0 的透明色（那会让全场看不见，且不报错）。</para>
        /// </remarks>
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

        /// <summary>进程内的那一份（见 <see cref="LoadOrDefault"/>）。</summary>
        private static VisualPalette Cached;

        /// <summary>取球种颜色；未知球种给兜底色。</summary>
        public Color BallColor(BallType type)
        {
            switch (type)
            {
                case BallType.Water: return waterBall;
                case BallType.Earth: return earthBall;
                default: return unknownBall;
            }
        }

        /// <summary>
        /// 这一帧的敌人身体颜色（四态之一：正常 / 减速 / 闪烁 / 减速+闪烁）。
        /// </summary>
        /// <param name="slowMultiplier">本帧实际生效的减速系数（<c>&lt; 1</c> 表示被减速）。</param>
        /// <param name="flashOn">闪烁相位的亮半周。</param>
        /// <remarks>
        /// <b>四态而不是"二选一"：</b>减速与受击是两个独立的 debuff，同时发生时两种反馈都要在。
        /// 写成 <c>if (flash) 亮 else if (slow) 深</c> 的话，站在减速格里被打中的敌人看起来
        /// "跟没踩进去一样" —— 而那正是玩家会去判断"这减速到底有没有用"的时刻。
        /// <para>判据用"是否小于 1"而不是"是否等于某个具体系数"：状态将来分等级（更强的减速）时，
        /// 这里不需要跟着改。</para>
        /// </remarks>
        public Color EnemyBodyColor(float slowMultiplier, bool flashOn)
        {
            bool slowed = slowMultiplier < 1f;

            if (slowed) return flashOn ? enemyFlashSlowed : enemyBodySlowed;

            return flashOn ? enemyFlash : enemyBodyNormal;
        }
    }
}
