namespace DeepseaOil.Presentation
{
    /// <summary>由持有者驱动，谁造谁销毁</summary>
    /// <remarks>EnemyActor 走物理帧，不实现本接口。</remarks>
    public interface IDrivenEntity
    {
        /// <summary>暂停时 deltaTime=0 即冻结</summary>
        void Tick(float deltaTime);

        /// <summary>幂等，重复调用不重复销毁</summary>
        void Dispose();

        bool IsAlive { get; }
    }
}
