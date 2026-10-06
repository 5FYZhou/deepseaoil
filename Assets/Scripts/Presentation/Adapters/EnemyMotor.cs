using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 敌人移动执行器：共用 <see cref="ActorMotor"/> 的读写与镜像，只补"敌人特有的物理参数"。
    /// </summary>
    /// <remarks>
    /// <b>与玩家执行器的关系：</b>两者是兄弟（同一个基类），差异只有
    /// <see cref="ApplyPhysics"/> 里那一条连续碰撞检测。
    /// <para><b>它现在承载的是"敌人的物理参数归敌人执行器"</b>：收口前
    /// <c>gravityScale</c> / <c>freezeRotation</c> / <c>collisionDetectionMode</c> 写在
    /// <c>EnemyActor.BuildBody</c> 里 —— 那是"造一只敌人"的地方，却顺带定义了"敌人的物理长什么样"。
    /// 现在建刚体只建刚体，物理参数由执行器在初始化时固化（"两处都设一遍"的历史到此为止）。</para>
    /// <para><b>收口前它有一份 91 行的独立实现</b>（自己取刚体、自己管朝向、自己写 Move），
    /// 以及一段"为什么不复用玩家那个 motor"的理由。改成统一结构（敌人与玩家同构）之后，
    /// 那些理由逐条失效：抽象基类不含玩家语义，继承共享的是代码而不是组件实例，
    /// 而重复的代价是两份 <c>Facing</c> 镜像契约会各自漂。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyMotor : ActorMotor
    {
        /// <inheritdoc />
        /// <remarks>连续检测是<b>敌人侧</b>的要求：敌人会被击退成高速（那一帧的位移不可控），
        /// 而玩家速度由输入给出、量级固定。参数放这里，改敌人不影响玩家。</remarks>
        protected override void ApplyPhysics(Rigidbody2D rigidbody2D)
        {
            base.ApplyPhysics(rigidbody2D);

            rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }
}
