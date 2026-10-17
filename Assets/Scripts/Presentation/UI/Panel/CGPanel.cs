using System;
using System.Collections;
using System.Collections.Generic;
using DeepseaOil.Presentation.UI;
using DeepseaOil.Logic;
using DeepseaOil.Data;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.Playables;
using DeepseaOil.Logic.Events;
using UnityEngine.UI;
using TMPro;

namespace DeepseaOil.Presentation.UI
{
    [RequireComponent(typeof(PlayableDirector))]
    public class CGPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        public event Action<PointerEventData> Clicked;

        [SerializeField] private GameObject startRoot;
        [SerializeField] private GameObject shot1;
        [SerializeField] private float fadeSeconds = 1f;
        [SerializeField] private float blackenSeconds = 1f;
        [SerializeField] private float holdSeconds = 2f;

        private const double TimelineHoldBack = 0.05;

        private PlayableDirector _director;
        private Image _imgEndRoot;
        private TMP_Text _txtEnd;
        private Coroutine _endRoutine;
        private Coroutine _timelineWatch;
        private bool _isEndMode;

        protected override void Awake()
        {
            base.Awake();
            _director = GetComponent<PlayableDirector>();
            // 挂住末帧：wrap=None 播完即把各 ImgStory Revert 回作者态，CG 先于遮罩消失
            _director.extrapolationMode = DirectorWrapMode.Hold;
            _imgEndRoot = GetComponent<Image>("ImgEnd");
            _txtEnd = GetComponent<TMP_Text>("TxtEnd");
            EventBus<SceneActivated>.Subscribe(Hide);
        }

        protected override void OnDestroy()
        {
            StopTimelineWatch();
            EventBus<SceneActivated>.Unsubscribe(Hide);
            base.OnDestroy();
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            return TxtStyle.Title;
        }

        public override void ShowMe()
        {
            shot1.SetActive(true);
        }

        protected override bool KeepAuthoredTxt(string name)
        {
            return name == "TxtEnd";
        }

        public override void HideMe()
        {
            StopEndSequence();
            StopTimelineWatch();
            _director.Stop();
        }

        public void SetType(bool isStart)
        {
            StopEndSequence();
            StopTimelineWatch();

            if (isStart)
            {
                _isEndMode = false;
                _imgEndRoot.gameObject.SetActive(false);
                startRoot.gameObject.SetActive(true);
                return;
            }

            _isEndMode = true;
            _director.Stop();
            _imgEndRoot.gameObject.SetActive(true);
            startRoot.gameObject.SetActive(false);
            _endRoutine = StartCoroutine(PlayEndSequence());
        }

        private void StopEndSequence()
        {
            if (_endRoutine == null)
                return;

            StopCoroutine(_endRoutine);
            _endRoutine = null;
        }

        private IEnumerator PlayEndSequence()
        {
            Color imgFrom = Color.white;
            imgFrom.a = 0f;
            _imgEndRoot.color = imgFrom;

            Color txtTo = _txtEnd.color;
            Color txtFrom = txtTo;
            txtFrom.a = 0f;
            _txtEnd.color = txtFrom;

            yield return FadeGraphic(_imgEndRoot, imgFrom, Color.white, fadeSeconds);

            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            yield return FadeGraphic(_imgEndRoot, Color.white, Color.black, blackenSeconds);

            yield return FadeGraphic(_txtEnd, txtFrom, txtTo, fadeSeconds);

            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            _endRoutine = null;
            GameRoot.Instance.UI.ShowPanel<EndPanel>();
        }

        private static IEnumerator FadeGraphic(Graphic graphic, Color from, Color to, float duration)
        {
            graphic.color = from;

            if (duration <= 0f)
            {
                graphic.color = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                graphic.color = Color.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            graphic.color = to;
        }

        public void StartTimeLine()
        {
            StopTimelineWatch();

            _director.time = 0;
            _director.Play();

            _timelineWatch = StartCoroutine(CoWaitTimelineEnd());
        }

        private void StopTimelineWatch()
        {
            if (_timelineWatch == null)
                return;

            StopCoroutine(_timelineWatch);
            _timelineWatch = null;
        }

        private IEnumerator CoWaitTimelineEnd()
        {
            // Hold 下不会自己 stopped：按时间到点冻住，不能用 Stop（graph 销毁会让各 Activation 轨道 Revert）
            double holdTime = _director.duration - TimelineHoldBack;

            while (_director.state == PlayState.Playing && _director.time < holdTime)
                yield return null;

            _director.Pause();

            _timelineWatch = null;

            if (_isEndMode)
                yield break;

            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Game, true));
        }

        public void Hide(SceneActivated evt)
        {
            GameRoot.Instance.UI.HidePanel<CGPanel>();
        }
    }
}