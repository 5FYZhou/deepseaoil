using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Projectile
{
    /// <summary>球落地后的<b>逻辑效果</b>：把"落地"翻译成"世界状态怎么变"。</summary>
    /// <remarks>契约：<b>球不直接伤害任何人</b>（决策 D7），伤害全部由格子产生；本接口只驱动逻辑，飞行 / 阴影 / 冲量 / 音效归表现层。</remarks>
    public interface IBallLogicEffect
    {
        /// <param name="landingCell">落点格（调用方须已吸附到格子）。</param>
        void Apply(Vector3Int landingCell, ProjectileSpec ball, IBallLogicEffectContext ctx);
    }

    /// <summary>球效果能用到的世界操作。<b>由世界侧实现</b>（当前是球调度器）。</summary>
    /// <remarks>刻意<b>只开一个口</b>：要加"球直接造成伤害 / 球触发粒子"就在这里加一条并写清语义，别把 <c>GridLogic</c> 或 <c>EffectModule</c> 递给效果实现。
    /// 参数是"<b>球的元素</b>"而不是"目标格状态"：世界侧要拿它和地形元素做合成、查 <c>element_rule</c>，于是这一层与"结果状态有哪些"解耦（旧版砍掉的那条"直接指定状态"就是这么坏的）。</remarks>
    public interface IBallLogicEffectContext
    {
        /// <summary>请求在落点格结算一次元素反应（合成 → 查规则 → 切状态 ＋ 提交效果清单）。</summary>
        void RequestTileState(Vector3Int cell, in ElementValue element);
    }
}
