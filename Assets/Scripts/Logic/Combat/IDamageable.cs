namespace DeepseaOil.Logic.Combat
{
    /// <summary>可被结算的目标，Logic 层端口，表现层组合根实现</summary>
    /// <remarks>生死体征与位置由 IEffectTarget / IAlivable 给；TakeDamage 不抛异常，Amount 为 0 不扣血</remarks>
    public interface IDamageable : IEffectTarget
    {
        /// <summary>结算一次伤害</summary>
        void TakeDamage(in Damage damage);
    }
}
