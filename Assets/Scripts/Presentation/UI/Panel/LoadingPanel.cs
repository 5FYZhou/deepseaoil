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
            EventBus<SceneLoaded>.Subscribe(Hide);
            EventBus<RequestActiveScene>.Subscribe(Hide);
            EventBus<SceneActivated>.Subscribe(Hide);
        }

        protected override void OnDestroy()
        {
            EventBus<RequestLoadScene>.Unsubscribe(Show);
            EventBus<SceneLoaded>.Unsubscribe(Hide);
            EventBus<RequestActiveScene>.Unsubscribe(Hide);
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
            Debug.Log("LoadingShow");
            mask.gameObject.SetActive(true);
        }

        public void Hide(SceneLoaded evt)
        {
            Debug.Log("LoadingHide");
            //mask.gameObject.SetActive(false);
            EventBus<RequestActiveScene>.Publish(new RequestActiveScene(evt.sceneName));
        }

        public void Hide(RequestActiveScene evt)
        {
            Debug.Log("LoadingHideActive");
            //mask.gameObject.SetActive(false);
        }
        public void Hide(SceneActivated evt)
        {
            Debug.Log("LoadingSceneActivated");
            mask.gameObject.SetActive(false);
        }
    }
}
