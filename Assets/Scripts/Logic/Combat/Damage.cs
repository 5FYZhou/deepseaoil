using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    public enum DamageSource
    {
        Tile = 0,

        /// <summary>环境（陷阱 / 地形 / 脚本）。预留，当前无发布者。</summary>
        Environment = 1,

        /// <summary>接触伤害：敌人贴在身上（世界侧判定"谁被打到了"之后，经玩家侧入口触发）。</summary>
        Contact = 2,
    }

    /// <summary>一次伤害结算的全部事实。<b>纯数据</b>，由 <see cref="IDamageable"/> 的宿主消费。</summary>
    public readonly struct Damage
    {
        /// <summary>结算位置（世界坐标，贴地）。方向与范围都以它为基准。</summary>
        public readonly Vector2 Point;

        /// <summary>从 <see cref="Point"/> 指向受害者的<b>单位</b>方向；两者重合时是 <see cref="Vector2.up"/>。</summary>
        public readonly Vector2 Direction;

        /// <summary>伤害值；<c>0</c> = 纯效果（只击退、不扣血）。</summary>
        public readonly float Amount;

        /// <summary>击退冲量（速度，单位/秒），<c>0</c> = 不击退（受害方不该凭空推自己一下）。<b>不</b>乘 Δt。</summary>
        public readonly float Impulse;

        public readonly DamageSource Source;

        public bool HasKnockback => Impulse > 0f && Direction.sqrMagnitude > 0f;

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

        /// <remarks><b>正中命中时给"上"</b>：落点压在受害者身上时方向向量为零，归一化会产生 <c>NaN</c>（角色会带着非数坐标消失），也不该让站在落点正中的敌人免疫击退。</remarks>
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
