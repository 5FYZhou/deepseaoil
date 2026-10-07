using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>敌人移动执行器：读写、镜像与帧末唯一一次写速度都在 <see cref="ActorMotor"/>（引擎回读口与账本的差别见 <c>IActorMotor</c>）；额外参数只有连续碰撞检测 —— 敌人会被击退成高速，那一帧的位移不可控（玩家速度由输入给出、量级固定）。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyMotor : ActorMotor
    {
        /// <inheritdoc />
        protected override void ApplyPhysics(Rigidbody2D rigidbody2D)
        {
            base.ApplyPhysics(rigidbody2D);

            rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }
}
