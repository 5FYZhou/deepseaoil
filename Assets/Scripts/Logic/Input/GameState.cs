namespace DeepseaOil.Logic
{
    /// <summary>游戏状态，逻辑层概念，执行在表现层</summary>
    public enum GameState
    {
        /// <summary>默认值，据此进菜单</summary>
        None = 0,

        Menu,

        /// <summary> 播放CG时的状态 </summary>
        CG,

        Paused,

        Running,

        /// <summary>确认退出的那一态，真退出是 Exit</summary>
        BeforeExit,

        Exit,
    }
}
