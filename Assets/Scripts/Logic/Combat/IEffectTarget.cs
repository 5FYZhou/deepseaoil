using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>格上效果的<b>下发目标</b>：站在某一格上、并能被格子作用的东西。Logic 层端口，由表现层的组合根实现。</summary>
    /// <remarks>
    /// 它比"能受伤的人"宽一格：格子的效果不止伤害（减速 / 击退 / 麻痹 / 将来更多），归属表要回答的是"谁站在这格上"。
    /// 「能不能受伤」由结算那一刻再判 <see cref="IDamageable"/> —— 于是加一种新能力不需要动归属表。
    /// <para><b>生死体征经 <see cref="IAlivable"/> 继承</b>：上游的 <c>IEffectTarget.IsDead</c> <b>不采用</b>（与"统一生死体征"的裁定冲突；
    /// <c>IsDead</c> 作为"结算窗口已经关上"的语义不引进）。</para>
    /// <para><b>命名规则：能力接口一律 <c>-able</c> 后缀</b>（<c>IAlivable</c> / <c>IEffectTarget</c> / <c>IDamageable</c> / <c>ISlowable</c> / <c>IKnockBackable</c> / <c>IStunnable</c> / <c>IManagedActor</c>）。</para>
    /// </remarks>
    public interface IEffectTarget : IAlivable
    {
        /// <summary>当前位置（世界坐标）。<b>语义一律是"脚底中心"</b> —— 格归属、受击方向、结算半径全部按它算。</summary>
        /// <remarks><b>最高规则级</b>：任何"取位置"的成员，默认语义就是脚底中心。
        /// 需要身体中心（受击点、绕身体旋转、命中判定中心）的地方<b>必须自己显式命名</b>（如 <c>BodyCenter</c> / <c>HitCenter</c>），
        /// <b>不得复用 <c>Position</c> 这个名字含糊过去</b>。</remarks>
        Vector2 Position { get; }
    }
}
