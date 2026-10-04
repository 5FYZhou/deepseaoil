using DeepseaOil.Presentation.UI;
using DeepseaOil.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    public class ExitConfirmPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => true;

        public override void ShowMe()
        {
            base.ShowMe();
        }

        public override void HideMe()
        {
            GameManager.Instance.ChangeState(GameState.Menu);
        }

        protected override void OnButtonClicked(string name)
        {
            switch(name)
            {
                case "ReturnMenuBtn":
                    UIMgr.Instance.HidePanel<ExitConfirmPanel>();
                    break;
                case "ExitBtn":
                    GameManager.Instance.ChangeState(GameState.Exit);
                    break;
            }
        }
    }
}
