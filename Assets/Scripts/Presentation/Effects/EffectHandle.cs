using System;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 一次播放的句柄。可跨帧持有，用于 <c>EffectModule.Stop(handle)</c> 提前停掉这一次播放。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么不直接用 int</b>：句柄里带着<b>哪个 Driver 发的</b>（<see cref="Driver"/>），
    /// 所以 <c>EffectModule.Stop</c> 不需要回表查 <c>EffectId</c>，也不怕两个 Driver 的实例编号撞车。</para>
    /// <para><b>Generation 是 Driver 的 epoch</b>：<c>ParticleDriver.CleanAll()</c> 会把它 +1。
    /// 于是「CleanAll 之前拿到的句柄」即使实例编号被复用，也一定停在 <c>Stop</c> 的 epoch 校验上，
    /// 不会误停新实例。</para>
    /// <para><b>None</b>：<c>default(EffectHandle)</c>，<see cref="IsValid"/> 为 false。
    /// 所有失败路径都返回它，调用方无需判空。</para>
    /// <para>只读结构体：相等比较按值 + 驱动引用，可安全放进字典 / 比较。</para>
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

        /// <summary>是否为一次真实播放。失败路径返回的句柄一律为 false。</summary>
        public bool IsValid => Id != 0 && Driver != null;

        /// <summary>空句柄。</summary>
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
