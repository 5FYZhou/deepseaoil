using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>
    /// 可被结算的目标。<b>Logic 层端口</b>，由表现层的组合根实现（敌人、将来的可破坏物）。
    /// </summary>
    /// <remarks>
    /// <b>为什么带 <see cref="Position"/>：</b>结算方要用"从结算点指向受害者"算击退方向，
    /// 而它手里只有一个接口引用。少了这个属性，逻辑层就只能反过来去问表现层每个目标在哪 ——
    /// 那等于把"目标是谁"的接线又还给调用方。
    /// <para><b>"还活着吗"不在这里</b>（见 <see cref="IAlivable"/>）：能受伤与活着不是一件事，
    /// 而且收口前玩家侧叫 <c>IsAlive</c>、怪物侧叫 <c>IsDead</c>，同一个问题两个方向 ——
    /// 统一到 <see cref="IAlivable"/> 之后，结算方判"是尸体"只看一个名字。</para>
    /// <para>实现方必须满足：<see cref="TakeDamage"/> <b>不抛异常</b>，且对
    /// <see cref="Damage.Amount"/> 为 0 的结算不扣血（只做被显式要求的击退）。</para>
    /// </remarks>
    public interface IDamageable
    {
        /// <summary>当前位置（世界坐标），用来算结算方向。</summary>
        Vector2 Position { get; }

        /// <summary>结算一次伤害。<b>唯一受伤入口</b>。</summary>
        void TakeDamage(in Damage damage);
    }
}
