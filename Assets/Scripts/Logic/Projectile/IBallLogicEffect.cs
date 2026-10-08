using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>球落地后的逻辑效果：落地→世界状态怎么变</summary>
    /// <remarks>契约：球不直接伤害任何人（决策D7），伤害由格子产生；飞行/阴影/冲量/音效归表现层</remarks>
    public interface IBallLogicEffect
    {
        void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx);
    }

    /// <summary>球效果能用到的世界操作，由世界侧实现（当前是球调度器）</summary>
    /// <remarks>只开一个口：别把 GridLogic/EffectModule 递给效果实现。参数是球的元素而非目标格状态，世界侧拿它合成、查 element_rule</remarks>
    public interface IBallLogicEffectContext
    {
        void RequestTileState(Vector3Int cell, in ElementValue element);
    }
}
