using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 全局随机门面：默认用 <see cref="DefaultRng"/>，测试/回放可整体替换实现。
    /// </summary>
    /// <remarks>
    /// <b>为什么是静态门面而不是注入：</b>随机的消费者会越来越多（刷怪、掉落、抖动），
    /// 每个都从组合根注入一遍会把构造签名撑满；而"这一局的随机源"本来就只有一个。
    /// 需要确定性的测试用 <see cref="SetImpl"/> 换掉它，测完 <see cref="Reset"/> 还原。
    /// <para><b>它在地基</b>（收口前在 <c>Logic/Random/</c>）：逻辑层与表现层都要用它，
    /// 而"确定性"不属于任何一层。搬过来还有一个副作用 —— 命名空间不再叫 <c>Random</c>，
    /// 于是 <c>System.Random</c> 的重名遮蔽问题自动消失（下面仍然写全名，是为了读的人一眼看清用的是哪一个）。</para>
    /// </remarks>
    public static class Rng
    {
        private static IRng _impl = new DefaultRng();

        /// <summary>当前实现（只读；替换请用 <see cref="SetImpl"/>）。</summary>
        public static IRng Impl => _impl;

        /// <summary>替换实现；传 <c>null</c> 等价于 <see cref="Reset"/>。</summary>
        public static void SetImpl(IRng impl)
        {
            _impl = impl ?? new DefaultRng();
        }

        /// <summary>还原成默认实现（随机种子）。</summary>
        public static void Reset()
        {
            _impl = new DefaultRng();
        }

        /// <summary>还原成默认实现并指定种子（复现一局用）。</summary>
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

    /// <summary>
    /// 默认实现：<c>System.Random</c>。
    /// </summary>
    /// <remarks>
    /// 不用 <c>UnityEngine.Random</c>：那个是全局状态且与渲染/物理共享，
    /// 而本类需要"能被替换、能被固定种子复现"。
    /// <para>无参构造用一个固定种子（而不是时钟）：白模与自动化测试的价值在于
    /// "同一个现象能不能再出现一次"，而默认随机会让每次 Play 都不一样。</para>
    /// </remarks>
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
        /// <remarks>
        /// 用"半径开方"而不是拒绝采样：两者分布都对，但开方版本没有循环，
        /// 在"每次生成都要调"的路径上没有最坏情况。
        /// </remarks>
        public Vector2 InsideUnitCircle()
        {
            float radius = Mathf.Sqrt(Value01());
            float angle = Value01() * 2f * Mathf.PI;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <c>min == max</c> 时返回 <c>min</c>（空区间），不抛异常：调用方算出一个空区间是常见情况
        /// （比如"在 0 个候选里挑一个"），让它在边界上安静地退化比抛异常便宜。
        /// </remarks>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;

            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
