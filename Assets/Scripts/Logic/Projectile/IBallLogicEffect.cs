using UnityEngine;
using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>球落地后的<b>逻辑效果</b>：把"落地"翻译成"世界状态怎么变"。</summary>
    /// <remarks>契约：<b>球不直接伤害任何人</b>（决策 D7），伤害全部由格子产生；本接口只驱动逻辑，飞行 / 阴影 / 冲量 / 音效归表现层。接口是"球 → 世界"的可替换接缝：旧版合成系统"永久砍还是先不做"需求书里还没答案，有答案时新增实现即可，不必回来改落地结算。</remarks>
    public interface IBallLogicEffect
    {
        /// <param name="landingCell">落点格（调用方须已吸附到格子）。</param>
        void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx);
    }

    /// <summary>球效果能用到的世界操作。<b>由世界侧实现</b>（当前是球调度器）。</summary>
    /// <remarks>刻意<b>只开一个口</b>：要加"球直接造成伤害 / 球触发粒子"就在这里加一条并写清语义，别把 <c>GridLogic</c> 或 <c>EffectModule</c> 递给效果实现。
    /// 参数是"格状态"而不是"球"：世界侧只需知道"把这一格切成什么"，于是这一层与 <c>projectile</c> 表解耦。</remarks>
    public interface IBallLogicEffectContext
    {
        /// <summary>请求把某格切成指定状态，并结算这次转换的冲击。</summary>
        void RequestTileState(Vector3Int cell, TileStateType next);
    }
}
