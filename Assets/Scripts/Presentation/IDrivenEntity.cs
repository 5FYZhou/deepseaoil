namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 由持有者驱动的场景实体：<b>谁造谁销毁，自己不自驱</b>。
    /// </summary>
    /// <remarks>
    /// 球与掉落物的生命周期约定完全相同（造 → Tick → Dispose，回收权归持有者），
    /// 却各自与自己的 director 硬绑 —— director 只认具体类型。抽出接口之后，
    /// "怎么持有一批实体"这件事不必为每种实体各写一遍。
    /// <para><b>覆盖范围是刻意收窄的</b>：只有 <see cref="Presentation.Ball.BallActor"/>（纯 C# 组合件）
    /// 与 <see cref="Presentation.Drop.DropActor"/>（Mono 抽象基类）实现它。
    /// <c>EnemyActor</c> <b>不纳入</b> —— 它的帧相位不同（物理帧，签名为
    /// <c>FixedTick(now, dt)</c> 两参），塞进 <c>Tick(float dt)</c> 只能靠忽略一个参数，
    /// 而"参数被忽略"正是下一个读代码的人会踩的坑。</para>
    /// <para><b>它不是 MonoBehaviour 的替代品</b>：实现方照样可以是 MonoBehaviour
    /// （掉落物就是），接口只约束"被驱动"这件事。</para>
    /// </remarks>
    public interface IDrivenEntity
    {
        /// <summary>推进一个渲染帧。暂停时 <c>deltaTime</c> 为 0，实体自然冻结。</summary>
        void Tick(float deltaTime);

        /// <summary>回收本实体。<b>幂等</b>：重复调用不得重复销毁。</summary>
        void Dispose();

        /// <summary>是否还在场（球：还在飞；掉落物：还没被领取）。</summary>
        bool IsAlive { get; }
    }
}
