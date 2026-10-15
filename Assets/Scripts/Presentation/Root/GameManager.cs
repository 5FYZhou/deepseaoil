using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>游戏状态机：切状态连带切面板、发暂停/恢复意图，是状态唯一入口。构造点只有 GameRoot.Assemble；面板操作走注入的 UIMgr，本类不认识 GameRoot。UI 输入只能经 IGameStateRequest 请求切状态。</summary>
    public sealed class GameManager : IGameStateRequest
    {
        public GameState CurState { get; private set; } = GameState.None;

        public GameManager()
        {
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
                    // 开始场景BeginSceneInitializer打开开始面板
                    break;
                case GameState.CG:

                    break;
                case GameState.Running:
                    break;

                case GameState.Paused:
                    break;

                case GameState.Exit:
                    break;
            }

            CurState = newState;
            EventBus<GameStateChanged>.Publish(new GameStateChanged(CurState));
        }
    }
}
