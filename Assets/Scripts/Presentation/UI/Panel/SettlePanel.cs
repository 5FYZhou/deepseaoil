using DeepseaOil.Logic;
using TMPro;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class SettlePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => false;

        private TMP_Text txtTitle;

        protected override void Awake()
        {
            base.Awake();
            txtTitle = GetComponent<TMP_Text>("TxtTitle");
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
                case "TxtTitle":
                    return TxtStyle.Title; 
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnRestart":
                    // 重新开始当前波次
                    break;
                case "BtnReturnMenu":
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, false));
                    break;
            }
        }

    }
}
