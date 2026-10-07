namespace DeepseaOil.Logic.Movement
{
    /// <summary>移动状态机中的状态。</summary>
    /// <remarks>
    /// <see cref="Empty"/> 是 <c>default</c> 的具名哨兵值：永不注册进状态机，仅表示"没有目标状态"。
    /// 加新状态需三处：此处加标签 → <c>Movement/States/</c> 加状态类 → <c>MoveGroup</c> 注册。
    /// </remarks>
    public enum MovementStateTag
    {
        Empty = 0, // =default
        Idle,
        Move,
        Dash,
    }
}
