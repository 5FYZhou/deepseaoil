using System.Collections.Generic;
using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 单格状态机：只管切换与回调（Tick 调度在 <c>GridLogic</c> 与队列里）；切换立即、同步、同帧生效。
    /// 未注册 id（含 <see cref="TileStateType.Normal"/>）造不出实例、CurrentId 读作 Normal；每次进入造新实例而非共享原型 —— 共享会让两格计时器合一、一起消失且不报错。
    /// </summary>
    public sealed class TileStateMachine
    {
        private readonly Dictionary<TileStateType, System.Func<ITileState>> _factories = new();

        private ITileState _current;

        public TileStateType CurrentId => _current != null ? _current.Id : TileStateType.Normal;

        public ITileState Current => _current;

<<<<<<< HEAD
=======
        /// <summary>注册一个状态的工厂。<see cref="TileStateType.Normal"/> 不需要注册。</summary>
>>>>>>> main
        public void Register(TileStateType id, System.Func<ITileState> factory)
        {
            if (factory == null) return;

            _factories[id] = factory;
        }

        /// <summary>
        /// 已经是该状态时不重入、不重置（反复投水球不会刷新泥浆计时），返回 <c>false</c>。
        /// </summary>
        public bool SwitchTo(TileStateType next, in TileContext ctx)
        {
            if (CurrentId == next) return false;

            _current?.OnExit(in ctx);

            _current = Create(next);

            _current?.OnEnter(in ctx);

            return true;
        }

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
