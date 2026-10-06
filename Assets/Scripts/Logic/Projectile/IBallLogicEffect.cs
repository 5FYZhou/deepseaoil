using UnityEngine;
using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>
    /// 球落地后的<b>逻辑效果</b>：把"落地"翻译成"世界状态怎么变"。
    /// </summary>
    /// <remarks>
    /// <b>球不直接伤害任何人</b>（决策 D7）：伤害全部由格子产生。接口存在的意义是给"球 → 世界"
    /// 这一步留一个可替换的接缝 —— 需求书里"旧版合成系统永久砍还是先不做"还没有答案，
    /// 等它有答案时新增一个实现即可，不必回来改落地结算。
    /// <para><b>名字里的 <c>Logic</c> 是刻意的：</b>表现层的事（球的飞行、阴影、冲量、音效）
    /// <b>不在这里</b> —— 它们由表现层的球实体自持，本接口只驱动逻辑。</para>
    /// </remarks>
    public interface IBallLogicEffect
    {
        /// <summary>施加一次落地效果。</summary>
        /// <param name="landingCell">落点格（已吸附到格子）。</param>
        /// <param name="ball">球定义（取值边界）。</param>
        /// <param name="ctx">能请求的世界操作。</param>
        void Apply(Vector3Int landingCell, in BallDefinition ball, IBallLogicEffectContext ctx);
    }

    /// <summary>球效果能用到的世界操作。<b>由世界侧实现</b>（当前是球调度器）。</summary>
    /// <remarks>
    /// 刻意<b>只开一个口</b>：能请求的东西越少，"球能干什么"这件事就越集中在格子系统里。
    /// 将来要加"球直接造成伤害"或"球触发粒子"时，在这里加一条并写清语义，
    /// 而不是把 <c>GridLogic</c> 或 <c>EffectModule</c> 直接递给效果实现。
    /// <para><b>参数是"格状态"而不是"球"：</b>世界侧只需要知道"把这一格切成什么"，
    /// 不需要认识球种 —— 于是这一层与 <c>projectile</c> 表彻底解耦。</para>
    /// </remarks>
    public interface IBallLogicEffectContext
    {
        /// <summary>请求把某格切成指定状态，并结算这次转换的冲击。</summary>
        void RequestTileState(Vector3Int cell, TileStateType next);
    }
}
