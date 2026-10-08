using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>格上效果的下发目标：站在某一格上、并能被格子作用的东西；Logic 层端口，由表现层的组合根实现</summary>
    /// <remarks>比"能受伤的人"宽一格：格子的效果不止伤害（减速/击退/麻痹/将来更多），归属表回答的是"谁站在这格上"，"能不能受伤"由结算那一刻再判 IDamageable——加一种新能力不需要动归属表。生死体征统一走继承来的 IAlivable</remarks>
    public interface IEffectTarget : IAlivable
    {
        /// <summary>当前位置（世界坐标），语义一律是"脚底中心"——格归属、受击方向、结算半径全部按它算</summary>
        /// <remarks>最高规则级：任何"取位置"的成员默认就是脚底中心；需要身体中心（受击点、绕身体旋转、命中判定中心）的地方必须自己显式命名（如 BodyCenter / HitCenter），不得复用 Position 含糊过去</remarks>
        Vector2 Position { get; }
    }
}
