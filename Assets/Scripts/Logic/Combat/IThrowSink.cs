using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>
    /// 一次投掷请求：<b>玩家侧表达的意图</b>（"我想把这一颗球扔到这一格"）。
    /// </summary>
    /// <remarks>
    /// 它<b>不是</b>一次投掷事实：是否被采纳由世界侧裁决（落点有没有地板、将来是否被阻挡）。
    /// 因此这里只有"我想干什么"，没有任何"结果"字段。
    /// <para><b>为什么带上出手点与落点：</b>两者由同一次计算得出（吸附函数输出的格子 ＋ 该格中心），
    /// 让接收方自己再算一遍等于给"看着能扔到、其实扔不到"留一扇门。</para>
    /// </remarks>
    public readonly struct ThrowIntent
    {
        /// <summary>球种。</summary>
        public readonly BallType Ball;

        /// <summary>目标格（吸附后的那一格）。</summary>
        public readonly Vector3Int Cell;

        /// <summary>出手点（玩家位置）。</summary>
        public readonly Vector2 Origin;

        /// <summary>落点（目标格的几何中心）。</summary>
        public readonly Vector2 Target;

        public ThrowIntent(BallType ball, Vector3Int cell, Vector2 origin, Vector2 target)
        {
            Ball = ball;
            Cell = cell;
            Origin = origin;
            Target = target;
        }
    }

    /// <summary>
    /// 投掷裁决口：玩家侧提交意图，世界侧回答"采纳不采纳"。
    /// </summary>
    /// <remarks>
    /// <b>方向是"玩家 → 世界 = 请求裁决"</b>（与"世界 → 玩家 = 通知"对称）：
    /// 逻辑层定义端口、表现层的组合根实现它，于是玩家逻辑不需要认识格子、球、场景里的任何东西。
    /// <para><b>为什么是"裁决"而不是"直接投"：</b>落点合法性是世界信息（有没有地板、将来有没有阻挡）。
    /// 玩家只表达意图；被拒绝时<b>不消耗弹药、不进冷却</b> —— 那正是"没被采纳"的含义。</para>
    /// </remarks>
    public interface IThrowSink
    {
        /// <summary>
        /// 裁决一次投掷请求。
        /// </summary>
        /// <param name="intent">意图（球种 ＋ 目标格 ＋ 出手点与落点）。</param>
        /// <returns>采纳（球已经真的飞出去）为 <c>true</c>。</returns>
        bool RequestThrow(in ThrowIntent intent);
    }
}
