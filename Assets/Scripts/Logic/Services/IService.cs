namespace DeepseaOil.Logic.Service
{
    /// <summary>进程级服务：由 <c>GameRoot</c> 装配并按"注册序 = Init 序 = Tick 序"驱动，拆除按反序。</summary>
    /// <remarks>
    /// <b>时间一律由驱动方给，服务永不自己读 <c>Time</c></b>：口径只有一处（<c>GameRoot</c> 的顺序表），于是"暂停时哪些东西还在走"是一个看得见的决定，而不是散在每个人自己调 <c>Time.unscaledDeltaTime</c> 的地方。
    /// <para>两个 delta 都要给：只需要 unscaled 的服务（暂停计时）与两个都要的服务（双时间轴计时器）各取所需，接口不必为第二种情况再开一条通道。</para>
    /// </remarks>
    public interface IService
    {
        void Init();

        /// <param name="deltaTime">缩放后的帧时长（暂停时为 0）。</param>
        /// <param name="unscaledDeltaTime">未缩放的帧时长（暂停时照走）。</param>
        void Tick(float deltaTime, float unscaledDeltaTime);

        void Dispose();
    }
}
