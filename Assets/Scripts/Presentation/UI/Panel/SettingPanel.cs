using DeepseaOil.Logic.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeepseaOil.Presentation.UI
{
    public class SettingPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => true;

        private TMP_Text txtSfxNum;
        private TMP_Text txtBgmNum;

        protected override void Awake()
        {
            base.Awake();
            txtSfxNum = GetComponent<TMP_Text>("txtSfxNum");
            txtBgmNum = GetComponent<TMP_Text>("txtBgmNum");
        }

        public override void ShowMe()
        {
            ShowBgmValue((int)(AudioManager.Instance.BgmVolume * 100));
            ShowSfxValue((int)(AudioManager.Instance.SfxVolume * 100));
        }

        public override void HideMe()
        {
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "ReturnBtn":
                    UIMgr.Instance.HidePanel<SettingPanel>();
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
            txtSfxNum.text = vol.ToString() + " " + "%";
            AudioManager.Instance.SetSfxVolume(vol * 0.01f);
        }

        public void ShowBgmValue(int vol)
        {
            txtBgmNum.text = vol.ToString() + " " + "%";
            AudioManager.Instance.SetBgmVolume(vol * 0.01f);
        }

    }
}
