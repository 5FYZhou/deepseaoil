using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>驱动工厂：把一行 <see cref="EffectSpec"/> 变成 <see cref="IEffectDriver"/>。分派依据是 <see cref="EffectSpec.DriverKind"/> 而不是 <c>spec.Id</c>。</summary>
    /// <remarks>未加 <c>case</c> 的种类静默落到 <c>default</c> → <c>ParticleDriver</c>，不报错。</remarks>
    internal static class EffectDriverFactory
    {
        internal static IEffectDriver Create(in EffectSpec spec, Transform root)
        {
            switch (spec.DriverKind)
            {
                case EffectDriverKind.Shatter:
                    return new ShatterDriver(spec, root);

                case EffectDriverKind.Highlight:
                    return new HighlightDriver(spec, root);

                default:
                    return new ParticleDriver(spec, root);
            }
        }
    }
}
