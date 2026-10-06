using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的视效决策（<b>纯函数</b>）：这一帧身体该是什么颜色、头顶数字该写什么、闪不闪。
    /// </summary>
    /// <remarks>
    /// <b>为什么把颜色拿出来做纯函数（而不是留在组合根里）：</b>颜色是"减速生效了没有"与
    /// "这一下打中了没有"的唯一反馈，而这个项目里没有 buff 图标也没有粒子。
    /// 留在 MonoBehaviour 里它就只能靠眼睛验；提出来之后，"深色必须比原色暗""四态两两不同"
    /// 这些是能断言的。
    /// <para><b>四态显式写出来，不做乘法或插值。</b><c>Color.Lerp(BODY, SLOW, t)</c> 或 <c>body * 0.4f</c>
    /// 会让"这一帧到底该是什么色"变成一个算出来的数，既没法断言也没法单独调，
    /// 而"减速时颜色变深"本身就是一个设计决定，它值得有自己的常量。</para>
    /// </remarks>
    public static class EnemyVisual
    {
        /// <summary>正常身体色（偏暖的红，与玩家黄、水球蓝、土球棕都能一眼分开）。</summary>
        public static readonly Color BodyColorNormal = new Color(0.86f, 0.30f, 0.28f, 1f);

        /// <summary>踩在减速格里时的身体色（比正常色明显更深，色相不变）。</summary>
        /// <remarks>
        /// 它是一个独立常量，而不是"代码里乘个 0.4"：乘出来的中间色没法断言、也没法单独调，
        /// 而"减速要看得出来"是这个白模里<b>唯一</b>的减速反馈。
        /// </remarks>
        public static readonly Color BodyColorSlowed = new Color(0.34f, 0.12f, 0.11f, 1f);

        /// <summary>受击闪烁的亮色（比正常色亮，但仍是暖色，不像换了个敌人）。</summary>
        public static readonly Color FlashColor = new Color(1f, 0.92f, 0.90f, 1f);

        /// <summary>踩在减速格里、且正在闪。</summary>
        public static readonly Color FlashColorSlowed = new Color(0.62f, 0.42f, 0.40f, 1f);

        /// <summary>
        /// 这一帧的身体颜色。
        /// </summary>
        /// <param name="slowMultiplier">本帧实际喂给逻辑层的减速系数（<c>&lt; 1</c> 表示被减速）。</param>
        /// <param name="flashOn">闪烁相位的亮半周。</param>
        /// <returns>四态之一：正常 / 减速 / 闪烁 / 减速+闪烁。</returns>
        /// <remarks>
        /// <b>四态而不是"二选一"：</b>减速与受击是两个独立的 debuff，同时发生时两种反馈都要在。
        /// 写成 <c>if (flash) 亮 else if (slow) 深</c> 的话，站在减速格里被打中的敌人看起来
        /// "跟没踩进去一样" —— 而那正是玩家会去判断"这减速到底有没有用"的时刻。
        /// <para>判据用"是否小于 1"而不是"是否等于某个具体系数"：状态将来分等级（更强的减速）时，
        /// 这里不需要跟着改。</para>
        /// </remarks>
        public static Color BodyColor(float slowMultiplier, bool flashOn)
        {
            bool slowed = slowMultiplier < 1f;

            if (slowed) return flashOn ? FlashColorSlowed : BodyColorSlowed;

            return flashOn ? FlashColor : BodyColorNormal;
        }

        /// <summary>
        /// 头顶的剩余耐久数字。
        /// </summary>
        /// <param name="hp">剩余耐久。</param>
        /// <returns>要写进 <c>TextMesh</c> 的字符串；<b>耐久为 0 时是空串</b>。</returns>
        /// <remarks>
        /// 耐久为 0 时不写 <c>"0"</c>：那一帧敌人正在碎裂，显示一个 0 只会让人以为
        /// "它还有 0 点血所以在场上" —— 空串至少不撒谎。
        /// </remarks>
        public static string HpText(int hp)
        {
            return hp > 0 ? hp.ToString() : string.Empty;
        }

        /// <summary>
        /// 这一帧该不该亮。闪烁是<b>相位</b>而不是状态。
        /// </summary>
        /// <param name="time">当前时间（秒，用 <c>Time.time</c>，不累加）。</param>
        /// <param name="hz">闪烁频率（Hz）；非法值按不闪处理。</param>
        /// <returns>亮半周为 <c>true</c>。</returns>
        /// <remarks>
        /// 用相位而不是一个布尔字段：布尔字段会在暂停、掉帧、以及"受击滑停时长改了但闪烁频率没改"
        /// 三种情况下偷偷不同步。
        /// <para>用 <c>Sin</c> 而不是 <c>(time * hz) % 1</c> 取整：取整在 <c>hz</c> 为 0 时会除零，
        /// 而 <c>Sin</c> 在频率为 0 时恒为 0（整段受击都保持亮色），是"能看出不对但不崩"的行为。</para>
        /// </remarks>
        public static bool IsFlashOn(float time, float hz)
        {
            if (float.IsNaN(hz) || hz <= 0f) return false;

            return Mathf.Sin(time * 2f * Mathf.PI * hz) > 0f;
        }
    }
}
