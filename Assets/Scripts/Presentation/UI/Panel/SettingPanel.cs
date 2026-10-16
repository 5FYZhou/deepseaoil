using DeepseaOil.Logic;
using TMPro;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DeepseaOil.Presentation.UI
{
    public class SettingPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => true;
        public override bool CloseOnSceneTransition => true;

        private TMP_Text txtSfxNum;
        private TMP_Text txtBgmNum;

        private Button btnSave;

        protected override void Awake()
        {
            base.Awake();
            txtSfxNum = GetComponent<TMP_Text>("txtSfxNum");
            txtBgmNum = GetComponent<TMP_Text>("txtBgmNum");
            btnSave = GetComponent<Button>("BtnSave");
        }

        public override void ShowMe()
        {
            ShowBgmValue((int)(GameRoot.Instance.Audio.BgmVolume * 100));
            ShowSfxValue((int)(GameRoot.Instance.Audio.SfxVolume * 100));
        }

        public override void HideMe()
        {
        }

        public void CanSave(bool canSave)
        {
            btnSave.interactable = canSave;
        }


        protected override ButtonStyle SetBtnStyle(string name)
        {
            switch (name)
            {
                case "BtnClose":
                case "BtnReturnMenu":
                case "BtnSave":
                case "BtnLoad":
                    return ButtonStyle.None;
            }
            return ButtonStyle.None;
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "txtSfxNum":
                    return TxtStyle.Title;
                case "txtBgmNum":
                    return TxtStyle.Body;
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnClose":
                    GameRoot.Instance.UI.HidePanel<SettingPanel>();
                    break;
                case "BtnReturnMenu":
                    GameRoot.Instance.UI.HidePanel<SettingPanel>();
                    GameRoot.Instance.Game.ChangeState(GameState.Menu);

                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, true));
                    break;
                case "BtnSave":
                    GameRoot.Instance.UI.ShowPanel<SavePanel>((panel) => panel.SetMode(SavePanelMode.Save));
                    break;
                case "BtnLoad":
                    GameRoot.Instance.UI.ShowPanel<SavePanel>((panel) => panel.SetMode(SavePanelMode.Load));
                    break;
            }
        }

        protected override void OnSliderValueChange(string sliderName, float value)
        {
            switch (sliderName)
            {
                case "SliderSfx":
                    ShowSfxValue((int)value);
                    break;
                case "SliderBgm":
                    ShowBgmValue((int)value);
                    break;
            }
        }

        public void ShowSfxValue(int vol)
        {
            if (txtSfxNum != null) txtSfxNum.text = vol.ToString() + " " + "%";

            GameRoot.Instance.Audio.SetSfxVolume(vol * 0.01f);
        }

        public void ShowBgmValue(int vol)
        {
            if (txtBgmNum != null) txtBgmNum.text = vol.ToString() + " " + "%";

            GameRoot.Instance.Audio.SetBgmVolume(vol * 0.01f);
        }

    }
}
