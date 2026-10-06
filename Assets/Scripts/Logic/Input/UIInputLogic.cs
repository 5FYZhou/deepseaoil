using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    /// <summary>
    /// 面板操作口：逻辑层（<c>UIInputLogic</c>）按 Esc 时能用到的三件事。
    /// </summary>
    /// <remarks>实现方是表现层的 <c>UIMgr</c> —— 依赖方向永远是「端口 ← 实现」。</remarks>
    public interface IUIOperation
    {
        bool TryCloseTopmostPanel();
        void OpenPausePanel();
        void OpenExitConfirmPanel();
    }

    /// <summary>
    /// 状态请求口：逻辑层只能"请求"切游戏状态，不能直接改它。
    /// </summary>
    /// <remarks>
    /// <b>为什么不做成"逻辑层直接调 GameManager"：</b><c>GameManager</c> 在表现层
    /// （要切面板、要发暂停意图），逻辑层引用它就成了反向依赖。
    /// <para><b>为什么"读状态"不在本接口里：</b>当前状态已经由 <c>UILogicContext</c> 逐帧喂进来了 ——
    /// 收口前 <c>UIInputLogic</c> 一边读上下文、一边又去读单例，
    /// 同一帧的"当前状态"存在两个来源。现在只有一个。</para>
    /// </remarks>
    public interface IUIStateRequest
    {
        /// <summary>请求切到某个状态；是否真的切由实现方判定（同状态是 no-op）。</summary>
        void RequestState(GameState state);
    }

    /// <summary>
    /// UI 输入逻辑：由 <c>GameRoot</c> 每渲染帧驱动一次，消费 UI 输入快照。
    /// </summary>
    /// <remarks>
    /// 它只做一件事：Esc 的优先级链 —— 先尝试关掉最上层面板，关不掉才切游戏状态。
    /// <b>它不认识 <c>GameManager</c></b>：面板操作与状态请求都是构造时注入的接口。
    /// </remarks>
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
            // 如果按Esc
            if (!ctx.inputSnapshot.EscPressed) return;

            // 1. 尝试关闭最上层的面板（除开始面板）
            if (_uiMgr.TryCloseTopmostPanel()) return;

            // 2. 面板关不掉，才切游戏状态。当前状态取自本帧上下文（唯一来源）
            switch (ctx.gameState)
            {
                // 游戏进行时，切换到暂停
                case GameState.Running:
                    _states.RequestState(GameState.Paused);
                    return;

                // 在开始菜单时，确认是否关闭游戏
                case GameState.Menu:
                    _states.RequestState(GameState.BeforeExit);
                    return;

                // 游戏暂停时，切换到进行
                case GameState.Paused:
                    _states.RequestState(GameState.Running);
                    return;
            }
        }
    }
}
