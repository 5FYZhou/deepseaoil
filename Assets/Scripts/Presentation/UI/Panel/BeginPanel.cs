using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic;

namespace DeepseaOil.Presentation.UI
{
    public class BeginPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Bottom;
        public override bool CanBeHideByKey => false;

        public override void HideMe()
        {

        }

        public override void ShowMe()
        {
        }
        protected override void OnButtonClicked(string btnName)
        {
            switch (btnName)
            {
                case "StartBtn":
                    GameManager.Instance.ChangeState(GameState.Running);
                    break;
                case "ContinueBtn":
                    break;
                case "SettingBtn":
                    UIMgr.Instance.ShowPanel<SettingPanel>();
                    break;
                case "ExitBtn":
                    GameManager.Instance.ChangeState(GameState.BeforeExit);
                    break;
            }
        }
    }
}
