using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 掉落物的<b>程序调参</b>：抛物线时长与弧高、飞向玩家的速度、判定距离、领取数量。
    /// </summary>
    /// <remarks>
    /// <b>为什么进 SO 而不是 Luban 表：</b>与 <c>ThrowTuning</c> 同一条分界 ——
    /// 这几个数是"边跑边看手感"时改的，改它们不该经过导表，也不该占用策划的表。
    /// 它们原本写在 <c>WaterBall</c> 组件的 Inspector 上，而掉落物是<b>运行期建出来的</b>：
    /// 场景里根本没有它的实例，那四个数除了点开组件看没有第二个地方能查。
    /// <para><b>资产随仓库提供：</b><c>Assets/Resources/tuning/DropTuning.asset</c>
    /// （键 <c>tuning/DropTuning</c>），走 <c>AssetModule</c> 的同步窄路（体量小、必须当场拿到）。
    /// <b>它必须在仓库里</b>：资产不存在时资源系统会记一条加载失败并计入 <c>DataMetrics</c> ——
    /// 那是"缺资源"这条事实应有的表现，不该靠兜底掩盖。</para>
    /// <para><b>兜底分支是安全网，不是常态：</b>真丢了资产时 <see cref="LoadOrDefault"/> 仍会返回一份
    /// <b>字段默认值</b>的实例（默认值就是白模验收过的那一套），于是玩法退回"能跑的默认手感"，
    /// 而不是"掉落物时长 0、原地不动"。代价是 Console 上会同时留下"加载失败"与"改用默认值"两条记录。</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "DropTuning", menuName = "DeepseaOil/Tuning/Drop")]
    public sealed class DropTuning : ScriptableObject
    {
        /// <summary>AssetModule 的 Key。</summary>
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

        /// <summary>
        /// 取调参资产；没有（未接线 / 资产不存在）时返回一份字段默认值的实例。
        /// </summary>
        /// <remarks>
        /// <b>调用方只有 <c>ConfigModule.BindAssets</c> 一处</b>：取值口径收口之后，
        /// 消费者不再直接读 SO，而是经 <c>ConfigModule.GetDrop</c> 拿到 <c>DropSpec</c>。
        /// <para><b>重复调用返回同一份</b>：兜底分支会创建一个不进资源系统的 <c>ScriptableObject</c>，
        /// 每次新建会攒垃圾，更糟的是"某一处改了字段、另一处看不见"。</para>
        /// </remarks>
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

        /// <summary>进程内的那一份（见 <see cref="LoadOrDefault"/>）。</summary>
        private static DropTuning Cached;
    }
}
