using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Combat
{
    // 玩家侧意图，非投掷事实，无结果字段；起点与落点一次算出，不得重算
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

    /// <summary>投掷裁决口，逻辑层定义端口、表现层组合根实现；拒绝时不耗弹药不进冷却</summary>
    public interface IThrowSink
    {
        /// <summary>采纳（球已飞出）为 true</summary>
        bool RequestThrow(in ThrowIntent intent);
    }
}
