using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 驱动工厂：把一行 <see cref="EffectSpec"/> 变成 <see cref="IEffectDriver"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>这是 <c>EffectModule</c> 与具体驱动之间唯一的接缝</b>：<c>EffectModule</c> 只认识
    /// <see cref="IEffectDriver"/>，具体类型全部收在这里。</para>
    /// <para><b>分派依据是 <see cref="EffectSpec.DriverKind"/> 而不是 <c>spec.Id</c></b>：
    /// 按 Id 分派会让"加一种特效"必须在工厂里再加一个 <c>case</c>，而按种类分派则允许
    /// 多种特效共用同一个驱动（只是参数不同）—— 后者才是常见形态。</para>
    /// <para><b>加一种驱动</b>：<c>EffectCatalog.cs</c> 的 <c>EffectDriverKind</c> 加一枚，
    /// 这里加一个 <c>case</c>，Catalog 里写上新种类。三步都在明处。</para>
    /// </remarks>
    internal static class EffectDriverFactory
    {
        internal static IEffectDriver Create(in EffectSpec spec, Transform root)
        {
            switch (spec.DriverKind)
            {
                case EffectDriverKind.EnemyShatter:
                    return new EnemyShatterDriver(spec, root);

                case EffectDriverKind.TileHighlight:
                    return new TileHighlightDriver(spec, root);

                default:
                    return new ParticleDriver(spec, root);
            }
        }
    }
}
