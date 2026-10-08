using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>驱动工厂，按 DriverKind 而非 spec.Id 分派</summary>
    /// <remarks>未加 case 的种类静默落到 default 的 ParticleDriver，不报错</remarks>
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
