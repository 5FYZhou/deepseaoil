namespace DeepseaOil.Logic.Movement
{
    /// <summary>Empty=default 哨兵永不注册；加新状态三处：此处 → Movement/States/ 状态类 → MoveGroup 注册</summary>
    public enum MovementStateTag
    {
        Empty = 0,
        Idle,
        Move,
        Dash,
    }
}
