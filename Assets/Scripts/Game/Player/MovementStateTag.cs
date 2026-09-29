namespace DeepSeaOil.Logic.Movement
{
    /// <summary>
    /// 移动状态机中的状态。
    /// </summary>
    /// <remarks>
    /// 起跳与二段跳分开（动画要能区分），蹬墙跳并入 <c>WallSlide</c>，理由见 <c>Docs/M1微规划.md</c> §三 D6、D12。
    /// <see cref="Empty"/> 是 <c>default</c> 的具名哨兵值：永不注册进状态机，仅用于表达"没有目标状态"。
    /// </remarks>
    public enum MovementStateTag
    {
        Empty = 0, // =default
        Idle,
        Move,
        Jump,
        DoubleJump,
        Fall,
        Dash,
        WallSlide,
    }
}
