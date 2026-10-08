using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 游戏状态机：切状态时连带切面板、发暂停 / 恢复意图；它也是状态的唯一入口（改 <c>CurState</c> 只此一条路）。
    /// 唯一构造点是 <c>GameRoot.Assemble</c>，普通类、由 <c>GameRoot</c> 持有；面板操作走构造注入的 <c>UIMgr</c>，本类不认识 <c>GameRoot</c>。
    /// UI 输入逻辑只能经 <see cref="IUIStateRequest"/> 请求切状态，不能直接调它（依赖方向 Logic → 接口）。
    /// </summary>
    public sealed class GameManager : IUIStateRequest
    {
        private readonly UIMgr _ui;

        public GameState CurState { get; private set; } = GameState.None;

        public GameManager(UIMgr ui)
        {
            _ui = ui;
        }

        /// <inheritdoc />
        public void RequestState(GameState state)
        {
            ChangeState(state);
        }

        /// <summary>切换游戏状态；同状态是 no-op（面板显示 / 隐藏与暂停意图都在这里发生）。</summary>
        public void ChangeState(GameState newState)
        {
            if (CurState == newState) return;

            switch (newState)
            {
                case GameState.Menu:
                    _ui.ShowPanel<BeginPanel>();

                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    // 收起战斗 HUD：它是局内读数，留在菜单上会盖住开始面板。
                    _ui.HidePanel<HudPanel>();
                    _ui.HidePanel<GamePlayPanel>();

                    // 请求暂停：PlayerController 监听该事件，并在暂停时关掉输入。
                    EventBus<RequestPause>.Publish(new RequestPause());
                    break;

                case GameState.Running:
                    EventBus<RequestResume>.Publish(new RequestResume());

                    if (CurState == GameState.Menu)
                        _ui.HidePanel<BeginPanel>();
                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    _ui.ShowPanel<HudPanel>();
                    _ui.ShowPanel<GamePlayPanel>();
                    break;

                case GameState.Paused:
                    EventBus<RequestPause>.Publish(new RequestPause());
                    _ui.ShowPanel<PausePanel>();
                    break;

                case GameState.BeforeExit:
                    _ui.ShowPanel<ExitConfirmPanel>();
                    break;

                case GameState.Exit:
                    Application.Quit();
                    break;
            }

            CurState = newState;
        }
    }
}
