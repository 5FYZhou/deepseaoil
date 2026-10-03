using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>
    /// 驱动工厂：把一行 <see cref="EffectSpec"/> 变成 <see cref="IEffectDriver"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>这是 <c>EffectModule</c> 与具体驱动之间唯一的接缝</b>：<c>EffectModule</c> 只认识
    /// <see cref="IEffectDriver"/>，具体类型全部收在这里。</para>
    /// <para><b>加第二种驱动时</b>（材质闪白 / 相机震动 / 位移震动）：在这里按 <c>spec.Id</c> 或
    /// （更推荐）给 <c>EffectSpec</c> 加一个 <c>DriverKind</c> 字段来分派即可，
    /// <c>EffectModule</c> 一行都不用改。</para>
    /// </remarks>
    internal static class EffectDriverFactory
    {
        internal static IEffectDriver Create(in EffectSpec spec, Transform root)
            => new ParticleDriver(spec, root);
    }
}
