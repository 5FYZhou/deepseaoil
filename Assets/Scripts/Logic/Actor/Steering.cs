using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 一次转向决策的产物：<b>往哪走</b>与<b>走多快</b>。纯数据。
    /// </summary>
    /// <remarks>
    /// 拆成"方向"与"速度"两件事而不是一个速度向量，是因为两者来源不同：
    /// 方向来自几何（目标减自己），速度来自数值（基准速 × 门禁乘数）。
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
        /// <returns>方向与目标速度；不该动时两者都表示"不动"。</returns>
        /// <remarks>
        /// <b>三个"不动"的条件，每一个都对应一类真实缺陷：</b>
        /// <list type="number">
        /// <item><paramref name="stopDistance"/> 内不动 —— 否则敌人会不停往玩家刚体上挤，表现为"贴着玩家抖动"。</item>
        /// <item><paramref name="chaseRange"/> 外不动 —— 否则"跑得够远能脱离"这件事永远不可能成立。</item>
        /// <item>方向向量为零（正好站在目标点上）不动 —— 零向量归一化是 <c>NaN</c>，
        /// 角色会带着非数坐标消失（球的那条上限栽过同一个跟头）。</item>
        /// </list>
        /// <para><b>减速不在这里了</b>（收口前它带一个 <c>slowFromSource</c> 参数）：泥浆减速改成
        /// 由格状态提交、由目标的状态效果层持有、经移动层门禁落地 —— 于是本函数的目标速度
        /// 永远等于配置速度，"慢下来"由 <c>ActorLogic.SetSpeedScale</c> 乘在目标速度上。
        /// "减益只做乘法、不做加法"这条不变量还在，只是落在了它该在的那一层。</para>
        /// <para><b>"够近了"不能返回 <c>default</c>：</b>那样连方向都没了。
        /// 两者的区别在将来会被分开处理（"够近了但仍然要面向玩家"），提前抹掉就再也分不开。</para>
        /// </remarks>
        public static Steering Resolve(
            Vector2 self,
            Vector2 target,
            float stopDistance,
            float chaseRange,
            float maxSpeed)
        {
            Vector2 delta = target - self;

            // 站在目标点上：方向不可定义。返回"不动"而不是硬塞一个默认方向 ——
            // 硬塞会让敌人在玩家身上原地画圈。这一步必须**先于**距离判断。
            if (delta.sqrMagnitude <= 0f) return default;

            float distance = delta.magnitude;

            // 超出追击范围：不给方向 ⇒ 走指数衰减，是"滑停"而不是"定住"。
            if (distance > chaseRange) return default;

            float speed = distance <= stopDistance ? 0f : maxSpeed;

            return new Steering(delta, speed);
        }
    }
}
