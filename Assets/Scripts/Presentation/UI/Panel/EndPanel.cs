using DeepseaOil.Logic;
using TMPro;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class EndPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;

        private TMP_Text txtContext;

        protected override void Awake()
        {
            base.Awake();
            txtContext = GetComponent<TMP_Text>("TxtContext");
        }

        public override void ShowMe()
        {
        }

        public override void HideMe()
        {
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtContext":
                    return TxtStyle.Body; 
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSkip":
                    // 跳过
                    break;
            }
        }

    }
}
