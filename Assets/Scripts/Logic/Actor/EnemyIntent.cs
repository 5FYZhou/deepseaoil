using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的一帧决策结果（"意图"）：<b>往哪走 ＋ 走多快</b>。纯数据。
    /// </summary>
    /// <remarks>
    /// <b>它是敌人的"输入源"</b>：玩家那一侧对应的是 <c>InputSnapshot</c>（玩家按了什么），
    /// 敌人这一侧是大脑算出来的意图。两者的下游完全一样 —— 同一套账本、同一套状态组、
    /// 同一套控制律（<c>ActorLogic.MoveTowards</c>）。
    /// <para><b>方向不必归一化</b>（就是"目标 − 自己"）：归一化由 <c>SteerTowards</c> 统一做，
    /// 两处都归会出 √2 倍的静默偏差 —— 这是 <c>Steering</c> 写明的契约。</para>
    /// <para><b>目前只有方向与速度两个字段。</b>"是否攻击 / 是否禁足"等字段等敌人出现真实攻击行为
    /// 与禁足来源时再加：结构跟着行为走，而不是先摆一排没人填的字段
    /// （同一条判据见"冷却期没有行为就不做成状态"）。</para>
    /// </remarks>
    public readonly struct EnemyIntent
    {
        /// <summary>期望方向（未归一化）；零向量表示"没有期望方向"。</summary>
        public readonly Vector2 Direction;

        /// <summary>该方向上的目标速度（单位/秒）；<c>0</c> 表示不动。</summary>
        public readonly float Speed;

        public EnemyIntent(Vector2 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        /// <summary>不动：没有目标、或已经在停止距离内。</summary>
        public static EnemyIntent Idle => default;

        /// <summary>本帧是否"不该动"。</summary>
        public bool IsIdle => Direction.sqrMagnitude <= 0f || Speed <= 0f;
    }
}
