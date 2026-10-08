using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>全局随机门面，默认 DefaultRng，可整体替换</summary>
    public static class Rng
    {
        private static IRng _impl = new DefaultRng();

        public static IRng Impl => _impl;

        /// <summary>替换实现，null 等价 Reset</summary>
        public static void SetImpl(IRng impl)
        {
            _impl = impl ?? new DefaultRng();
        }

        public static void Reset()
        {
            _impl = new DefaultRng();
        }

        public static void Reset(int seed)
        {
            _impl = new DefaultRng(seed);
        }

        public static float Value01() => _impl.Value01();

        public static Vector2 InsideUnitCircle() => _impl.InsideUnitCircle();

        public static int Range(int minInclusive, int maxExclusive) => _impl.Range(minInclusive, maxExclusive);
    }

    /// <summary>默认实现，用 System.Random；UnityEngine.Random 是全局状态，无法替换与复现</summary>
    public sealed class DefaultRng : IRng
    {
        /// <summary>固定默认种子，每局一致便于复现</summary>
        public const int DefaultSeed = 20261004;

        private readonly System.Random _random;

        public DefaultRng() : this(DefaultSeed)
        {
        }

        public DefaultRng(int seed)
        {
            _random = new System.Random(seed);
        }

        public float Value01()
        {
            return (float)_random.NextDouble();
        }

        public Vector2 InsideUnitCircle()
        {
            float radius = Mathf.Sqrt(Value01());
            float angle = Value01() * 2f * Mathf.PI;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>min≥max 的空区间返回 min，不抛异常</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;

            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
