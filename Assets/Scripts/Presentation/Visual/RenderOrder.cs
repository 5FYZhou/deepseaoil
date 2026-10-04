namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 场上可渲染件的排序层。<b>渲染约定，留在代码里</b>（不进 Luban，也不进 SO）。
    /// </summary>
    /// <remarks>
    /// <b>为什么整条链写在一个文件里：</b>排序层是"谁压谁"的全序关系，散在各个视效件里就没人能回答
    /// "这两个谁在上面"。集中之后，调顺序是改一个常量并顺手检查相邻两项。
    /// <para><b>为什么用 sortingOrder 而不是 z：</b>俯视角正交相机下所有件都是 z=0，
    /// 深度分不出先后；而 <c>MeshRenderer</c>（头顶数字走它）与 <c>SpriteRenderer</c>
    /// <b>都有</b> <c>sortingOrder</c> —— 这一点白模期间曾经判断错，结果是数字被自己的身体挡住。</para>
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
        /// 状态贴图由 <c>GridView</c> 直接改 Tilemap，而 <c>TilemapRenderer</c> 的排序由那一层的
        /// Inspector 设置决定（不由代码改 —— 改它等于抢走美术的层设置）。
        /// 它是"效果层应当落在哪一档"的书面约定。
        /// </summary>
        public const int TileEffect = 300;

        /// <summary>角色身体层（玩家、敌人）。</summary>
        public const int Actor = 500;

        /// <summary>头顶读数层（耐久数字）。高于身体与玩家，低于瞄准环。</summary>
        public const int ActorOverlay = 560;

        /// <summary>瞄准反馈层。必须低于球。</summary>
        public const int Aim = 900;

        /// <summary>球本体层。取 1000 是为了盖过一切场景装饰。</summary>
        public const int Ball = 1000;

        /// <summary>球阴影：比球本体低 1，仍在装饰之上。</summary>
        public const int BallShadow = Ball - 1;

        /// <summary>落地瞬闪层。比球再高一层，落地那一下才不会被球本体盖住。</summary>
        public const int LandingRing = 1100;

        /// <summary>碎片层。比球还高：碎片是"这一帧发生了什么"的最高优先级读数。</summary>
        public const int ShatterPiece = 1200;

        /// <summary>视觉件使用的 Unity layer（0 = Default）。</summary>
        public const int OverlayLayer = 0;
    }
}
