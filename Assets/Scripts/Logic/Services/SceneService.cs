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

        public SceneService(PauseService pauseService)
        {
            this.pauseService = pauseService;
        }

        public void Init()
        {
            EventBus<RequestLoadScene>.Subscribe(Load);
            EventBus<RequestActiveScene>.Subscribe(Active);
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
            pauseService.SetPaused(false);


            AssetModule.OnSceneSwitch();

            if (!evt.isAsync)
            {


                SceneManager.LoadScene(evt.sceneName);
                _curScene = evt.sceneName;
                EventBus<SceneActivated>.Publish(new SceneActivated(evt.sceneName));
                return;
            }

            _nextSceneName = evt.sceneName;
            _loadedNotified = false;
            _op = SceneManager.LoadSceneAsync(_nextSceneName);
            _op.allowSceneActivation = false;   // 先不激活，等 GameManager 放行
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
        }
    }
}
