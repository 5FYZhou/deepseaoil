using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepseaOil.Presentation.UI
{
    public class PausePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => true;
        public override bool CloseOnSceneTransition => true;

        public override void ShowMe() { }

        public override void HideMe()
        {
            GameRoot.Instance.Game.Resume();
        }

        protected override ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSetting":
                    GameRoot.Instance.UI.ShowPanel<SettingPanel>((panel)=>panel.CanSave(true));
                    break;
                case "BtnContinue":
                    GameRoot.Instance.Game.Resume();
                    break;
                case "BtnRestart":
                    string targetScene = SceneManager.GetActiveScene().name;
                    TryLoadScene(targetScene);
                    break;
                case "BtnReturnMenu":
                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, true));
                    break;
            }
        }

        private void TryLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[Level] 目标场景名为空，无法切换场景。", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[Level] 场景 '{sceneName}' 不存在或未加入 Build Settings，无法切换场景。",
                    this);
                return;
            }

            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(sceneName, true));
        }
    }
}
