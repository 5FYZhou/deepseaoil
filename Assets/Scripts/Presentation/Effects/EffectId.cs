namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 特效标识。<b>编译期检查</b>是它存在的全部理由——拼错会编译不过。
    /// </summary>
    /// <remarks>
    /// <para><b>None = 0 是哨兵值</b>：<c>Play(EffectId.None, ctx)</c> 静默返回 <c>EffectHandle.None</c>，
    /// 不报错、不查表。调用方写 <c>EffectId.None</c> 表示「这次不播」。</para>
    /// <para><b>词条只放"通用性"的东西</b>（审查已定）：名字里不绑"敌人"或"格子" ——
    /// 谁用它、用什么颜色都由消费者通过 <c>EffectContext.Tint</c> 给出。
    /// 于是"敌人闪白"与"血条抖动"可以是同一个 <see cref="Flash"/>，"瞄准格高亮"与
    /// "危险区提示"可以是同一个 <see cref="Highlight"/>。</para>
    /// <para><b>新增一个特效要动三处</b>（顺序固定）：
    /// ① 本枚举加一项；② <c>EffectCatalog</c> 加一行（驱动种类 / 是否单例 / 池上限 / 预热数）；
    /// ③ 需要资源的驱动才要预制体 <c>Assets/Resources/effects/&lt;枚举名&gt;.prefab</c>。
    /// 只做①不做②时 <c>Play</c> 会打一条 LogError 并返回 None（这就是「未注册」的可见形态）。</para>
    /// <para><b>程序生成的驱动不需要预制体</b>：它在 <c>EffectCatalog</c> 里声明
    /// <c>EffectDriverKind</c>，由 <c>EffectDriverFactory</c> 造出来，
    /// <c>AssetKey</c> 返回空串 ⇒ <c>EffectModule</c> 完全不碰 <c>AssetModule</c>。
    /// <see cref="Shatter"/> 与 <see cref="Highlight"/> 就是这一类：
    /// 它们的形状（扇形碎片 / 整格色块）是逐像素算出来的，导入美术反而要多一份资产。</para>
    /// <para><b>未实现驱动的项</b>：<see cref="Flash"/> / <see cref="Shake"/> / <see cref="ScreenShake"/>
    /// 目前<b>故意不在</b> <c>EffectCatalog</c> 里——它们的驱动（材质闪白 / 位移震动 / 相机震动）由用户按需实现，
    /// 实现后加一行 Catalog 即可接入，不需要动 <c>EffectModule</c>。</para>
    /// </remarks>
    public enum EffectId
    {
        /// <summary>空 / 无特效（哨兵值）。</summary>
        None = 0,

        /// <summary>爆发火花。命中时在命中点播。</summary>
        BurstSparks,

        /// <summary>闪烁（待实现驱动：材质 / 颜色动画）。<b>颜色由消费者给</b>（<c>ctx.Tint</c>）。</summary>
        Flash,

        /// <summary>震动（血条抖动、受击震动等，或以此为基。待实现驱动）。</summary>
        Shake,

        /// <summary>屏幕震动（待实现驱动：相机震动；合并策略为「取最大强度 + 重置计时」）。</summary>
        ScreenShake,

        /// <summary>泥浆飞溅。水球落地时在落点播。</summary>
        MudSplash,

        /// <summary>碎裂：耐久归零时飞出的几块碎片，沿 <c>ctx.Direction</c> 扇形散开。</summary>
        Shatter,

        /// <summary>
        /// 持续性高亮：跟着"逻辑层发布的那一格"走，可更新、可停止。<b>颜色由消费者给</b>（<c>ctx.Tint</c>）。
        /// </summary>
        /// <remarks>
        /// <b>它是持续效果口的第一个消费者</b>（<c>EffectModule.Update</c>）：创建一次，
        /// 之后每帧只更新位置与颜色 —— 每帧 <c>Play</c> 一次等于每帧新建一个实例，
        /// 而"每帧新建"正是这个能力被加进来的原因。
        /// <para>高亮的资产（描边图 / 材质）在正式美术阶段由这个驱动自持，现在用运行期图元顶替
        /// （见 <c>HighlightDriver</c> 的说明）。</para>
        /// </remarks>
        Highlight,
    }
}
