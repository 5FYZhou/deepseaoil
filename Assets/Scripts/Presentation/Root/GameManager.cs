using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Presentation;
using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 
    /// 游戏状态驱动暂停、面板等
    /// 
    /// </summary>

    public enum GameState
    {
        None = 0,
        // 开始界面
        Menu,
        // 暂停中
        Paused,
        // 游戏中
        Running,
        // 确认退出游戏
        BeforeExit,
        // 退出游戏
        Exit
    }

    public class GameManager : BaseManager<GameManager>
    {
        private GameState _state = GameState.None;

        public GameState CurState { get => _state; set => _state = value; }

        private GameManager() { }

        public void ChangeState(GameState newState)
        {
            if (CurState == newState) return;

            // 处理状态进入时的逻辑
            switch (newState)
            {
                case GameState.Menu:
                    UIMgr.Instance.ShowPanel<BeginPanel>();
                    if (CurState == GameState.Paused)
                        UIMgr.Instance.HidePanel<PausePanel>();
                    
                    // 请求暂停 (PlayerController监听了暂停事件,暂停时关闭InputProvider
                    EventBus<RequestPause>.Publish(new RequestPause());
                    break;
                case GameState.Running:
                    // 请求恢复
                    EventBus<RequestResume>.Publish(new RequestResume());
                    // 关闭面板
                    if (CurState == GameState.Menu)
                        UIMgr.Instance.HidePanel<BeginPanel>();
                    if(CurState == GameState.Paused)
                        UIMgr.Instance.HidePanel<PausePanel>();
                    // 打开玩家面板
                    // ...
                    break;
                case GameState.Paused:
                    // 请求暂停
                    EventBus<RequestPause>.Publish(new RequestPause());
                    // 打开暂停面板
                    UIMgr.Instance.ShowPanel<PausePanel>();
                    break;
                case GameState.BeforeExit:
                    UIMgr.Instance.ShowPanel<ExitConfirmPanel>();
                    break;
                case GameState.Exit:
                    // 退出游戏
                    Application.Quit();
                    break;
            }

            CurState = newState;
            // 广播状态切换事件
            //EventCenter.Broadcast(EventType.GameStateChanged, newState);
        }

    }
}
