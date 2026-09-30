using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    public class BeginPanel : BasePanel
    {
        public Image tip;
        public override void HideMe()
        {

        }

        public override void ShowMe()
        {
        }
        protected override void OnButtonClicked(string btnName)
        {
            switch (btnName)
            {
                case "StartBtn":
                    EventBus<RequestPause>.Publish(new RequestPause());
                    break;
                case "ContinueBtn":
                    EventBus<RequestResume>.Publish(new RequestResume());
                    break;
                case "ExitBtn":
                    EventBus<RequestAudio>.Publish(new RequestAudio(Logic.Service.audioType.None));
                    Application.Quit();
                    break;
            }
        }
    }
}
