using System.Collections;
using DeepseaOil.Logic;
using DeepseaOil.Data;
using UnityEngine;
using DeepseaOil.Logic.Events;
using TMPro;

namespace DeepseaOil.Presentation.UI
{
    public class BeginPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => false;

        public RectTransform maskStart;
        [SerializeField] private GameObject btnRoot = null;
        [SerializeField] private GameObject logoRoot = null;
        [SerializeField] private GameObject btnEnter = null;

        private const float LogoFadeSeconds = 0.4f;
        private const float BtnFadeSeconds = 0.4f;
        private const float TipBreathSeconds = 2.4f;
        private const float TipAlphaMin = 0.3f;

        // 首次进入给 Logo，从游戏回来直接给按钮；面板跨场景常驻，静态量随进程走
        private static bool s_logoPending = true;

        private TMP_Text _txtTip;
        private UIFader _logoFader;
        private UIFader _btnFader;
        private Coroutine _breathRoutine;

        protected override void Awake()
        {
            base.Awake();
            _txtTip = GetComponent<TMP_Text>("TxtTip");
            _logoFader = UIFader.Create(this, logoRoot);
            _btnFader = UIFader.Create(this, btnRoot);
        }

        public override void ShowMe()
        {
            GameRoot.Instance.Audio.EnqueueBgm(cfg.dso.AudioId.BgmMain);

            if (s_logoPending)
                ShowLogoState();
            else
                ShowButtonState(false);
        }

        public override void HideMe()
        {
            StopBreath();
        }

        protected override ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            return name == "TxtTip" ? TxtStyle.None : TxtStyle.Body;
        }

        protected override bool KeepAuthoredTxt(string name)
        {
            return name == "TxtTip";
        }

        private void ShowLogoState()
        {
            btnEnter.SetActive(true);
            _btnFader.HideImmediate();
            _logoFader.ShowImmediate();

            StopBreath();
            _breathRoutine = StartCoroutine(CoBreathTip());
        }

        private void ShowButtonState(bool animated)
        {
            s_logoPending = false;
            StopBreath();
            btnEnter.SetActive(false);

            if (!animated)
            {
                _logoFader.HideImmediate();
                _btnFader.ShowImmediate();
                return;
            }

            _logoFader.FadeOut(LogoFadeSeconds);
            _btnFader.FadeIn(BtnFadeSeconds);
        }

        private void StopBreath()
        {
            if (_breathRoutine == null)
                return;

            StopCoroutine(_breathRoutine);
            _breathRoutine = null;
        }

        private IEnumerator CoBreathTip()
        {
            if (_txtTip == null)
                yield break;

            float omega = 2f * Mathf.PI / TipBreathSeconds;
            float phase = 0f;

            while (true)
            {
                // 菜单态 timeScale=0，缩放 deltaTime 恒为 0，呼吸必须吃 unscaled
                phase += Time.unscaledDeltaTime;
                _txtTip.alpha = Mathf.Lerp(TipAlphaMin, 1f, (Mathf.Sin(phase * omega) + 1f) * 0.5f);
                yield return null;
            }
        }

        protected override void OnButtonClicked(string btnName)
        {
            switch (btnName)
            {
                case "BtnEnter":
                    ShowButtonState(true);
                    break;
                case "BtnStart":
                    GameRoot.Instance.Audio.EnqueueBgm(cfg.dso.AudioId.BgmAscensio);
                    GameRoot.Instance.UI.ShowPanel<TransitionPanel>();
                    GameRoot.Instance.Game.ChangeState(GameState.CG);
                    GameRoot.Instance.UI.GetPanel<TransitionPanel>((transition) =>
                    {
                        GameRoot.Instance.UI.ShowPanel<CGPanel>((cgPanel) =>
                        {
                            cgPanel.SetType(true);
                            transition.SetMaskPos(this.maskStart.localPosition);
                            transition.Play(
                                this, cgPanel,
                                () =>
                                {
                                    GameRoot.Instance.UI.HidePanel<BeginPanel>();
                                    GameRoot.Instance.UI.HidePanel<TransitionPanel>();
                                    cgPanel.StartTimeLine();
                                }
                            );
                        });
                    });

                    break;
                case "BtnLoad":
                    GameRoot.Instance.UI.ShowPanel<SavePanel>((panel) => panel.SetMode(SavePanelMode.Load));
                    break;
                case "BtnSetting":
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>((panel) => panel.CanSave(false));
                    break;
                case "BtnExit":
                    GameRoot.Instance.Game.ChangeState(GameState.Exit);
                    Application.Quit();
                    break;
            }
        }
    }
}
