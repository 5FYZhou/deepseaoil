using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>
    /// 球落地后的<b>逻辑效果</b>。
    /// </summary>
    /// <remarks>
    /// <b>球不直接伤害任何人</b>（决策 D7）：伤害全部由格子产生。接口存在的意义是给"球 → 世界"
    /// 这一步留一个可替换的接缝 —— 需求书里"旧版合成系统永久砍还是先不做"还没有答案，
    /// 等它有答案时新增一个实现即可，不必回来改落地结算。
    /// <para>表现层的东西（瞬闪、冲量、音效）<b>不在</b>这里：它们是无生命刚体与视觉的事，
    /// 在 <c>LandingResolver</c> 里，与"世界状态怎么变"分开。</para>
    /// </remarks>
    public interface IBallEffect
    {
        /// <summary>施加一次落地效果。</summary>
        /// <param name="landingCell">落点格（已吸附到格子）。</param>
        /// <param name="ball">球种配置行。</param>
        /// <param name="ctx">能请求的世界操作。</param>
        void Apply(Vector3Int landingCell, in BallSpec ball, IBallEffectContext ctx);
    }

    /// <summary>球效果能用到的世界操作。由组合根实现（当前是落点结算器）。</summary>
    /// <remarks>
    /// 刻意<b>只开一个口</b>：能请求的东西越少，"球能干什么"这件事就越集中在格子系统里。
    /// 将来要加"球直接造成伤害"或"球触发粒子"时，在这里加一条并写清语义，
    /// 而不是把 <c>GridLogic</c> 或 <c>EffectModule</c> 直接递给效果实现。
    /// </remarks>
    public interface IBallEffectContext
    {
        /// <summary>请求把某格切到该球种对应的状态，并结算这次落地的冲击。</summary>
        void RequestTileState(Vector3Int cell, in BallSpec ball);
    }
}
