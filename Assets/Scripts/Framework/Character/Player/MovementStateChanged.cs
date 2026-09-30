namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 移动状态发生切换时广播的事实。
    /// </summary>
    public readonly struct MovementStateChanged
    {
        /// <summary>切换后的状态。</summary>
        public readonly MovementStateTag Current;

        /// <summary>切换前的状态。</summary>
        public readonly MovementStateTag Previous;

        public MovementStateChanged(MovementStateTag current, MovementStateTag previous)
        {
            Current = current;
            Previous = previous;
        }
    }
}
