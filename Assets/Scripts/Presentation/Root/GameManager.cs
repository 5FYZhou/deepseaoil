using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>游戏状态机：切状态连带切面板、发暂停/恢复意图，是状态唯一入口。构造点只有 GameRoot.Assemble；面板操作走注入的 UIMgr，本类不认识 GameRoot。UI 输入只能经 IUIStateRequest 请求切状态。</summary>
    public sealed class GameManager : IUIStateRequest
    {
        private readonly UIMgr _ui;

        public GameState CurState { get; private set; } = GameState.None;

        public GameManager(UIMgr ui)
        {
            _ui = ui;
        }

        public void RequestState(GameState state)
        {
            ChangeState(state);
        }

        public void ChangeState(GameState newState)
        {
            if (CurState == newState) return;

            switch (newState)
            {
                case GameState.Menu:
                    _ui.ShowPanel<BeginPanel>();

                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    _ui.HidePanel<HudPanel>();

                    // 请求暂停；PlayerController 监听它并在暂停时关掉输入
                    EventBus<RequestPause>.Publish(new RequestPause());
                    break;

                case GameState.Running:
                    EventBus<RequestResume>.Publish(new RequestResume());

                    if (CurState == GameState.Menu)
                        _ui.HidePanel<BeginPanel>();
                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    _ui.ShowPanel<HudPanel>();
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
