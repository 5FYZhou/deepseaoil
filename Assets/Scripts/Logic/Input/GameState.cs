namespace DeepseaOil.Logic
{
    /// <summary>游戏状态，逻辑层概念，执行在表现层</summary>
    public enum GameState
    {
        /// <summary>默认值，据此进菜单</summary>
        None = 0,

        Menu,

        CG,

        Paused,

        Running,

        Exit,
    }
}
