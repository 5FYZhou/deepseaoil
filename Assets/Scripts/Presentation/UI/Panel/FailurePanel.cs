using DeepseaOil.Logic;

namespace DeepseaOil.Presentation.UI
{
    public class FailurePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => false;

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnRestart":
                    break;
                case "BtnReturnMenu":
                    // 切换到开始场景
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);
                    break;
            }
        }
    }
}
