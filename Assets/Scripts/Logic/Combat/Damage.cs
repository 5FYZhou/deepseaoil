using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>伤害来源。当前只有一个消费者语义：格子状态转换。</summary>
    /// <remarks>
    /// 白模的 <c>BallType</c> 曾经占着这个字段（"哪颗球打的"），迁移后球不再直接伤害敌人，
    /// 于是它换成"这次结算是谁发起的"。环境伤害（陷阱、地形）先占位，无消费者。
    /// </remarks>
    public enum DamageSource
    {
        /// <summary>格子状态转换产生的结算。</summary>
        Tile = 0,

        /// <summary>环境（陷阱 / 地形 / 脚本）。预留，当前无发布者。</summary>
        Environment = 1,
    }

    /// <summary>
    /// 一次伤害结算的全部事实。<b>纯数据</b>，由 <see cref="IDamageable"/> 的宿主消费。
    /// </summary>
    /// <remarks>
    /// <b>为什么不只是一个 <c>float</c>：</b>受害方要的不只是"扣多少血"。落点决定推开方向、
    /// 冲量决定推多远、来源决定将来要不要区分抗性，这些都是加字段而不是改签名。
    /// <para><b>击退是可选的：</b><see cref="Impulse"/> 为 0 时本结构只表达"扣血"，
    /// 受害方不该凭空推自己一下（白模里 <c>TakeDirectDamage</c> 与 <c>TakeDamage</c> 两条路的差别就在这里，
    /// 迁移后合并成一条：带冲量就推，不带就不推）。</para>
    /// <para>它是 <c>readonly struct</c>：一次落地可能造好几份，不能有堆分配。</para>
    /// </remarks>
    public readonly struct Damage
    {
        /// <summary>结算位置（世界坐标，贴地）。方向与范围都以它为基准。</summary>
        public readonly Vector2 Point;

        /// <summary>从 <see cref="Point"/> 指向受害者的<b>单位</b>方向；两者重合时是 <see cref="Vector2.up"/>。</summary>
        public readonly Vector2 Direction;

        /// <summary>伤害值；<c>0</c> = 纯效果（只击退、不扣血）。</summary>
        public readonly float Amount;

        /// <summary>击退冲量（速度，单位/秒），<c>0</c> = 不击退。<b>不</b>乘 Δt。</summary>
        public readonly float Impulse;

        /// <summary>来源标记。</summary>
        public readonly DamageSource Source;

        /// <summary>是否应当击退：冲量与方向都有效才成立。</summary>
        public bool HasKnockback => Impulse > 0f && Direction.sqrMagnitude > 0f;

        /// <summary>是否应当扣血。</summary>
        public bool HasDamage => Amount > 0f;

        public Damage(
            Vector2 point,
            float amount,
            DamageSource source,
            Vector2 direction = default,
            float impulse = 0f)
        {
            Point = point;
            Amount = amount;
            Source = source;
            Direction = direction;
            Impulse = impulse;
        }

        /// <summary>
        /// 按"结算点 + 受害者位置"造一次结算。<b>静态纯函数</b>。
        /// </summary>
        /// <param name="point">结算点（世界坐标）。</param>
        /// <param name="victim">受害者当前位置。</param>
        /// <param name="amount">伤害值；<c>0</c> 表示只推不扣血。</param>
        /// <param name="source">来源标记。</param>
        /// <param name="impulse">击退冲量；<c>0</c> 表示不击退。</param>
        /// <remarks>
        /// <b>方向在这里算，不在受害方算：</b>"从落点指向我"要用落点，而落点只有施害方知道。
        /// 让受害方反推等于把落点泄露给它。
        /// <para><b>正中命中时给"上"：</b>落点正好压在受害者身上时方向向量为零，归一化会产生 <c>NaN</c>
        /// （角色会带着非数坐标消失）。给一个确定方向比到处判 <c>NaN</c> 便宜，
        /// 也不该让站在落点正中的敌人免疫击退。</para>
        /// </remarks>
        public static Damage At(
            Vector2 point,
            Vector2 victim,
            float amount,
            DamageSource source,
            float impulse = 0f)
        {
            Vector2 delta = victim - point;

            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

            return new Damage(point, amount, source, direction, impulse);
        }
    }
}
