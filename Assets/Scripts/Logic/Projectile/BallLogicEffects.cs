using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>落地效果，落点格交给元素反应链：球元素⊕地形元素→查 element_rule→新状态＋效果清单</summary>
    public sealed class TileStateLogicEffect : IBallLogicEffect
    {
        public void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx)
        {
            if (ctx == null) return;

            ctx.RequestTileState(landingCell, ball.Element);
        }
    }
}
