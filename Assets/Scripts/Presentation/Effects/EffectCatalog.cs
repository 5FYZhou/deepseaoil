using System.Collections.Generic;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 驱动种类：<b>决定 <c>EffectDriverFactory</c> 造哪种驱动</b>。
    /// </summary>
    /// <remarks>
    /// 加第二种驱动时，<c>EffectModule</c> 一行都不用改 —— 它只认识 <see cref="IEffectDriver"/>。
    /// 这里加一枚枚举、在 <see cref="EffectCatalog"/> 的行里写上它、在工厂里加一个 <c>case</c>，
    /// 装配链就通了。
    /// </remarks>
    internal enum EffectDriverKind
    {
        /// <summary>粒子驱动：需要 <c>Assets/Resources/effects/&lt;枚举名&gt;.prefab</c>。</summary>
        Particle = 0,

        /// <summary>落地环：程序生成贴地圆环，不需要资源。</summary>
        LandingRing = 1,

        /// <summary>敌人碎裂：程序生成扇形碎片，不需要资源。</summary>
        EnemyShatter = 2,

        /// <summary>瞄准格高亮：程序生成整格色块，<b>持续型</b>（创建一次、之后只更新），不需要资源。</summary>
        TileHighlight = 3,
    }

    /// <summary>
    /// 一行特效的装配参数。<b>纯数据</b>，不含任何逻辑。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="EffectId"/> 一一对应：枚举说「有哪几种特效」，本结构说「这一种怎么装配」。
    /// </remarks>
    internal readonly struct EffectSpec
    {
        /// <summary>对应的特效标识。</summary>
        public readonly EffectId Id;

        /// <summary>资源 Key，交给 <c>AssetModule</c>。默认 <c>"effects/" + 枚举名</c>。</summary>
        /// <remarks>
        /// <b>程序生成的驱动会忽略它</b>（自己的 <c>AssetKey</c> 返回空串），
        /// 于是 <c>EffectModule</c> 既不预加载也不懒加载 —— 也就不会因为"没有同名预制体"而报缺失。
        /// </remarks>
        public readonly string Key;

        /// <summary>驱动种类。</summary>
        public readonly EffectDriverKind DriverKind;

        /// <summary>是否单例型（同时只有一个实例，重复 Play 合并）。</summary>
        public readonly bool IsSingleton;

        /// <summary>池上限（同时存在的实例数上限，超了丢弃并警告）。</summary>
        public readonly int MaxSize;

        /// <summary>池预热数（资源就位时立刻实例化的个数）。</summary>
        public readonly int Prewarm;

        /// <param name="id">特效标识。</param>
        /// <param name="driverKind">驱动种类；默认粒子。</param>
        /// <param name="isSingleton">是否单例型。默认 false（每次都新建）。</param>
        /// <param name="maxSize">池上限。必须 &gt; 0。</param>
        /// <param name="prewarm">预热数。会截断到 maxSize。0 = 不预热，首次 Play 现场实例化。</param>
        /// <param name="key">资源 Key。留空 = 取约定 <c>"effects/" + id</c>。</param>
        public EffectSpec(
            EffectId id,
            EffectDriverKind driverKind = EffectDriverKind.Particle,
            bool isSingleton = false,
            int maxSize = 16,
            int prewarm = 0,
            string key = null)
        {
            Id = id;
            Key = string.IsNullOrEmpty(key) ? EffectCatalog.KeyPrefix + id : key;
            DriverKind = driverKind;
            IsSingleton = isSingleton;
            MaxSize = maxSize > 0 ? maxSize : 1;
            Prewarm = prewarm < 0 ? 0 : prewarm;
        }

        public override string ToString()
            => $"{Id}(kind={DriverKind} key={Key} singleton={IsSingleton} max={MaxSize} prewarm={Prewarm})";
    }

    /// <summary>
    /// 特效装配表：<b>新增一个特效只需要在这里加一行</b>（外加枚举一项 + 一个预制体）。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么不做成 ScriptableObject</b>：枚举已经承担了「编译期检查」的职责，
    /// 再引一层资产配置只会多一个「忘了填 / 填错引用」的失败面。等特效数量到几十个、
    /// 需要策划自己调参时再谈 SO（换起来只动本文件）。</para>
    /// <para><b>Key 约定</b>：<c>"effects/" + 枚举名</c> ⇒ 预制体放在
    /// <c>Assets/Resources/effects/&lt;枚举名&gt;.prefab</c>。
    /// 与 <c>UIMgr</c> 的 <c>"ui/Panel/" + 类名</c> 同一套写法；路径语义转换只在
    /// <c>AssetRegistry.ResolvePath</c>，将来切 Addressables 也只改那一处。</para>
    /// <para><b>未列入本表的 EffectId</b>（当前是 <c>EnemyFlashWhite</c> / <c>ObjectShake</c> /
    /// <c>ScreenShake</c>）：<c>Play</c> 会打一条 LogError 并返回 None。这不是缺陷，
    /// 是「驱动还没实现」的显式形态——它们的驱动由用户按需补，补完在这里加行即可。</para>
    /// </remarks>
    internal static class EffectCatalog
    {
        /// <summary>资源 Key 前缀。约定：预制体在 <c>Assets/Resources/effects/</c> 下，文件名 = 枚举名。</summary>
        internal const string KeyPrefix = "effects/";

        private static readonly EffectSpec[] Specs =
        {
            // Id                       驱动种类                          单例   池上限  预热
            new EffectSpec(EffectId.HitSpark,  EffectDriverKind.Particle, isSingleton: false, maxSize: 32, prewarm: 8),
            new EffectSpec(EffectId.MudSplash, EffectDriverKind.Particle, isSingleton: false, maxSize: 16, prewarm: 4),

            // 程序生成的两个（白模迁移）：不需要预制体，池上限按"同屏可能同时存在几个"给。
            new EffectSpec(EffectId.LandingRing,   EffectDriverKind.LandingRing,   maxSize: 16, prewarm: 0),
            new EffectSpec(EffectId.EnemyShatter,  EffectDriverKind.EnemyShatter,  maxSize: 16, prewarm: 0),

            // 瞄准高亮（持续型）：同时只会有一个实例（创建一次、之后走 Update），
            // 所以池上限 1、不预热 —— 预热一个开局用不到的对象没有收益。
            new EffectSpec(EffectId.TileHighlight, EffectDriverKind.TileHighlight, isSingleton: true, maxSize: 1, prewarm: 0),

            // 待实现驱动的三种（加行即接入，EffectModule 不用改）：
            // new EffectSpec(EffectId.EnemyFlashWhite, maxSize: 16, prewarm: 4),
            // new EffectSpec(EffectId.ObjectShake, isSingleton: true, maxSize: 1, prewarm: 1),
            // new EffectSpec(EffectId.ScreenShake, isSingleton: true, maxSize: 1, prewarm: 1),
        };

        internal static IReadOnlyList<EffectSpec> All => Specs;

        /// <summary>查表。行数极少（个位数），线性扫描比字典更省。</summary>
        internal static bool TryGet(EffectId id, out EffectSpec spec)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                if (Specs[i].Id == id)
                {
                    spec = Specs[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}
