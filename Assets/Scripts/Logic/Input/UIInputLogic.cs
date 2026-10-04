using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Input
{
    public interface IUIOperation
    {
        bool TryCloseTopmostPanel();
        void OpenPausePanel();
        void OpenExitConfirmPanel();
    }

    /// <summary>
    /// 由GameRoot驱动
    /// </summary>
    public sealed class UIInputLogic : ITickable
    {
        private IUIOperation _uiMgr;

        public UIInputLogic(IUIOperation uiMgr)
        {
            _uiMgr = uiMgr;
        }

        public void Tick(UILogicContext ctx)
        {
            // 如果按Esc
            if (ctx.inputSnapshot.EscPressed)
            {
                // 1. 尝试关闭最上层的面板（除开始面板）
                if (_uiMgr.TryCloseTopmostPanel())
                {
                    return;
                }
                var curState = GameManager.Instance.CurState;
                // 2. 游戏状态切换
                // 游戏进行时，切换到暂停
                if(curState == GameState.Running)
                {
                    GameManager.Instance.ChangeState(GameState.Paused);
                    return;
                }
                // 在开始菜单时，确认是否关闭游戏
                if(curState == Logic.GameState.Menu)
                {
                    GameManager.Instance.ChangeState(GameState.BeforeExit);
                    return;
                }
                // 游戏暂停时，切换到进行
                if(curState == GameState.Paused)
                {
                    GameManager.Instance.ChangeState(GameState.Running);
                    return;
                }
            }
        }
    }
}
