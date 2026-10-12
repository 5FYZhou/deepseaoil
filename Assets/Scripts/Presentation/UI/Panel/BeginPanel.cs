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

        public RectTransform maskStart;

        public override void HideMe()
        {
        }

        public override void ShowMe()
        {
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
                                    // 预先加载游戏场景
                                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Game, true));
                                }
                            );
                        });
                    });
                    // 预先打开游戏场景中的面板
                    GameRoot.Instance.UI.ShowPanel<HudPanel>();
                    GameRoot.Instance.UI.ShowPanel<GamePlayPanel>();
                    break;
                case "BtnContinue":
                    GameRoot.Instance.UI.ShowPanel<SavePanel>();
                    break;
                case "BtnSetting":
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>();
                    break;
                case "BtnExit":
                    GameRoot.Instance.UI.ShowPanel<ExitConfirmPanel>();
                    GameRoot.Instance.Game.ChangeState(GameState.BeforeExit);
                    break;
            }
        }
    }
}
