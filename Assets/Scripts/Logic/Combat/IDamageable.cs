using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>可被结算的目标。Logic 层端口，由表现层的组合根实现（敌人、将来的可破坏物）。</summary>
    /// <remarks>
    /// 实现方必须满足：<see cref="TakeDamage"/> <b>不抛异常</b>，对 <see cref="Damage.Amount"/> 为 0 的结算不扣血。
    /// </remarks>
    public interface IDamageable : IEffectTarget
    {
        /// <summary>当前位置（世界坐标），用来算结算方向。</summary>
        Vector2 Position { get; }

        /// <summary>结算一次伤害。唯一受伤入口。</summary>
        void TakeDamage(in Damage damage);
    }
}
