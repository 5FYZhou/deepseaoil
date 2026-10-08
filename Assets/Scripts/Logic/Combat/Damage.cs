using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    public enum DamageSource
    {
        Tile = 0,

        /// <summary>环境伤害，预留，无发布者</summary>
        Environment = 1,

        /// <summary>接触伤害，敌人贴身时触发</summary>
        Contact = 2,
    }

    /// <summary>一次伤害结算的事实，位置为世界坐标贴地</summary>
    public readonly struct Damage
    {
        public readonly Vector2 Point;

        /// <summary>指向受害者的单位方向，重合时为 up</summary>
        public readonly Vector2 Direction;

        /// <summary>伤害值，0=纯效果不扣血</summary>
        public readonly float Amount;

        /// <summary>击退冲量，速度(单位/秒)，0=不击退，不乘 Δt</summary>
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

        /// <remarks>正中命中给"上"，避免方向为零时归一化出 NaN</remarks>
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
