using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    public interface IUIOperation
    {
        bool TryCloseTopmostPanel();
    }

    /// <summary>GameRoot 每渲染帧驱动一次；Esc 优先链：先关顶层，关不掉才请求切状态</summary>
    public sealed class UIInputLogic : ITickable
    {
        private readonly IUIOperation _uiMgr;
        private readonly IGameStateRequest _states;

        public UIInputLogic(IUIOperation uiMgr, IGameStateRequest states)
        {
            _uiMgr = uiMgr;
            _states = states;
        }

        public void Tick(UILogicContext ctx)
        {
            if (!ctx.inputSnapshot.EscPressed) return;

            if (_uiMgr.TryCloseTopmostPanel()) return;

            switch (ctx.gameState)
            {
                case GameState.Running:
                    _states.RequestState(GameState.Paused);
                    return;

                case GameState.Paused:
                    _states.RequestState(GameState.Running);
                    return;
            }
        }
    }
}
