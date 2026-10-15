using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Events;
using DeepseaOil.Data;
using UnityEngine.SceneManagement;
using UnityEngine;
using System;

namespace DeepseaOil.Logic.Service
{
    public sealed class SceneService : IService
    {
        private readonly PauseService pauseService;
        private AsyncOperation _op;
        private string _curScene;
        private string _nextSceneName;
        private bool _loadedNotified;
        private bool _transitionInProgress;
        private bool _sceneLoadStarted;

        public SceneService(PauseService pauseService)
        {
            this.pauseService = pauseService;
        }

        public void Init()
        {
            EventBus<RequestLoadScene>.Subscribe(Load);
            EventBus<RequestActiveScene>.Subscribe(Active);
            EventBus<LoadingPanelReady>.Subscribe(OnLoadingPanelReady);
            EventBus<SceneTransitionFinished>.Subscribe(OnSceneTransitionFinished);
            EventBus<SceneTransitionFailed>.Subscribe(OnSceneTransitionFailed);
        }

        /// <remarks>无每帧工作，切场景由事件驱动（Load）</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (_op == null) return;


            if (_op.progress >= 0.9f && !_op.allowSceneActivation)
            {
                if(_loadedNotified) return;
                EventBus<SceneLoaded>.Publish(new SceneLoaded(_nextSceneName));
                _loadedNotified = true;
            }


            if (_op.isDone && _op.allowSceneActivation)
            {
                _op = null;
                _loadedNotified = false;
                EventBus<SceneActivated>.Publish(new SceneActivated(_nextSceneName));
            }
        }

        public void Load(RequestLoadScene evt)
        {
            if (_transitionInProgress)
            {
                string error = $"场景切换 '{_nextSceneName}' 尚未完成，拒绝重复请求 '{evt.sceneName}'。";
                Debug.LogError($"[Scene] {error}");
                return;
            }

            if (evt.isAsync)
            {
                _nextSceneName = evt.sceneName;
                _loadedNotified = false;
                _sceneLoadStarted = false;
                _transitionInProgress = true;
                return;
            }

            pauseService.SetPaused(false);

            AssetModule.OnSceneSwitch();

            EventBus<SceneLoadStarted>.Publish(new SceneLoadStarted(evt.sceneName, evt.isAsync));

            //EventBus.ClearAll();
            SceneManager.LoadScene(evt.sceneName);
            _curScene = evt.sceneName;
            EventBus<SceneActivated>.Publish(new SceneActivated(evt.sceneName));
        }

        private void OnLoadingPanelReady(LoadingPanelReady evt)
        {
            if (!_transitionInProgress || _sceneLoadStarted || _nextSceneName != evt.sceneName)
                return;

            _sceneLoadStarted = true;
            EventBus<SceneLoadStarted>.Publish(new SceneLoadStarted(_nextSceneName, true));

            if (!Application.CanStreamedLevelBeLoaded(_nextSceneName))
            {
                FailTransition($"场景 '{_nextSceneName}' 不存在或未加入 Build Settings。");
                return;
            }

            AssetModule.OnSceneSwitch();

            try
            {
                _op = SceneManager.LoadSceneAsync(_nextSceneName);
            }
            catch (ArgumentException ex)
            {
                FailTransition($"无法异步加载场景 '{_nextSceneName}'：{ex.Message}");
                return;
            }

            if (_op == null)
            {
                FailTransition($"Unity 未能创建场景 '{_nextSceneName}' 的异步加载操作。");
                return;
            }

            _op.allowSceneActivation = false;
        }

        private void FailTransition(string error)
        {
            Debug.LogError($"[Scene] {error}");
            _op = null;
            _loadedNotified = false;
            EventBus<SceneTransitionFailed>.Publish(new SceneTransitionFailed(_nextSceneName, error));
        }

        private void OnSceneTransitionFailed(SceneTransitionFailed evt)
        {
            if (!_transitionInProgress || _nextSceneName != evt.sceneName)
                return;

            _op = null;
            _loadedNotified = false;
            _sceneLoadStarted = false;
        }

        private void OnSceneTransitionFinished(SceneTransitionFinished evt)
        {
            if (!_transitionInProgress || _nextSceneName != evt.sceneName)
                return;

            _op = null;
            _nextSceneName = null;
            _loadedNotified = false;
            _sceneLoadStarted = false;
            _transitionInProgress = false;
        }

        /// 激活异步加载的场景，同步加载不调用
        public void Active(RequestActiveScene evt)
        {
            if (_op == null)
                throw new InvalidOperationException(
                    "Activate 被调用时没有正在加载的场景。");

            if (_nextSceneName != evt.sceneName)
                throw new InvalidOperationException(
                    $"尝试激活 '{evt.sceneName}'，但正在加载的是 '{_nextSceneName}'。");

            if (_op.allowSceneActivation) return; // 幂等

            _op.allowSceneActivation = true;
            _curScene = evt.sceneName;
        }

        public void Dispose()
        {
            EventBus<RequestLoadScene>.Unsubscribe(Load);
            EventBus<RequestActiveScene>.Unsubscribe(Active);
            EventBus<LoadingPanelReady>.Unsubscribe(OnLoadingPanelReady);
            EventBus<SceneTransitionFinished>.Unsubscribe(OnSceneTransitionFinished);
            EventBus<SceneTransitionFailed>.Unsubscribe(OnSceneTransitionFailed);
        }
    }
}
