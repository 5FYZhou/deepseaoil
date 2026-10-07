using DeepseaOil.Logic;

namespace DeepseaOil.Presentation.UI
{
    public class SavePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => false;

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnClose":
                    GameRoot.Instance.UI.HidePanel<SavePanel>();
                    break;
                case "BtnSave0":
                    break;
                case "BtnSave1":
                    break;
                case "BtnSave2":
                    break;
                case "BtnSave3":
                    break;
            }
        }
    }
}
