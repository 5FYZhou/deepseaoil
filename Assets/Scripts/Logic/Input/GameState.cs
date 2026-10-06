namespace DeepseaOil.Logic
{
    /// <summary>
    /// 游戏状态：<b>它是逻辑层的概念</b>（决定"这一帧游戏在不在跑"），所以住在 Logic 层。
    /// </summary>
    /// <remarks>
    /// 收口之前它定义在 <c>Presentation/Root/GameManager.cs</c> 里、命名空间却写的是
    /// <c>DeepseaOil.Logic</c> —— 目录与命名空间不一致，而 <c>UILogicContext</c>（逻辑层）
    /// 又必须引用它。现在把它单独放回 <c>Logic/Input/</c>：类型位置与它的使用者一致。
    /// <para>状态的<b>执行</b>（切面板、发暂停意图）仍在表现层的 <c>GameManager</c>：
    /// 枚举属于逻辑，怎么表现属于表现层。</para>
    /// </remarks>
    public enum GameState
    {
        /// <summary>还没有人切过状态（<c>GameRoot</c> 启动时据此进菜单）。</summary>
        None = 0,

        /// <summary>开始界面。</summary>
        Menu,

        /// <summary>暂停中。</summary>
        Paused,

        /// <summary>游戏中。</summary>
        Running,

        /// <summary>确认退出游戏。</summary>
        BeforeExit,

        /// <summary>退出游戏。</summary>
        Exit,
    }
}
