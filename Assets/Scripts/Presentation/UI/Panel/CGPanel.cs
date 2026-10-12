using DeepseaOil.Presentation.UI;
using DeepseaOil.Logic;
using DeepseaOil.Data;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Playables;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Presentation.UI
{
    [RequireComponent(typeof(PlayableDirector))]
    public class CGPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;

        private PlayableDirector _director;
        private Image _img1;
        private Image _img2;

        protected override void Awake()
        {
            base.Awake();
            _img1 = GetComponent<Image>("Img1");
            _img2 = GetComponent<Image>("Img2");
            _director = GetComponent<PlayableDirector>();
            _director.stopped += OnTimelineStopped;
            EventBus<SceneActivated>.Subscribe(Hide);
        }

        protected override void OnDestroy()
        {
            if (_director != null)
                _director.stopped -= OnTimelineStopped;
            EventBus<SceneActivated>.Unsubscribe(Hide);
            base.OnDestroy();
        }

        public override void ShowMe()
        {
        }

        public override void HideMe()
        {
            _director.Stop();
        }

        public void StartTimeLine()
        {
            _director.time = 0;
            _director.Play();
        }

        private void OnTimelineStopped(PlayableDirector director)
        {
            if (director != _director)
                return;

            // Timeline 播放完成
            // 正式激活新场景
            EventBus<RequestActiveScene>.Publish(new RequestActiveScene("EmptyTest"));
            GameRoot.Instance.Game.ChangeState(GameState.Running);
        }

        public void Hide(SceneActivated evt)
        {
            GameRoot.Instance.UI.HidePanel<CGPanel>();
        }
    }
}
