namespace DeepseaOil.Logic.Combat
{
    /// <summary>最基本生命体征，是"谁是活的"唯一判据</summary>
    public interface IAlivable
    {
        bool IsAlive { get; }
    }
}
