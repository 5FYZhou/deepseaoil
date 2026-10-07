using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一个格子状态：进 / 出 / 每次 Tick 三个钩子。状态自己决定要不要继续 Tick。</summary>
    /// <remarks>
    /// Tick 请求是一次性的（见 <see cref="TileTickQueue"/>）：消费一次就消失，想继续得在 <see cref="OnTick"/> 里再提交。
    /// 只结算一次 → 在 <see cref="OnEnter"/> 里提交一次；周期结算 → 在 OnTick 末尾再提交；不需要 Tick → 都不提交（纯装饰、或只在进入那一刻起一次作用的状态属于这一档）。
    /// 实现类是"每格一份"（<see cref="TileStateMachine"/> 每次进入状态都调工厂造一个新的）：共享一个原型实例会让全场格子共用一个计时器，而且不报错，只表现为"泥浆一起消失"。
    /// 实现必须无参可测：纯 C#，不许碰 <c>MonoBehaviour</c> / <c>Time</c> / <c>Physics2D</c>，时间与提交口都在 <see cref="TileContext"/> 里。
    /// </remarks>
    public interface ITileState
    {
        TileStateType Id { get; }

        void OnEnter(in TileContext ctx);

        void OnTick(in TileContext ctx);

        void OnExit(in TileContext ctx);
    }
}
