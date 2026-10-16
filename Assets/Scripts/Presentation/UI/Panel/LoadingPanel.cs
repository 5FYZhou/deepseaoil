using DeepseaOil.Logic.Events;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class LoadingPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => false;

        [SerializeField] private Image mask;
        [SerializeField] private float fadeDuration = 0.3f;

        public bool CanRunTransition => mask != null;

        private bool _sceneLoaded;
        private bool _fadeInFinished;
        private bool _activationRequested;
        private bool _transitionActive;
        private string _sceneName;

        protected override void Awake()
        {
            base.Awake();

            EventBus<SceneLoadStarted>.Subscribe(OnSceneLoadStarted);
            EventBus<SceneLoaded>.Subscribe(OnSceneLoaded);
            EventBus<SceneActivated>.Subscribe(OnSceneActivated);
            EventBus<SceneTransitionFailed>.Subscribe(OnSceneTransitionFailed);
        }

        protected override void OnDestroy()
        {
            EventBus<SceneLoadStarted>.Unsubscribe(OnSceneLoadStarted);
            EventBus<SceneLoaded>.Unsubscribe(OnSceneLoaded);
            EventBus<SceneActivated>.Unsubscribe(OnSceneActivated);
            EventBus<SceneTransitionFailed>.Unsubscribe(OnSceneTransitionFailed);

            base.OnDestroy();
        }

        public override void ShowMe() { }

        public override void HideMe() { }

        // 直接修改 Mask Image 的透明度
        private void SetMaskAlpha(float alpha)
        {
            Color color = mask.color;
            color.a = alpha;
            mask.color = color;
        }

        private void OnSceneLoadStarted(SceneLoadStarted evt)
        {
            _sceneLoaded = false;
            _fadeInFinished = false;
            _activationRequested = false;
            _sceneName = evt.sceneName;

            if (!evt.isAsync) return;

            _transitionActive = true;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        private void OnSceneLoaded(SceneLoaded evt)
        {
            if (!_transitionActive || _sceneName != evt.sceneName)
                return;

            _sceneLoaded = true;

            TryActivateScene();
        }

        private void TryActivateScene()
        {
            if (!_transitionActive || _activationRequested || !_sceneLoaded || !_fadeInFinished)
                return;

            _activationRequested = true;
            EventBus<RequestActiveScene>.Publish(new RequestActiveScene(_sceneName));
        }

        private IEnumerator FadeIn()
        {
            mask.gameObject.SetActive(true);
            SetMaskAlpha(0f);

            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;

                SetMaskAlpha(
                    Mathf.Clamp01(time / fadeDuration));

                yield return null;
            }

            SetMaskAlpha(1f);

            _fadeInFinished = true;

            TryActivateScene();
        }

        private void OnSceneActivated(SceneActivated evt)
        {
            if (!_transitionActive || _sceneName != evt.sceneName)
                return;

            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private void OnSceneTransitionFailed(SceneTransitionFailed evt)
        {
            if (!_transitionActive || _sceneName != evt.sceneName)
                return;

            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            float startAlpha = mask.color.a;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;

                SetMaskAlpha(
                    Mathf.Lerp(startAlpha, 0f,
                        Mathf.Clamp01(time / fadeDuration)));

                yield return null;
            }

            SetMaskAlpha(0f);
            _transitionActive = false;

            EventBus<SceneTransitionFinished>.Publish(new SceneTransitionFinished(_sceneName));
        }
    }
}