using System.Collections.Generic;
using cfg.demo;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 单格状态机：只管切换与回调（Tick 调度在 <c>GridLogic</c> 与队列里）；切换立即、同步、同帧生效。
    /// 未注册 id（含 <see cref="TileStateType.Normal"/>）造不出实例、CurrentId 读作 Normal；每次进入造新实例而非共享原型 —— 共享会让两格计时器合一、一起消失且不报错。
    /// </summary>
    /// <remarks>
    /// <b>它不认识效果、也不认识元素</b>：效果清单的提交在状态实现里（<c>TableTileState</c>），元素的读写由 <c>GridLogic</c> 与元素层负责。
    /// </remarks>
    public sealed class TileStateMachine
    {
        private readonly Dictionary<TileStateType, System.Func<ITileState>> _factories = new();

        private ITileState _current;

        public TileStateType CurrentId => _current != null ? _current.Id : TileStateType.Normal;

        public ITileState Current => _current;

        /// <summary>注册一个状态的工厂。<see cref="TileStateType.Normal"/> 不需要注册。</summary>
        public void Register(TileStateType id, System.Func<ITileState> factory)
        {
            if (factory == null) return;

            _factories[id] = factory;
        }

        /// <summary>
        /// 已经是该状态时不重入、不重置（反复投水球不会刷新泥浆计时），返回 <c>false</c>。
        /// </summary>
        /// <remarks><b>造不出实例时也返回 <c>false</c>，且不动 <see cref="_current"/></b>：空状态机不算一次切换。
        /// 曾经的写法是"先换、后判、恒返回 true"，于是"切到一个没有实现的状态"会被上层当成切换成功 —— 上层照常发事件、刷元素、把机器存回去，
        /// 而这一格的状态读回来仍是常规：<b>反应发生了、格子却是空的</b>，且没有任何报错。</remarks>
        public bool SwitchTo(TileStateType next, in TileContext ctx)
        {
            if (CurrentId == next) return false;

            ITileState created;

            try
            {
                created = Create(next);
            }
            catch (System.Exception e)
            {
                // 工厂抛异常不能把状态机弄成「旧的已经走了、新的没来」：那正是本方法要防的半途状态。
                UnityEngine.Debug.LogError($"[Grid] 造状态 {next} 的实例时抛异常，本次切换作废：{e}");

                created = null;
            }

            if (created == null) return false;

            _current?.OnExit(in ctx);

            _current = created;

            _current.OnEnter(in ctx);

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
