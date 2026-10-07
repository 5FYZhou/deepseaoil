namespace DeepseaOil.Logic
{
    /// <summary>游戏状态：逻辑层概念（决定"这一帧游戏在不在跑"）；状态的执行在表现层 <c>GameManager</c>。</summary>
    public enum GameState
    {
        /// <summary>还没有人切过状态（<c>GameRoot</c> 启动时据此进菜单）；<c>default(GameState)</c> 即此值。</summary>
        None = 0,

        Menu,

        Paused,

        Running,

        /// <summary>确认退出游戏（弹确认框的那一态）；<c>Exit</c> 才是真的退出。</summary>
        BeforeExit,

        Exit,
    }
}
