namespace DeepseaOil.Logic
{
    /// <summary>游戏状态，逻辑层概念，执行在表现层</summary>
    public enum GameState
    {
        /// <summary>默认值，据此进菜单</summary>
        None = 0,

        Menu,

        CG,
        /// <summary> 专指关卡进行中的暂停 </summary>
        Paused,

        Running,
        /// <summary> 关卡内玩家死亡后-切换下一关前的状态 </summary>
        Settle,

        Exit,

        SceneTransition,
    }
}
