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

        protected override ButtonStyle SetBtnStyle(string name)
        {
            switch (name)
            {
                case "BtnStart":
                case "BtnContinue":
                case "BtnExit":
                    return ButtonStyle.Normal;
                case "BtnSetting":
                    return ButtonStyle.None;
            }
            return ButtonStyle.None;
        }

        protected override void OnButtonClicked(string btnName)
        {
            switch (btnName)
            {
                case "BtnStart":
                    GameRoot.Instance.Game.ChangeState(GameState.Running);
                    break;
                case "BtnContinue":
                    GameRoot.Instance.UI.ShowPanel<SavePanel>();
                    break;
                case "BtnSetting":
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>();
                    break;
                case "BtnExit":
                    GameRoot.Instance.Game.ChangeState(GameState.BeforeExit);
                    break;
            }
        }
    }
}
