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
                    GameManager.Instance.ChangeState(GameState.Running);
                    break;
                case "BtnSetting":
                    // 打开设置面板
                    UIMgr.Instance.ShowPanel<SettingPanel>();
                    break;
                case "BtnReturnMenu":
                    // 切换到开始场景
                    GameManager.Instance.ChangeState(GameState.Menu);
                    break;
            }
        }
    }
}
