namespace DeepseaOil.Presentation.Effects
{
    /// <remarks><c>None = 0</c> 是哨兵值：<c>Play(EffectId.None, ctx)</c> 静默返回 <c>EffectHandle.None</c>，不报错、不查表；调用方写它表示「这次不播」。
    /// 新增特效要动三处（顺序固定）：① 本枚举加一项；② <c>EffectCatalog</c> 加一行（驱动种类 / 是否单例 / 池上限 / 预热数）；③ 需要资源的驱动才要预制体。只做①不做②时 <c>Play</c> 会打一条 LogError 并返回 None —— 这就是「未注册」的可见形态。</remarks>
    public enum EffectId
    {
        None = 0,

        BurstSparks,

        Flash,

        Shake,

        ScreenShake,

        MudSplash,

        Shatter,

        /// <summary>持续性高亮：跟着"逻辑层发布的那一格"走，可更新、可停止；<b>颜色由消费者给</b>（<c>ctx.Tint</c>）。这是持续效果口的入口（<c>EffectModule.Update</c>）—— <b>每帧 <c>Play</c> 一次等于每帧新建一个实例</b>，持续型要创建一次、之后只更新位置与颜色。</summary>
        Highlight,
    }
}
