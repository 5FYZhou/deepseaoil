using DeepseaOil.Logic;
using DeepseaOil.Data;
using UnityEngine;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class BeginPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => false;

        public RectTransform maskStart;

        public override void HideMe()
        {
        }

        public override void ShowMe()
        {
            GameRoot.Instance.Audio.EnqueueBgm(cfg.dso.AudioId.BgmMain);
        }

        protected override ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        protected override void OnButtonClicked(string btnName)
        {
            switch (btnName)
            {
                case "BtnStart":
                    GameRoot.Instance.UI.ShowPanel<TransitionPanel>();
                    GameRoot.Instance.Game.ChangeState(GameState.CG);
                    GameRoot.Instance.UI.GetPanel<TransitionPanel>((transition) =>
                    {
                        GameRoot.Instance.UI.ShowPanel<CGPanel>((cgPanel) =>
                        {
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
