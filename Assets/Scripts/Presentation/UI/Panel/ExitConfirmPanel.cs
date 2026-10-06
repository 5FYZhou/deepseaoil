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
            GameRoot.Instance.Game.ChangeState(GameState.Menu);
        }

        protected override void OnButtonClicked(string name)
        {
            switch(name)
            {
                case "ReturnMenuBtn":
                    GameRoot.Instance.UI.HidePanel<ExitConfirmPanel>();
                    break;
                case "ExitBtn":
                    GameRoot.Instance.Game.ChangeState(GameState.Exit);
                    break;
            }
        }
    }
}
