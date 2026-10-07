using UnityEngine;

namespace DeepseaOil.Logic
{
    public readonly struct Steering
    {
        /// <summary>期望方向：<b>可能不是单位向量</b>（就是"目标 − 自己"），零向量表示"没有期望方向"；归一化不在这里做，交给 <c>ActorLogic.SteerTowards</c>（两处都归就会出 √2 倍的静默偏差）。</summary>
        public readonly Vector2 Direction;

        /// <summary>该方向上的目标速度（单位/秒）。</summary>
        public readonly float Speed;

        public Steering(Vector2 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        public bool IsIdle => Direction.sqrMagnitude <= 0f || Speed <= 0f;

        /// <remarks>按"追击 + 泥浆减速"算一次转向。<b>静态纯函数</b>：只吃参数，不读时间、不读单例、不持状态；两个距离参数都是世界单位。减速不在这里做：减益只做乘法、不做加法，由 <c>ActorLogic.SetSpeedScale</c> 乘在目标速度上。
        /// 三个"不动"各对应一类真实缺陷：<paramref name="stopDistance"/> 内不动（否则敌人会不停往玩家身上挤，表现为贴着玩家抖动）、<paramref name="chaseRange"/> 外不动（不给方向 ⇒ 走指数衰减，是"滑停"而不是"定住"，否则"跑得够远能脱离"永远不可能成立）、方向向量为零不动（返回"不动"而不是硬塞默认方向，且必须先于距离判断；零向量归一化是 <c>NaN</c>，角色会带着非数坐标消失）。</remarks>
        public static Steering Resolve(
            Vector2 self,
            Vector2 target,
            float stopDistance,
            float chaseRange,
            float maxSpeed)
        {
            Vector2 delta = target - self;

            if (delta.sqrMagnitude <= 0f) return default;

            float distance = delta.magnitude;

            if (distance > chaseRange) return default;

            float speed = distance <= stopDistance ? 0f : maxSpeed;

            return new Steering(delta, speed);
        }
    }
}
