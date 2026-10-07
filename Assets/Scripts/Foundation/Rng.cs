using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>全局随机门面：默认用 <see cref="DefaultRng"/>，测试/回放可整体替换实现。</summary>
    public static class Rng
    {
        private static IRng _impl = new DefaultRng();

        public static IRng Impl => _impl;

        /// <summary>替换实现；传 <c>null</c> 等价于 <see cref="Reset"/>。</summary>
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

        /// <inheritdoc cref="IRng.Value01"/>
        public static float Value01() => _impl.Value01();

        /// <inheritdoc cref="IRng.InsideUnitCircle"/>
        public static Vector2 InsideUnitCircle() => _impl.InsideUnitCircle();

        /// <inheritdoc cref="IRng.Range(int,int)"/>
        public static int Range(int minInclusive, int maxExclusive) => _impl.Range(minInclusive, maxExclusive);
    }

    /// <summary>默认实现：<c>System.Random</c>（<b>不用</b> <c>UnityEngine.Random</c>：那个是全局状态、与渲染/物理共享，无法替换、无法固定种子复现）。</summary>
    public sealed class DefaultRng : IRng
    {
        /// <summary>默认种子。固定值 = 每局一致，便于复现。</summary>
        public const int DefaultSeed = 20261004;

        private readonly System.Random _random;

        public DefaultRng() : this(DefaultSeed)
        {
        }

        public DefaultRng(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <inheritdoc />
        public float Value01()
        {
            return (float)_random.NextDouble();
        }

        /// <inheritdoc />
        public Vector2 InsideUnitCircle()
        {
            float radius = Mathf.Sqrt(Value01());
            float angle = Value01() * 2f * Mathf.PI;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <inheritdoc />
        /// <remarks><c>min == max</c> 时返回 <c>min</c>（空区间），不抛异常：调用方算出一个空区间是常见情况（比如"在 0 个候选里挑一个"），让它在边界上安静地退化比抛异常便宜。</remarks>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;

            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
