namespace DeepseaOil.Presentation
{
    // 命名空间约定：一个子目录一个命名空间，专管层内各组边界；新增目录必须带自己的命名空间。
    // 反例：Presentation/Debug/ 曾名 …Presentation.Debug，让本层 Debug.LogError 全变 CS0234，故改名 Diagnostics/。

    public static class SceneOrder
    {
        /// <summary>玩家侧：必须早于世界侧，否则接触判定读上一帧站位</summary>
        public const int Player = -100;

        public const int World = 0;
    }

    /// <summary>场景级组合根：自己注册/注销</summary>
    public interface ISceneRoot
    {
        /// <summary>驱动顺序，小者先</summary>
        int Order { get; }

        /// <summary>GameRoot 调一次，必须幂等</summary>
        void Attach();
    }

    /// <summary>渲染帧驱动</summary>
    public interface IRenderTicked
    {
        /// <summary>暂停时 deltaTime 也为 0</summary>
        void RenderTick(float deltaTime);
    }

    /// <summary>GameRoot.FixedUpdate 驱动；暂停时 Unity 不跑</summary>
    public interface IPhysicsTicked
    {
        void FixedTick(float deltaTime);
    }
}
