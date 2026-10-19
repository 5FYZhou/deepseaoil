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

        /// <remarks>静态纯函数，只吃参数、不持状态；距离是世界单位。两个"不动"各对应一类缺陷：stopDistance 内不动（否则贴脸抖动）；零向量不动（必须先于距离判断，零向量归一化是 NaN）。
        /// deceleration &gt; 0 时按 √(2a·d) 收到停靠环：否则满速进入停靠距离后才归零，会滑进玩家碰撞体。</remarks>
        public static Steering Resolve(
            Vector2 self,
            Vector2 target,
            float stopDistance,
            float maxSpeed,
            float deceleration)
        {
            Vector2 delta = target - self;

            if (delta.sqrMagnitude <= 0f) return default;

            float distance = delta.magnitude;

            if (distance <= stopDistance) return new Steering(delta, 0f);

            float speed = maxSpeed;

            if (deceleration > 0f)
            {
                float brake = Mathf.Sqrt(2f * deceleration * (distance - stopDistance));

                if (brake < speed) speed = brake;
            }

            return new Steering(delta, speed);
        }
    }
}
