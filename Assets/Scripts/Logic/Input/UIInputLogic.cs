using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    public interface IUIOperation
    {
        bool TryCloseTopmostPanel();
        void OpenPausePanel();
        void OpenExitConfirmPanel();
    }

    public interface IUIStateRequest
    {
        /// <summary>请求切到某状态，是否真切由实现方判定</summary>
        void RequestState(GameState state);
    }

    /// <summary>GameRoot 每渲染帧驱动一次；Esc 优先链：先关顶层，关不掉才请求切状态</summary>
    public sealed class UIInputLogic : ITickable
    {
        private readonly IUIOperation _uiMgr;
        private readonly IUIStateRequest _states;

        public UIInputLogic(IUIOperation uiMgr, IUIStateRequest states)
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

                case GameState.Menu:
                    _states.RequestState(GameState.BeforeExit);
                    return;

                case GameState.Paused:
                    _states.RequestState(GameState.Running);
                    return;
            }
        }
    }
}
