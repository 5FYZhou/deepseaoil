namespace DeepseaOil.Logic.Combat
{
    /// <summary>可被结算的目标：能受伤。Logic 层端口，由表现层的组合根实现（敌人、将来的可破坏物）。</summary>
    /// <remarks>
    /// <b>只管受伤，不管生死</b>：生死体征走 <see cref="IAlivable"/>（经 <see cref="IEffectTarget"/> 继承得到），"取位置"也由父接口给 ——
    /// 本接口只声明"我吃得住一次伤害"。
    /// <para>实现方必须满足：<see cref="TakeDamage"/> <b>不抛异常</b>，对 <see cref="Damage.Amount"/> 为 0 的结算不扣血。</para>
    /// </remarks>
    public interface IDamageable : IEffectTarget
    {
        /// <summary>结算一次伤害。唯一受伤入口。</summary>
        void TakeDamage(in Damage damage);
    }
}
