using System;
using System.Collections;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Level;
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

        private TMP_Text txtTitle;
        private GameObject _winRoot;
        private GameObject _loseRoot;
        private int _nextLevelId;
        private GameObject _root;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeInRoutine;
        private LevelState _levelState;
        private const float FadeInDuration = 0.35f;

        protected override void Awake()
        {
            base.Awake();
            txtTitle = GetComponent<TMP_Text>("TxtTitle");
            _loseRoot = GetComponent<Image>("ImgLose").gameObject;
            _root = GetComponent<Image>("ImgRoot").gameObject;
            _winRoot = GetComponent<Image>("ImgWin").gameObject;
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

        private void OnEnable()
        {
            EventBus<LevelStateChanged>.Subscribe(OnLevelStateChanged);
            EventBus<LevelEnded>.Subscribe(OnLevelEnded);
        }

        private void OnDisable()
        {
            EventBus<LevelStateChanged>.Unsubscribe(OnLevelStateChanged);
            EventBus<LevelEnded>.Unsubscribe(OnLevelEnded);
            StopFadeIn();
        }

        private void OnLevelStateChanged(LevelStateChanged evt)
        {
            _levelState = evt.Level;
        }

        private void OnLevelEnded(LevelEnded evt)
        {
            if (_levelState != LevelState.Victory && _levelState != LevelState.Defeat)
                return;

            _nextLevelId = _levelState == LevelState.Victory ? evt.nextId : 0;
            SetResult(evt.curId);
            _root.SetActive(true);
            StartFadeIn();
        }

        private void SetResult(int currentLevelId)
        {
            switch (_levelState)
            {
                case LevelState.Victory:
                    txtTitle.text = $"第 {currentLevelId} 关胜利\n继续进入第 {_nextLevelId} 关";
                    _winRoot.SetActive(true);
                    _loseRoot.SetActive(false);
                    break;
                case LevelState.Defeat:
                    txtTitle.text = "";
                    _winRoot.SetActive(false);
                    _loseRoot.SetActive(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(_levelState),
                        _levelState,
                        "Only terminal level states can be shown.");
            }
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
            GameRoot.Instance.Game.ChangeState(GameState.Paused);
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
                case "BtnNextLevel":
                    TryLoadScene($"Level_{_nextLevelId}", GameState.Running);
                    break;
                case "BtnRestart":
                    string targetScene = SceneManager.GetActiveScene().name;
                    TryLoadScene(targetScene, GameState.Running);
                    break;
                case "BtnReturnMenu":
                    TryLoadScene(SceneName.Begin, GameState.Menu);
                    break;
            }
        }

        private void TryLoadScene(string sceneName, GameState state)
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

            GameRoot root = GameRoot.Instance;
            root.UI.CloseAllPanels();
            root.Game.ChangeState(state);
            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(sceneName, false));
        }

    }
}
