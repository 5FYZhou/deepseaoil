using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    public class PausePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "ContinueBtn":
                    GameRoot.Instance.Game.ChangeState(GameState.Running);
                    break;
                case "SettingBtn":
                    // 打开设置面板
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>();
                    break;
                case "ReturnMenuBtn":
                    // 切换到开始场景
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    break;
            }
        }
    }
}
