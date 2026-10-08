using System;

namespace DeepseaOil.Presentation.Effects
{
    /// 句柄带着"哪个 Driver 发的"，Stop 不必回表查 EffectId，两个 Driver 编号撞车也不会误停。
    /// Generation 是 Driver 的 epoch，CleanAll 会 +1，故之前拿到的句柄必停在 Stop 的 epoch 校验上，不会误停复用编号的新实例。
    public readonly struct EffectHandle : IEquatable<EffectHandle>
    {
        internal readonly int Id;
        internal readonly int Generation;
        internal readonly IEffectDriver Driver;

        internal EffectHandle(int id, int generation, IEffectDriver driver)
        {
            Id = id;
            Generation = generation;
            Driver = driver;
        }

        public bool IsValid => Id != 0 && Driver != null;

        /// <summary>空句柄（default），所有失败路径都返回它，调用方无需判空</summary>
        public static EffectHandle None => default;

        public bool Equals(EffectHandle other)
            => Id == other.Id
            && Generation == other.Generation
            && ReferenceEquals(Driver, other.Driver);

        public override bool Equals(object obj)
            => obj is EffectHandle h && Equals(h);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id;
                hash = (hash * 397) ^ Generation;
                return hash;
            }
        }

        public override string ToString()
            => IsValid ? $"Effect#{Id}g{Generation}" : "None";

        public static bool operator ==(EffectHandle a, EffectHandle b) => a.Equals(b);

        public static bool operator !=(EffectHandle a, EffectHandle b) => !a.Equals(b);
    }
}
