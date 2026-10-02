using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 敌人的视效决策（<b>纯函数</b>）：这一帧身体该是什么颜色、头顶数字该写什么。
    /// </summary>
    /// <remarks>
    /// <b>为什么把颜色拿出来做纯函数：</b>颜色是"减速生效了没有"和"这一下打中了没有"的唯一反馈，
    /// 而白模没有 buff 图标也没有粒子。留在 <c>EnemyActor</c> 里它就只能靠眼睛验；
    /// 提到这里之后，"深色必须比原色暗""闪烁色必须比原色亮"这些是能断言的。
    /// <para><b>四态显式写出来，不做乘法或插值。</b>有人会想写
    /// <c>Color.Lerp(BODY, SLOW, t)</c> 或 <c>body * 0.4f</c> 省几个常量 ——
    /// 那两种写法都让"这一帧到底该是什么色"变成一个算出来的数，既没法断言也没法单独调。
    /// 而"减速时颜色变深"这条要求本身就是一个设计决定，它值得有自己的常量。</para>
    /// </remarks>
    public static class EnemyVisual
    {
        /// <summary>被砸中时闪的亮色（比身体色亮，但仍是暖色，不像换了个敌人）。</summary>
        public static readonly Color FlashColor = new Color(1f, 0.92f, 0.90f, 1f);

        /// <summary>踩在泥浆里、且正在闪。<b>对外可见是为了让"四态两两不同"这条能被断言。</b></summary>
        public static readonly Color SlowColor = new Color(0.62f, 0.42f, 0.40f, 1f);

        /// <summary>
        /// 这一帧的身体颜色。
        /// </summary>
        /// <param name="slowMultiplier">本帧实际喂给逻辑层的泥浆系数（<c>&lt; 1</c> 表示被减速）。</param>
        /// <param name="flashOn">闪烁相位的亮半周。</param>
        /// <returns>四态之一：正常红 / 泥浆深红 / 闪烁亮色 / 泥浆+闪烁。</returns>
        /// <remarks>
        /// <b>四态而不是"二选一"：</b>减速与受击是两个独立的 debuff，同时发生时两种反馈都要在。
        /// 写成 <c>if (flash) 亮 else if (slow) 深</c> 的话，站在泥里被打中的敌人看起来"跟没中泥一样"，
        /// 而玩家正好会在这时候判断"这泥到底有没有用"。
        /// <para>判据用"是否小于 1"而不是"是否等于某个具体系数"：泥浆将来分等级（大水球更黏）时，
        /// 这里不需要跟着改。</para>
        /// </remarks>
        public static Color BodyColor(float slowMultiplier, bool flashOn)
        {
            bool slowed = slowMultiplier < 1f;

            if (slowed) return flashOn ? SlowColor : ThrowConstants.ENEMY_SLOW_BODY_COLOR;

            return flashOn ? FlashColor : ThrowConstants.ENEMY_BODY_COLOR;
        }

        /// <summary>
        /// 头顶的剩余耐久数字。
        /// </summary>
        /// <param name="hp">剩余耐久。</param>
        /// <returns>要写进 <c>TextMesh</c> 的字符串；<b>耐久为 0 时是空串</b>。</returns>
        /// <remarks>
        /// 耐久为 0 时不写 <c>"0"</c>：那一帧敌人正在碎裂，显示一个 0 只会让人以为
        /// "它还有 0 点血所以在场上" —— 空串至少不撒谎。
        /// <para>不做"血条""满/空符号"这类花活：需求是"直接写剩余耐久的数字"。</para>
        /// </remarks>
        public static string HpText(int hp)
        {
            return hp > 0 ? hp.ToString() : string.Empty;
        }

        /// <summary>
        /// 这一帧该不该亮。闪烁是<b>相位</b>而不是状态：只用一个布尔字段会在暂停、
        /// 掉帧、以及"禁足时长改了但闪烁频率没改"三种情况下偷偷不同步。
        /// </summary>
        /// <param name="time">当前时间（秒，用 <c>Time.time</c>，不累加）。</param>
        /// <returns>亮半周为 <c>true</c>。</returns>
        public static bool IsFlashOn(float time)
        {
            // 用 Sin 而不是 (time * hz) % 1 取整：取整在 hz 为 0 时会除零，
            // 而 Sin 在频率为 0 时恒为 0（整段禁足都亮），是"能看出不对但不崩"的行为。
            return Mathf.Sin(time * 2f * Mathf.PI * ThrowConstants.ENEMY_FLASH_HZ) > 0f;
        }
    }
}
