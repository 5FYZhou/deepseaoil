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
        /// <summary>请求切到某个状态；是否真的切由实现方判定（同状态是 no-op）。</summary>
        void RequestState(GameState state);
    }

    /// <summary>
    /// UI 输入逻辑：输入由表现层采样进 <c>UILogicContext</c>，本类由 <c>GameRoot</c> 每渲染帧驱动一次、消费该快照。
    /// 帧内"当前状态"的唯一来源是本帧上下文；Esc 优先级链：先关最上层面板，关不掉才请求切状态。
    /// </summary>
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
