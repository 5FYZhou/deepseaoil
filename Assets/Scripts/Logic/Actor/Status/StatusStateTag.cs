namespace DeepseaOil.Logic
{
    /// <summary>
    /// <b>状态效果层</b>的状态标签：受击 / 硬直这类"作用在角色身上的效果"。<b>玩家与敌人共用</b>。
    /// </summary>
    /// <remarks>
    /// 领域式分层里的第一层（状态效果 → 战斗 → 移动）。
    /// <see cref="Empty"/> 是"还没进入任何状态"的哨兵，永不注册。
    /// <para>与移动层的分工：本层的状态<b>不写速度</b>，只产出门禁（见 <c>MoveGates</c>）——
    /// "被撞飞"在这一层是一个状态，而"把速度改成多少"仍由移动层执行。</para>
    /// <para><b>为什么它不是"玩家专用"：</b>受击滑停是角色的共性，敌人与它的差别只有数值
    /// （走各自的 <c>CharacterConfig</c>）。收口前敌人另有一套"整数帧禁足"，于是同一件事
    /// 在同一工程里有两个答案。</para>
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
