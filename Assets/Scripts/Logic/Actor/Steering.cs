using UnityEngine;

namespace DeepseaOil.Logic
{
    public readonly struct Steering
    {
        /// <summary>期望方向，可能不是单位向量（目标−自己）；零向量=无期望方向，归一化只归 ActorLogic.SteerTowards</summary>
        public readonly Vector2 Direction;

        /// <summary>目标速度（单位/秒）</summary>
        public readonly float Speed;

        public Steering(Vector2 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        public bool IsIdle => Direction.sqrMagnitude <= 0f || Speed <= 0f;

        /// <remarks>静态纯函数，只吃参数、不持状态；两个距离都是世界单位。三个"不动"各对应一类缺陷：stopDistance 内不动（否则贴脸抖动）；chaseRange 外不动（否则"跑得够远能脱离"永不成立）；零向量不动（必须先于距离判断，零向量归一化是 NaN）。</remarks>
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
