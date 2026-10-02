namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 投掷物种类。决定颜色与（第二阶段的）落地效果。
    /// </summary>
    /// <remarks>
    /// 白模阶段只有两种，且**没有效果对象**：颜色直接由本枚举查表得到（见
    /// <see cref="ThrowConstants"/>）。之所以还敢这么硬编码，是因为需求书第八节第 3 题
    /// （旧版合成系统永久砍还是白模先不做）尚未答复 —— 若合成会回来，"球的效果"届时必须收进接口；
    /// 现在抽接口会是一次没有第二个实现者的抽象。
    /// </remarks>
    public enum BallType
    {
        /// <summary>水球：蓝色。第二阶段落地留泥浆减速敌人。</summary>
        Water,

        /// <summary>土球：棕色。第二阶段落地扬尘后消失。</summary>
        Earth,
    }
}
