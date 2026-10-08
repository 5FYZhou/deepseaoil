namespace DeepseaOil.Logic.Service
{
    /// <summary>由 GameRoot 按"注册序=Init序=Tick序"驱动，拆除按反序</summary>
    /// <remarks>时间一律由驱动方给，服务不自己读 Time（口径只在 GameRoot 顺序表）；两个 delta 都给，各取所需</remarks>
    public interface IService
    {
        void Init();

        /// <summary>deltaTime=缩放帧时长（暂停为0），unscaledDeltaTime=未缩放（暂停照走）</summary>
        void Tick(float deltaTime, float unscaledDeltaTime);

        void Dispose();
    }
}
