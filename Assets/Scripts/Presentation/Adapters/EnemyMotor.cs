using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>敌人移动执行器，额外参数只有连续碰撞检测：敌人被击退成高速时单帧位移不可控</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyMotor : ActorMotor
    {
        protected override void ApplyPhysics(Rigidbody2D rigidbody2D)
        {
            base.ApplyPhysics(rigidbody2D);

            rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }
}
