namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 特效标识。<b>编译期检查</b>是它存在的全部理由——拼错会编译不过。
    /// </summary>
    /// <remarks>
    /// <para><b>None = 0 是哨兵值</b>：<c>Play(EffectId.None, ctx)</c> 静默返回 <c>EffectHandle.None</c>，
    /// 不报错、不查表。调用方写 <c>EffectId.None</c> 表示「这次不播」。</para>
    /// <para><b>新增一个特效要动三处</b>（顺序固定）：
    /// ① 本枚举加一项；② <c>EffectCatalog</c> 加一行（驱动种类 / 是否单例 / 池上限 / 预热数）；
    /// ③ 需要资源的驱动才要预制体 <c>Assets/Resources/effects/&lt;枚举名&gt;.prefab</c>。
    /// 只做①不做②时 <c>Play</c> 会打一条 LogError 并返回 None（这就是「未注册」的可见形态）。</para>
    /// <para><b>程序生成的驱动不需要预制体</b>：它在 <c>EffectCatalog</c> 里声明
    /// <c>EffectDriverKind</c>，由 <c>EffectDriverFactory</c> 造出来，
    /// <c>AssetKey</c> 返回空串 ⇒ <c>EffectModule</c> 完全不碰 <c>AssetModule</c>。
    /// <see cref="LandingRing"/> 与 <see cref="EnemyShatter"/> 就是这一类：
    /// 它们的形状（贴地环 / 扇形碎片）是逐像素算出来的，导入美术反而要多一份资产。</para>
    /// <para><b>未实现驱动的项</b>：<see cref="EnemyFlashWhite"/> / <see cref="ObjectShake"/> / <see cref="ScreenShake"/>
    /// 目前<b>故意不在</b> <c>EffectCatalog</c> 里——它们的驱动（材质闪白 / 位移震动 / 相机震动）由用户按需实现，
    /// 实现后加一行 Catalog 即可接入，不需要动 <c>EffectModule</c>。</para>
    /// </remarks>
    public enum EffectId
    {
        /// <summary>空 / 无特效（哨兵值）。</summary>
        None = 0,

        /// <summary>打击火花。命中敌人时在命中点播。</summary>
        HitSpark,

        /// <summary>敌人白色闪白（待实现驱动：材质 / 颜色动画）。</summary>
        EnemyFlashWhite,

        /// <summary>单个物品震动（血条抖动、受击震动等，或以此为基。待实现驱动）。</summary>
        ObjectShake,

        /// <summary>屏幕震动（待实现驱动：相机震动；合并策略为「取最大强度 + 重置计时」）。</summary>
        ScreenShake,

        /// <summary>泥浆飞溅。水球落地时在落点播。</summary>
        MudSplash,

        /// <summary>
        /// 落地环：在落点画一个从满尺寸缩到消失的贴地圆环，半径与颜色由 <c>EffectContext</c> 给。
        /// </summary>
        /// <remarks>
        /// <b>它不是特效，是仪表。</b>落地的作用范围在屏幕上原本不可见 —— 没有这个圈，
        /// 试冲量强度就只能靠反复猜。画出来的半径就是那件事的实际生效范围，所以半径是<b>参数</b>
        /// （<c>ctx.Radius</c>）而不是驱动里的常量。
        /// </remarks>
        LandingRing,

        /// <summary>敌人碎裂：耐久归零时飞出的几块碎片，沿 <c>ctx.Direction</c> 扇形散开。</summary>
        EnemyShatter,
    }
}
