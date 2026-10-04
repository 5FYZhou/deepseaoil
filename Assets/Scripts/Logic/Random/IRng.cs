using UnityEngine;

namespace DeepseaOil.Logic.Random
{
    /// <summary>
    /// 随机数端口：逻辑层唯一被允许的随机来源。
    /// </summary>
    /// <remarks>
    /// <b>为什么不直接用 <c>UnityEngine.Random</c>：</b>它是全局静态状态，测试之间会互相污染，
    /// 而且"这一局的随机序列"无法被替换或复现。走端口之后，测试可以塞一个确定序列的假实现，
    /// 于是"敌人在随机位置生成"这类行为也能被断言。
    /// <para>实现必须是<b>纯函数式</b>的（只依赖内部状态），不许读时间、不许读场景。</para>
    /// </remarks>
    public interface IRng
    {
        /// <summary>[0, 1) 的均匀分布。</summary>
        float Value01();

        /// <summary>单位圆内的均匀分布点（模长 &lt; 1）。</summary>
        Vector2 InsideUnitCircle();

        /// <summary>[minInclusive, maxExclusive) 的均匀整数。</summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
