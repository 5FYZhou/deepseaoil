namespace DeepseaOil.Logic
{
    /// <summary>
    /// 状态效果层的状态标签：受击 / 硬直这类"作用在角色身上的效果"。玩家与敌人共用。
    /// </summary>
    /// <remarks>
    /// <see cref="Empty"/> 是"还没进入任何状态"的哨兵，永不注册。
    /// 本层不写速度，只产出门禁（见 <c>MoveGates</c>）；数值差异走各自的 <c>CharacterConfig</c>。
    /// </remarks>
    public enum StatusStateTag
    {
        /// <summary>还没有进入任何状态（哨兵，永不注册）。</summary>
        Empty = 0,

        /// <summary>平常：没有效果，门禁为空。</summary>
        Normal,

        /// <summary>受击：速度被外力接管，输入暂时失效，滑停到零即结束。</summary>
        Hurt,
    }
}
