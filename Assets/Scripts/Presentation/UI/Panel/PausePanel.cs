using DeepseaOil.Logic;

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
                case "BtnContinue":
                    GameRoot.Instance.Game.ChangeState(GameState.Running);
                    break;
                case "BtnSetting":
                    // 打开设置面板
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>();
                    break;
                case "BtnReturnMenu":
                    // 切换到开始场景
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    break;
            }
        }
    }
}
