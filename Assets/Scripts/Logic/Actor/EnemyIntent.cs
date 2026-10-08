using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人决策：方向＋速度</summary>
    /// <remarks>Direction 为目标−自己，不重归一，两处都归会出 √2 偏差</remarks>
    public readonly struct EnemyIntent
    {
        public readonly Vector2 Direction;

        /// <summary>目标速度，0=无目标或已到停止距离</summary>
        public readonly float Speed;

        public EnemyIntent(Vector2 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        public static EnemyIntent Idle => default;

        public bool IsIdle => Direction.sqrMagnitude <= 0f || Speed <= 0f;
    }
}
