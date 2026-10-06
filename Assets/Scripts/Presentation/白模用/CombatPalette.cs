using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 战斗表现件的颜色表。<b>渲染约定，留在代码里</b>（不进 Luban，也不进 SO）。
    /// </summary>
    /// <remarks>
    /// <b>为什么球种色必须只有一份：</b>同一个球种的颜色有三个消费者 —— 球本体、落地环、
    /// （将来的）HUD 图标。三处各写一份十六进制的话，"水球是蓝的"这件事就再也没有唯一答案了。
    /// <para>敌人身体色在 <c>EnemyVisual</c>（逻辑层）：它是"减速生效了没有"这个<b>判定</b>的
    /// 一部分，需要被测试断言，所以跟着那条纯函数走。</para>
    /// </remarks>
    public static class CombatPalette
    {
        /// <summary>水球色。</summary>
        public static readonly Color WaterBall = new Color(0.20f, 0.55f, 1.00f, 1f);

        /// <summary>土球色。</summary>
        public static readonly Color EarthBall = new Color(0.55f, 0.36f, 0.18f, 1f);

        /// <summary>格子状态变化的提示色（灰白）。与球种无关 —— "是哪颗球"已经由球色表达了。</summary>
        public static readonly Color TileEffect = new Color(0.90f, 0.90f, 0.90f, 1f);

        /// <summary>取球种颜色；未知球种给白色（不会看不见，也不会误导成某一颗球）。</summary>
        public static Color BallColor(BallType type)
        {
            switch (type)
            {
                case BallType.Water: return WaterBall;
                case BallType.Earth: return EarthBall;
                default: return Color.white;
            }
        }
    }
}
