using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class PausePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => true;

        public override void ShowMe() { }

        public override void HideMe()
        {
            GameRoot.Instance.Game.ChangeState(GameState.Running);
        }

        protected override ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSetting":
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>();
                    break;
                case "BtnContinue":
                    GameRoot.Instance.UI.HidePanel<PausePanel>();
                    GameRoot.Instance.Game.ChangeState(GameState.Running);
                    break;
                case "BtnRestart":
                    break;
                case "BtnReturnMenu":
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    // 开始场景直接同步加载
                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, false));
                    break;
            }
        }
    }
}
