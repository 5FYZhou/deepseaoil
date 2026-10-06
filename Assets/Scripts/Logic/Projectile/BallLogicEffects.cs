using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>
    /// 默认的落地效果：把落点格切成该球种配置的状态（水球 → 泥浆）。
    /// </summary>
    /// <remarks>
    /// <b>目标状态来自 <see cref="BallDefinition.TileState"/> 而不是代码</b>
    /// （<c>projectile.tile_state</c>）：换一种球、换一种状态都只是改表，不需要动这里。
    /// </remarks>
    public sealed class TileStateLogicEffect : IBallLogicEffect
    {
        /// <inheritdoc />
        public void Apply(Vector3Int landingCell, in BallDefinition ball, IBallLogicEffectContext ctx)
        {
            if (ctx == null) return;

            ctx.RequestTileState(landingCell, ball.TileState);
        }
    }

    /// <summary>
    /// 空逻辑效果：落地不改世界状态（土球）。
    /// </summary>
    /// <remarks>
    /// <b>它是刻意的空，不是没写完。</b>需求里土球的落地效果还挂着"待策划"
    /// （需求书第八节第 3 题：旧版合成系统永久砍还是先不做），而白模 v2 的行为就是"什么都不做"。
    /// 让它走一个具名实现而不是跳过，是为了让"土球现在没有世界效果"这件事在代码里是一个
    /// <b>能搜到的答案</b>，而不是一个需要推断的空白。
    /// <para>它<b>仍然推无生命刚体</b>：那是物理冲量，由表现层的冲量执行者做，
    /// 与"世界状态怎么变"分开。</para>
    /// </remarks>
    public sealed class NullLogicEffect : IBallLogicEffect
    {
        /// <inheritdoc />
        public void Apply(Vector3Int landingCell, in BallDefinition ball, IBallLogicEffectContext ctx)
        {
        }
    }
}
