using System.Collections.Generic;
using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 单格的格子状态机：<b>只管切换与回调</b>，不管 Tick 调度（那在 <c>GridLogic</c> 与队列里）。
    /// </summary>
    /// <remarks>
    /// <b>切换语义是"立即 Exit → 造新实例 → Enter"</b>，与 <c>MoveGroup</c> 的状态机同一套：
    /// 立即、同步、同帧生效，没有"下一帧才切"的中间态。
    /// <para><b>每次进入都造新实例（工厂），而不是共享原型：</b>状态把每格独立的量（已持续多久）
    /// 放在自己字段里。共享一份会让"第一格变泥浆"和"第二格变泥浆"共用一个计时器 ——
    /// 现象是"两片泥浆一起消失"，不报错，极难倒推。</para>
    /// <para><b><see cref="TileStateType.Normal"/> 是"没有状态"</b>：不注册工厂、<c>_current</c> 为 null。
    /// 这样 <c>GridLogic</c> 可以把落回 Normal 的格从字典里删掉 —— 常规格不占任何常驻内存。</para>
    /// </remarks>
    public sealed class TileStateMachine
    {
        private readonly Dictionary<TileStateType, System.Func<ITileState>> _factories = new();

        private ITileState _current;

        /// <summary>当前状态 ID；没有状态时是 <see cref="TileStateType.Normal"/>。</summary>
        public TileStateType CurrentId => _current != null ? _current.Id : TileStateType.Normal;

        /// <summary>当前状态实例；没有状态时为 <c>null</c>。</summary>
        public ITileState Current => _current;

        /// <summary>注册一个状态的工厂。<see cref="TileStateType.Normal"/> 不需要注册。</summary>
        public void Register(TileStateType id, System.Func<ITileState> factory)
        {
            if (factory == null) return;

            _factories[id] = factory;
        }

        /// <summary>
        /// 切换到 <paramref name="next"/>。已经是该状态时不重入、不重置 —— 返回 <c>false</c>。
        /// </summary>
        /// <returns>真的发生了切换为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>"同状态不重入"是有后果的：</b>它意味着反复对同一格投水球不会刷新泥浆计时
        /// （白模的行为也是如此）。要刷新就得先切走再切回，那是另一套语义。
        /// </remarks>
        public bool SwitchTo(TileStateType next, in TileContext ctx)
        {
            if (CurrentId == next) return false;

            _current?.OnExit(in ctx);

            _current = Create(next);

            _current?.OnEnter(in ctx);

            return true;
        }

        /// <summary>推进一次。</summary>
        public void Tick(in TileContext ctx)
        {
            _current?.OnTick(in ctx);
        }

        private ITileState Create(TileStateType id)
        {
            if (!_factories.TryGetValue(id, out System.Func<ITileState> factory) || factory == null) return null;

            return factory();
        }
    }
}
