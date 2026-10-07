using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人的一帧决策结果（"意图"）：往哪走 ＋ 走多快。纯数据，是敌人侧的 <c>InputSnapshot</c>。</summary>
    /// <remarks><c>Direction</c> 是"目标 − 自己"，不必归一化：归一化由 <c>SteerTowards</c> 统一做，两处都归会出 √2 倍的静默偏差；零向量 = 没有期望方向。</remarks>
    public readonly struct EnemyIntent
    {
        public readonly Vector2 Direction;

        /// <summary>该方向上的目标速度（单位/秒）；<c>0</c> = 不动（没有目标，或已经在停止距离内）。</summary>
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
