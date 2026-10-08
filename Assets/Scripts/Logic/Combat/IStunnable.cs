namespace DeepseaOil.Logic.Combat
{
    /// <summary>能被麻痹，一段时间内不能行动；与减速是两种独立能力，不共用通道</summary>
    public interface IStunnable
    {
        /// <summary>麻痹秒数，重复施加取较长者或续期由实现决定</summary>
        void ApplyStun(float seconds);
    }
}
