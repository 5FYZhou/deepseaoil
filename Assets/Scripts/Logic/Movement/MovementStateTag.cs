namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 移动状态机中的状态。
    /// </summary>
    /// <remarks>
    /// 状态集随品类而定：平台跳跃时代的 <c>Jump</c> / <c>DoubleJump</c> / <c>Fall</c> / <c>WallSlide</c>
    /// 在俯视角下没有驱动条件（其 <c>IsDone</c> 依赖"重力把 y 轴速度压到 ≤ 0"，而俯视角重力已移除），已移除。
    /// 需要新的移动状态时，在此加标签 → 在 <c>Movement/States/</c> 加状态类 → 在 <c>MoveGroup</c> 注册。
    /// <see cref="Empty"/> 是 <c>default</c> 的具名哨兵值：永不注册进状态机，仅用于表达"没有目标状态"。
    /// </remarks>
    public enum MovementStateTag
    {
        Empty = 0, // =default
        Idle,
        Move,
        Dash,
    }
}
