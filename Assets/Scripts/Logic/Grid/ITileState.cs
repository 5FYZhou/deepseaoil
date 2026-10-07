using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 一个格子状态：进 / 出 / 每次 Tick 三个钩子。<b>状态自己决定要不要继续 Tick</b>。
    /// </summary>
    /// <remarks>
    /// <b>Tick 请求是一次性的</b>（见 <see cref="TileTickQueue"/>）：被消费一次就消失，
    /// 想继续收就得在 <see cref="OnTick"/> 里再提交一次。于是：
    /// <list type="bullet">
    /// <item>只结算一次 → 在 <see cref="OnEnter"/> 里提交一次即可；</item>
    /// <item>周期结算 → 在 <see cref="OnTick"/> 末尾再提交一次；</item>
    /// <item>不需要 Tick → 都不提交（减速这类"被查询的修正值"属于这一档）。</item>
    /// </list>
    /// <para><b>实现类必须无参可测：</b>状态是纯 C#，不许碰 <c>MonoBehaviour</c> / <c>Time</c> / <c>Physics2D</c>；
    /// 时间与提交口都在 <see cref="TileContext"/> 里。</para>
    /// <para><b>实现类的实例是"每格一份"</b>：<see cref="TileStateMachine"/> 每次进入状态都调工厂造一个新的，
    /// 所以状态可以把"已经持续了多久"这种每格独立的东西放在自己的字段里 ——
    /// 共享一个原型实例会让全场格子共用一个计时器（而且不报错，只是"泥浆一起消失"）。</para>
    /// </remarks>
    public interface ITileState
    {
        /// <summary>本状态对应的配置 ID。</summary>
        TileStateType Id { get; }

        /// <summary>进入本格状态时调用一次。初次转换与"从别的状态切过来"走同一条路。</summary>
        void OnEnter(in TileContext ctx);

        /// <summary>每次被调度到 Tick 时调用一次。</summary>
        void OnTick(in TileContext ctx);

        /// <summary>离开本格状态时调用一次（切到别的状态、或落回 Normal）。</summary>
        void OnExit(in TileContext ctx);
    }
}
