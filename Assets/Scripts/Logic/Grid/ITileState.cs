using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一个格子状态：进 / 出 / 每次 Tick 三个钩子。状态自己决定要不要继续 Tick。</summary>
    /// <remarks>
    /// Tick 请求是一次性的（见 <see cref="TileTickQueue"/>）：消费一次就消失，想继续得在 <see cref="OnTick"/> 里再提交。
    /// 只结算一次 → 在 <see cref="OnEnter"/> 里提交一次；周期结算 → 在 OnTick 末尾再提交；不需要 Tick → 都不提交（纯装饰、或只在进入那一刻起一次作用的状态属于这一档）。
    /// <para><b>效果只能"提交"、不能自己施加</b>：状态拿不出执行者，也不该去拿 —— 找目标、算方向、判死活都在结算口（<see cref="ITileResolver"/>）那一侧。
    /// 于是"泥浆减速玩家"这条行为不可能存在：玩家不在归属表里，状态连知道玩家存在的机会都没有（D7）。</para>
    /// <para><b>状态不持有自己的效果清单</b>：清单来自 <c>TileStateSpec</c>（数据层已在构造期把档位解析好），状态只负责按节拍把它提交出去。</para>
    /// <para>实现类是"每格一份"（<see cref="TileStateMachine"/> 每次进入状态都调工厂造一个新的）：共享一个原型实例会让全场格子共用一个计时器，而且不报错，只表现为"泥浆一起消失"。</para>
    /// <para>实现必须无参可测：纯 C#，不许碰 <c>MonoBehaviour</c> / <c>Time</c> / <c>Physics2D</c>，时间与提交口都在 <see cref="TileContext"/> 里。</para>
    /// </remarks>
    public interface ITileState
    {
        TileStateType Id { get; }

        /// <summary>进入本格状态时调用一次。初次转换与"从别的状态切过来"走同一条路。</summary>
        void OnEnter(in TileContext ctx);

        void OnTick(in TileContext ctx);

        void OnExit(in TileContext ctx);
    }
}
