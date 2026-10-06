using DeepseaOil.Foundation;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 场上可渲染件的排序层。<b>渲染约定，留在代码里</b>（不进 Luban，也不进 SO）。
    /// </summary>
    /// <remarks>
    /// <b>为什么整条链写在一个文件里：</b>排序层是"谁压谁"的全序关系，散在各个视效件里就没人能回答
    /// "这两个谁在上面"。集中之后，调顺序是改一个常量并顺手检查相邻两项。
    /// <para><b>Y-Sort 频带（500..559）：</b>玩家 / 敌人 / 障碍物 / 球按世界 y 在频带内取档 ——
    /// 越靠下越晚画，于是"站在前面的人挡住后面的人"。档位换算在地基（<see cref="YSort"/>），
    /// 频带与档数在这里。<b>频带宽度是硬约束</b>：档数 ＝ 每单位档数 × 覆盖的世界单位数。</para>
    /// <para><b>刻意不进频带的：</b><see cref="GroundShadow"/>（贴地阴影与指示器：它们贴地，
    /// 跟着 y 变会把阴影排到角色前面）、<see cref="ActorOverlay"/>（头顶读数）、
    /// <see cref="Aim"/>（瞄准反馈，它是地面标记）、<see cref="ShatterPiece"/>（碎片）。</para>
    /// <para><b>为什么用 sortingOrder 而不是 z：</b>俯视角正交相机下所有件都是 z=0，深度分不出先后；
    /// 而 <c>MeshRenderer</c>（头顶数字走它）与 <c>SpriteRenderer</c> <b>都有</b> <c>sortingOrder</c> ——
    /// 这一点白模期间曾经判断错，结果是数字被自己的身体挡住。</para>
    /// <para>层内先后由 <c>sortingOrder</c> 决定；<b>用 layer 0（Default）</b>：
    /// 不为视觉件去改 <c>TagManager.asset</c>，那会变成一处需要人工同步的工程设置。</para>
    /// </remarks>
    public static class RenderOrder
    {
        /// <summary>
        /// 场景地面的装饰层（100）。<b>本工程没有代码使用它</b>：地面美术由场景自己摆。
        /// 它在这里是为了让"谁压谁"这条链完整可读 —— 视觉件的最低一档从 100 起。
        /// </summary>
        public const int Ground = 100;

        /// <summary>
        /// 格子效果的贴地覆盖层（300）。<b>本工程没有代码使用它</b>：
        /// 状态贴图由 <c>TilemapAdapter</c> 直接改 Tilemap，而 <c>TilemapRenderer</c> 的排序由那一层的
        /// Inspector 设置决定（不由代码改 —— 改它等于抢走美术的层设置）。
        /// 它是"格效果层应当落在哪一档"的书面约定。
        /// </summary>
        public const int TileEffect = 300;

        /// <summary>
        /// 贴地件的固定层（310）：球的阴影、掉落物的影子、贴地指示物。
        /// </summary>
        /// <remarks><b>它们不参与 Y-Sort</b>：阴影是"这个东西离地多高"的读数，贴着地面走；
        /// 跟着 y 取档会让它随高度越过自己的主人（球在天上时阴影排到球前面）。</remarks>
        public const int GroundShadow = 310;

        /// <summary>
        /// 瞄准反馈层（320）：格的瞄准高亮。
        /// </summary>
        /// <remarks>它是<b>地面标记</b>：压着格效果（<see cref="TileEffect"/>），但被站在那一格上的人盖住 ——
        /// 收口前它在 900（盖在角色之上），那是"它与角色谁先画"没有约定时的临时值。</remarks>
        public const int Aim = 320;

        /// <summary>Y-Sort 频带下沿（最远）。</summary>
        public const int YSortBandStart = 500;

        /// <summary>Y-Sort 频带上沿（最近）。</summary>
        public const int YSortBandEnd = 559;

        /// <summary>
        /// 每世界单位几档（0.25 米一档）。
        /// </summary>
        /// <remarks>频带宽度 60 档 ÷ 4 档/单位 ＝ 覆盖 15 个世界单位；超出部分被钳在频带两端
        /// （见 <see cref="YSort.OrderFor"/>）。调它必须同时看这个除法。</remarks>
        public const float YSortLevelsPerUnit = 4f;

        /// <summary>头顶读数层（耐久数字）。高于频带：数字不该被邻居的身体盖住。</summary>
        public const int ActorOverlay = 560;

        /// <summary>落地瞬闪层。比频带高一层，落地那一下才不会被球本体盖住。</summary>
        public const int LandingRing = 1100;

        /// <summary>碎片层。比球还高：碎片是"这一帧发生了什么"的最高优先级读数。</summary>
        public const int ShatterPiece = 1200;

        /// <summary>视觉件使用的 Unity layer（0 = Default）。</summary>
        public const int OverlayLayer = 0;

        /// <summary>角色 / 敌人 / 障碍物的档位（按世界 y）。</summary>
        public static int ActorOrder(float y)
        {
            return YSort.OrderFor(y, YSortBandStart, YSortBandEnd, YSortLevelsPerUnit);
        }

        /// <summary>
        /// 球的档位：<b>与角色同一频带</b>（审查已定：球参与 Y-Sort）。
        /// </summary>
        /// <remarks>取的是球的<b>贴地位置</b>的 y（不是它在弧线上的视觉高度）：
        /// 排序回答"它落在场上的哪里"，高度是画出来的偏移。</remarks>
        public static int BallOrder(float groundY)
        {
            return ActorOrder(groundY);
        }
    }
}
