using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>落地效果：把落点格交给元素反应链（球元素 ⊕ 该格地形元素 → 查 <c>element_rule</c> → 新状态＋效果清单）。</summary>
    /// <remarks>目标状态<b>不再来自表值</b>（旧链路的 <c>projectile.tile_state</c> 列已删）：球只交出"自己的元素"，由世界侧按规则算结果。</remarks>
    public sealed class TileStateLogicEffect : IBallLogicEffect
    {
        /// <inheritdoc />
        public void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx)
        {
            if (ctx == null) return;

            ctx.RequestTileState(landingCell, ball.Element);
        }
    }
}
