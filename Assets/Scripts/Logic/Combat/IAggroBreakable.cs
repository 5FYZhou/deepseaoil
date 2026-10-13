namespace DeepseaOil.Logic.Combat
{
    /// <summary>可被清仇恨的目标，由表现层实现</summary>
    public interface IAggroBreakable
    {
        void ForceLoseAggro();
    }
}
