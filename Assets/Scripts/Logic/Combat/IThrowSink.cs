using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Combat
{
    // 玩家侧的意图（"我想把这一颗球扔到这一格"），不是一次投掷事实，因此没有结果字段。
    // 出手点与落点由同一次计算得出，接收方不要自己重算，否则"看着能扔到、其实扔不到"。
    public readonly struct ThrowIntent
    {
        public readonly BallType Ball;

        public readonly Vector3Int Cell;

        public readonly Vector2 Origin;

        public readonly Vector2 Target;

        public ThrowIntent(BallType ball, Vector3Int cell, Vector2 origin, Vector2 target)
        {
            Ball = ball;
            Cell = cell;
            Origin = origin;
            Target = target;
        }
    }

    /// <summary>投掷裁决口：玩家侧提交意图，世界侧回答"采纳不采纳"。</summary>
    // 方向是"玩家 → 世界 = 请求裁决"；逻辑层定义端口、表现层的组合根实现它。
    // 落点合法性是世界信息，所以只有裁决一说；被拒绝时不消耗弹药、不进冷却。
    public interface IThrowSink
    {
        /// <returns>采纳（球已经真的飞出去）为 <c>true</c>。</returns>
        bool RequestThrow(in ThrowIntent intent);
    }
}
