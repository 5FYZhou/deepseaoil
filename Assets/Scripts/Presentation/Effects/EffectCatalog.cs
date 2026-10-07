using System.Collections.Generic;

namespace DeepseaOil.Presentation.Effects
{
    internal enum EffectDriverKind
    {
        Particle = 0,

        Shatter = 1,

        Highlight = 2,
    }

    internal readonly struct EffectSpec
    {
        public readonly EffectId Id;

        /// <summary>资源 Key，默认 <c>"effects/" + 枚举名</c>（预制体在 <c>Assets/Resources/effects/</c> 下）；程序生成型返回空串 ⇒ 不加载也不报错。</summary>
        public readonly string Key;

        public readonly EffectDriverKind DriverKind;

        /// <summary>是否单例型（同时只有一个实例，重复 Play 合并）。</summary>
        public readonly bool IsSingleton;

        /// <summary>池上限（同时存在的实例数上限，超了丢弃并警告）；构造时 ≤ 0 按 1 处理。程序生成型一般按「同屏可能同时存在几个」给。</summary>
        public readonly int MaxSize;

        /// <summary>池预热数（资源就位时立刻实例化的个数，会截断到 maxSize）；构造时 &lt; 0 按 0，0 = 不预热。</summary>
        public readonly int Prewarm;

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

    /// <summary>特效装配表：新增一个特效在这里加一行（外加枚举一项 + 一个预制体）。</summary>
    /// <remarks>未列入本表的 <see cref="EffectId"/>（<c>Flash</c> / <c>Shake</c> / <c>ScreenShake</c>）：<c>Play</c> 打一条 LogError 并返回 None（「驱动还没实现」的显式形态）；<c>EffectId.None</c> 是「这次不播」的合法值（不查表、不报错），与「未注册」不是一回事。</remarks>
    internal static class EffectCatalog
    {
        internal const string KeyPrefix = "effects/";

        private static readonly EffectSpec[] Specs =
        {
            // Id                       驱动种类                          单例   池上限  预热
            new EffectSpec(EffectId.BurstSparks,  EffectDriverKind.Particle, isSingleton: false, maxSize: 32, prewarm: 8),
            new EffectSpec(EffectId.MudSplash, EffectDriverKind.Particle, isSingleton: false, maxSize: 16, prewarm: 4),

            new EffectSpec(EffectId.Shatter,  EffectDriverKind.Shatter,  maxSize: 16, prewarm: 0),

            // 瞄准高亮（持续型）：创建一次、之后只更新，同时只有一个实例，所以池上限 1、不预热。
            new EffectSpec(EffectId.Highlight, EffectDriverKind.Highlight, isSingleton: true, maxSize: 1, prewarm: 0),
        };

        internal static IReadOnlyList<EffectSpec> All => Specs;

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
