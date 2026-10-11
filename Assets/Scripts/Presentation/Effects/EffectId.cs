namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>None=0 是哨兵值，Play(EffectId.None) 静默返回 EffectHandle.None，表示不播。</remarks>
    public enum EffectId
    {
        None = 0,

        BurstSparks,

        Flash,

        Shake,

        ScreenShake,

        MudSplash,

        Shatter,

        Highlight,

        SteamBlast,

        ChainShock,

        FlameEmbers,
    }
}
