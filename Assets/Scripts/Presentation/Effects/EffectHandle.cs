using System;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>一次播放的句柄：可跨帧持有，传给 <c>EffectModule.Stop(handle)</c> 提前停掉这一次播放。</summary>
    /// <remarks>
    /// 句柄里带着"哪个 <see cref="Driver"/> 发的"：<c>Stop</c> 不必回表查 <c>EffectId</c>，两个 Driver 的实例编号撞车也不会误停。
    /// <see cref="Generation"/> 是 Driver 的 epoch：<c>ParticleDriver.CleanAll()</c> 会把它 +1，于是 CleanAll 之前拿到的句柄即使实例编号被复用，也一定停在 <c>Stop</c> 的 epoch 校验上、不会误停新实例。
    /// </remarks>
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

        /// <summary>空句柄（<c>default</c>，<see cref="IsValid"/> 为 false）；所有失败路径都返回它，调用方无需判空。</summary>
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
