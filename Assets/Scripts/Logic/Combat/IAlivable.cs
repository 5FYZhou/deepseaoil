namespace DeepseaOil.Logic.Combat
{
    /// <summary>最基本的生命体征：这个目标还活着吗。</summary>
    /// <remarks>
    /// 只有这一条属性，是"谁是活的"的唯一判据；能受伤、能被减速、能被推动都是别的事。
    /// </remarks>
    public interface IAlivable
    {
        /// <summary>是否还活着。</summary>
        bool IsAlive { get; }
    }
}
