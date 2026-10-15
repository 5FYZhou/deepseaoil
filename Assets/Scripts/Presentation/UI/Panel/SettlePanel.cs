using System.Collections;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class SettlePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        private GameObject _root;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeInRoutine;
        private const float FadeInDuration = 0.1f;

        protected override void Awake()
        {
            base.Awake();
            _root = GetComponent<Image>("ImgRoot").gameObject;
            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.AddComponent<CanvasGroup>();
        }

        public override void ShowMe()
        {
            HideResult();
        }

        public override void HideMe()
        {
        }

        private void OnDisable()
        {
            StopFadeIn();
        }

        public void ShowDefeat(int currentLevelId)
        {
            _root.SetActive(true);
            StartFadeIn();
        }

        private void StartFadeIn()
        {
            StopFadeIn();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _fadeInRoutine = StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < FadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / FadeInDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _fadeInRoutine = null;
            OnFadeInEnd();
        }

        private void OnFadeInEnd()
        {
            // 完全淡入后切到暂停状态 挡住暂停面板
            //GameRoot.Instance.Game.ChangeState(GameState.Paused);
        }

        private void HideResult()
        {
            StopFadeIn();
            _root.SetActive(false);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private void StopFadeIn()
        {
            if (_fadeInRoutine == null)
                return;

            StopCoroutine(_fadeInRoutine);
            _fadeInRoutine = null;
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtTitle":
                    return TxtStyle.Title; 
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnRestart":
                    string targetScene = SceneManager.GetActiveScene().name;
                    TryLoadScene(targetScene);
                    break;
                case "BtnReturnMenu":
                    TryLoadScene(SceneName.Begin);
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
