namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动状态切换时广播的事实</summary>
    public readonly struct MovementStateChanged
    {
        public readonly MovementStateTag Current;

        public readonly MovementStateTag Previous;

        public MovementStateChanged(MovementStateTag current, MovementStateTag previous)
        {
            Current = current;
            Previous = previous;
        }
    }
}
