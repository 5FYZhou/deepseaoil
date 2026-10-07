namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 由持有者驱动的场景实体：谁造谁销毁，自己不自驱。
    /// </summary>
    /// <remarks>
    /// 生命周期由持有者负责：造 → Tick → Dispose。
    /// <c>EnemyActor</c> 不实现本接口：它走物理帧，签名是 <c>FixedTick(now, dt)</c> 两参。
    /// </remarks>
    public interface IDrivenEntity
    {
        /// <summary>推进一个渲染帧。暂停时 <c>deltaTime</c> 为 0，实体自然冻结。</summary>
        void Tick(float deltaTime);

        /// <summary>回收本实体。幂等：重复调用不得重复销毁。</summary>
        void Dispose();

        /// <summary>是否还在场（球：还在飞；掉落物：还没被领取）。</summary>
        bool IsAlive { get; }
    }
}
