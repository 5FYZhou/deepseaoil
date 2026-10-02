using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 一次转向决策的产物：<b>往哪走</b>与<b>走多快</b>。纯数据。
    /// </summary>
    /// <remarks>
    /// 拆成"方向"与"速度"两件事而不是一个速度向量，是因为两者有两个不同的来源：
    /// 方向来自几何（目标减自己），速度来自数值（基准速 × 减益系数）。
    /// 合成一个向量会让"泥浆把速度压到 45%"与"方向要不要归一化"混在一起，
    /// 而后者正是"斜向快 √2 倍"那类缺陷的温床。
    /// </remarks>
    public readonly struct Steering
    {
        /// <summary>
        /// 期望方向。<b>可能不是单位向量</b>（就是"目标 − 自己"），零向量表示"没有期望方向"。
        /// </summary>
        /// <remarks>
        /// 归一化刻意不在这里做，交给 <c>ActorLogic.SteerTowards</c>：那里的契约是
        /// "方向可以是未归一化向量"，两处都归就会出 √2 倍的静默偏差。
        /// </remarks>
        public readonly Vector2 Direction;

        /// <summary>该方向上的目标速度（单位/秒）。</summary>
        public readonly float Speed;

        public Steering(Vector2 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        /// <summary>没有期望方向（不追、距离太近、或没有目标）。</summary>
        public bool IsIdle => Direction.sqrMagnitude <= 0f || Speed <= 0f;

        /// <summary>
        /// 按"追击 + 泥浆减速"算一次转向。<b>静态纯函数</b>：只吃参数，不读时间、不读单例、不持状态。
        /// </summary>
        /// <param name="self">自己的位置。</param>
        /// <param name="target">目标位置（玩家）。</param>
        /// <param name="stopDistance">进入这个距离就不动（世界单位）。</param>
        /// <param name="chaseRange">超出这个距离就放弃（世界单位）。</param>
        /// <param name="maxSpeed">追击基准速度（单位/秒）。</param>
        /// <param name="mudSlowFromSource">泥浆减速系数（已由 <c>EnemyConfig.SlowMultiplier</c> 钳过）。</param>
        /// <returns>方向与目标速度；不该动时两者都表示"不动"。</returns>
        /// <remarks>
        /// <b>三个"不动"的条件，每一个都对应一类真实缺陷：</b>
        /// <list type="number">
        /// <item><paramref name="stopDistance"/> 内不动 —— 否则敌人会不停往玩家刚体上挤，
        /// 表现为"贴着玩家抖动"。</item>
        /// <item><paramref name="chaseRange"/> 外不动 —— 否则"跑得够远能脱离"这件事永远不可能成立。</item>
        /// <item>方向向量为零（正好站在目标点上）不动 —— 零向量归一化是 <c>NaN</c>，
        /// 角色会带着非数坐标消失（球的那条上限栽过同一个跟头）。</item>
        /// </list>
        /// <para><b>速度恒为正、且恒不超过 <paramref name="maxSpeed"/>：</b>减益只做乘法，不做加法。
        /// 这是"泥浆只减速不放大"这条不变量在代码里的落点。</para>
        /// </remarks>
        public static Steering Resolve(
            Vector2 self,
            Vector2 target,
            float stopDistance,
            float chaseRange,
            float maxSpeed,
            float mudSlowFromSource)
        {
            Vector2 delta = target - self;

            // 站在目标点上：方向不可定义。返回"不动"而不是硬塞一个默认方向 ——
            // 硬塞会让敌人在玩家身上原地画圈。这一步必须**先于**距离判断。
            if (delta.sqrMagnitude <= 0f) return default;

            float distance = delta.magnitude;

            // 超出追击范围：不给方向 ⇒ SteerTowards 走指数衰减，是"滑停"而不是"定住"。
            if (distance > chaseRange) return default;

            // 已经够近：还是把方向给出去，但目标速度为零。
            // **不能在这里返回 default**：那样 SteerTowards 收到的方向也是零，虽然同样会衰减，
            // 但"为什么不动"这件事就从"速度目标为零"变成了"没有方向"—— 两者将来会被分开处理
            // （比如"够近了但仍然要面向玩家"），把方向丢掉就等于提前把这个区分抹掉。
            float speed = distance <= stopDistance ? 0f : maxSpeed * mudSlowFromSource;

            return new Steering(delta, speed);
        }
    }
}
