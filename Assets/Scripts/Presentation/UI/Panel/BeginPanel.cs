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
            //MusicMgr.Instance.PlayBKMusic("阅读背景音");
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
                    Application.Quit();
                    break;
            }
        }

        private IEnumerator ShowTip()
        {
            tip.gameObject.SetActive(true);
            yield return new WaitForSeconds(2);
            tip.gameObject.SetActive(false);
        }
    }
}
