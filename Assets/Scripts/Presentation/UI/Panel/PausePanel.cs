using DeepseaOil.Logic;

namespace DeepseaOil.Presentation.UI
{
    public class PausePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;

        public override void ShowMe() { }

        public override void HideMe() { }

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
                    GameRoot.Instance.Game.ChangeState(GameState.Running);
                    break;
                case "BtnRestart":
                    // 重新开始关卡
                    break;
                case "BtnReturnMenu":
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    break;
            }
        }
    }
}
