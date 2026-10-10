using DeepseaOil.Presentation.UI;
using DeepseaOil.Logic;
using DeepseaOil.Data;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Playables;

namespace DeepseaOil.Presentation.UI
{
    [RequireComponent(typeof(PlayableDirector))]
    public class CGPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
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
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _director.stopped -= OnTimelineStopped;
        }

        public override void ShowMe()
        {
            _director.time = 0;
            _director.Play();
        }

        public override void HideMe()
        {
            _director.Stop();
        }

        private void OnTimelineStopped(PlayableDirector director)
        {
            if (director != _director)
                return;

            // Timeline 播放完成
            GameRoot.Instance.Game.ChangeState(GameState.Running);
        }
    }
}
