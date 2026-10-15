using DeepseaOil.Logic;
using TMPro;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class EndPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        private TMP_Text txtContext;

        protected override void Awake()
        {
            base.Awake();
            txtContext = GetComponent<TMP_Text>("TxtContext");
        }

        public override void ShowMe()
        {
            txtContext.text = "所有关卡已完成";
        }

        public override void HideMe()
        {
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtContext":
                    return TxtStyle.Body; 
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSkip":
                    if (!UnityEngine.Application.CanStreamedLevelBeLoaded(SceneName.Begin))
                    {
                        UnityEngine.Debug.LogError(
                            $"[Level] 主菜单场景 '{SceneName.Begin}' 不存在或未加入 Build Settings。",
                            this);
                        return;
                    }

                    EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, true));
                    break;
            }
        }

    }
}
