using DeepseaOil.Logic.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class LoadingPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;

        public Image mask;

        protected override void Awake()
        {
            base.Awake();
            EventBus<RequestLoadScene>.Subscribe(Show);


            EventBus<SceneActivated>.Subscribe(Hide);
        }

        protected override void OnDestroy()
        {
            EventBus<RequestLoadScene>.Unsubscribe(Show);


            EventBus<SceneActivated>.Unsubscribe(Hide);
            base.OnDestroy();
        }

        public override void ShowMe()
        {
        }

        public override void HideMe()
        {
        }

        public void Show(RequestLoadScene evt)
        {
            mask.gameObject.SetActive(true);
        }

        public void Hide(SceneLoaded evt)
        {
            mask.gameObject.SetActive(false);
        }

        public void Hide(RequestActiveScene evt)
        {
            mask.gameObject.SetActive(false);
        }
        public void Hide(SceneActivated evt)
        {
            mask.gameObject.SetActive(false);
        }
    }
}
