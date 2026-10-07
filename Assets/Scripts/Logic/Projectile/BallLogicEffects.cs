using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    // 默认的落地效果：把落点格切成该球种配置的状态（水球 → 泥浆）。
    // 目标状态来自表值 projectile.tile_state，换球种、换状态只改表。
    public sealed class TileStateLogicEffect : IBallLogicEffect
    {
        /// <inheritdoc />
        public void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx)
        {
            if (ctx == null) return;

            ctx.RequestTileState(landingCell, ball.TileState);
        }
    }

    // 空逻辑效果：落地不改世界状态（土球）。它是刻意的空，不是没写完：
    // 需求书第八节第 3 题（旧版合成系统永久砍还是先不做）待策划定，白模 v2 的行为就是"什么都不做"；
    // 走具名实现是为了让这件事在代码里能搜到。它仍然推无生命刚体 —— 那是表现层的物理冲量。
    public sealed class NullLogicEffect : IBallLogicEffect
    {
        /// <inheritdoc />
        public void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx)
        {
        }
    }
}
