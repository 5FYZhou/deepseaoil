using cfg.dso;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一个格子状态：进/出/每次 Tick 三个钩子，状态自己决定要不要继续 Tick</summary>
    /// <remarks>Tick 请求一次性，消费一次就消失，想继续得在 OnTick 里再提交。效果只能提交、不能自己施加，找目标/算方向/判死活都在结算口 ITileResolver 那一侧。状态不持有自己的效果清单，清单来自 TileStateSpec，数据层已在构造期解析好档位。实现类每格一份，工厂每次进入状态都造新实例。实现必须纯 C#，不许碰 MonoBehaviour/Time/Physics2D，时间与提交口都在 TileContext 里。</remarks>
    public interface ITileState
    {
        TileStateType Id { get; }

        /// <summary>进入本格状态时调用一次，初次转换与从别的状态切来同路</summary>
        void OnEnter(in TileContext ctx);

        void OnTick(in TileContext ctx);

        void OnExit(in TileContext ctx);
    }
}
