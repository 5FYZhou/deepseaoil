namespace DeepseaOil.Logic.Combat
{
    /// <summary>能被麻痹：一段时间内不能行动。</summary>
    /// <remarks><b>为什么不复用减速通道</b>（§10 定案）：麻痹会挡住输入，减速只改速度；两者是两种独立能力，挤进一个接口会让"只需要一种"的目标被迫实现另一种。
    /// 独立就加接口，但受"实例要实现的窄接口数"上限约束 —— 所以不引入 <c>IEffectReceiver</c> 式的垃圾桶接口。</remarks>
    public interface IStunnable
    {
        /// <summary>麻痹一段时间（秒）；重复施加按"取较长者"或"续期"由实现决定。</summary>
        void ApplyStun(float seconds);
    }
}
