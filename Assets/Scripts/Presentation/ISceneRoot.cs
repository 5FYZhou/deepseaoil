namespace DeepseaOil.Presentation
{
    // 命名空间约定：一个子目录一个命名空间（本工程没有 asmdef，命名空间是层与组之间唯一的边界表达）。三处刻意平坦的例外：Presentation/Root/、Presentation/UI/Panel/、本层根下两个接口文件；其余新增目录必须带自己的命名空间。
    // 反例留档：Presentation/Debug/ 曾按目录取名为 DeepseaOil.Presentation.Debug，会让本层所有 Debug.LogError(...) 变成 CS0234，故目录改名 Diagnostics/。

    public static class SceneOrder
    {
        /// <summary>玩家侧：必须早于世界侧，顺序反了接触判定会读到上一帧的站位。</summary>
        public const int Player = -100;

        public const int World = 0;
    }

    /// <summary>场景级组合根：自己在 Start 里向 GameRoot 注册、销毁时自己注销；顺序由 Order 决定而<b>不由注册先后决定</b>（多个 MonoBehaviour 的 Start 先后是 Unity 的自由），小者先跑。</summary>
    public interface ISceneRoot
    {
        /// <summary>驱动顺序，小者先。档位见 <see cref="SceneOrder"/>。</summary>
        int Order { get; }

        /// <summary>装配：由 GameRoot 在第一个被驱动的帧按 Order 调一次；必须幂等（渲染帧与物理帧两个通道都会请求装配）。</summary>
        void Attach();
    }

    /// <summary>需要被渲染帧驱动的场景根（由 GameRoot.Update 调）。</summary>
    public interface IRenderTicked
    {
        /// <param name="deltaTime"><c>Time.deltaTime</c>；暂停（<c>timeScale = 0</c>）时为 0。</param>
        void RenderTick(float deltaTime);
    }

    /// <summary>需要被物理帧驱动的场景根（由 GameRoot.FixedUpdate 调）；暂停时 Unity 不跑 FixedUpdate，所以不必再挡一层。</summary>
    public interface IPhysicsTicked
    {
        void FixedTick(float deltaTime);
    }
}
