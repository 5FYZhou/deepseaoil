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
            EventBus<RequestLoadScene>.Subscribe(LoadAsync);
            EventBus<RequestActiveScene>.Subscribe(Active);
        }

        /// <remarks>无每帧工作，切场景由事件驱动（Load）</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (_op == null) return;

            // 场景加载完毕，等待GameManager发信号激活
            if (_op.progress >= 0.9f && !_op.allowSceneActivation)
            {
                if(_loadedNotified) return;
                EventBus<SceneLoaded>.Publish(new SceneLoaded(_nextSceneName));
                _loadedNotified = true;
            }

            // 放行且真正完成，才清理
            if (_op.isDone && _op.allowSceneActivation)
            {
                _op = null;
                _loadedNotified = false;
                EventBus<SceneActivated>.Publish(new SceneActivated(_nextSceneName));
            }
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public void LoadAsync(RequestLoadScene evt)
        {
            pauseService.SetPaused(false);

            // 清资源侧冷却期（保留预加载条目，不动 refCount>0）：须在 LoadScene 前，否则新场景命中上一场景高频缓存
            AssetModule.OnSceneSwitch();

            if (!evt.isAsync)
            {
                //切场景清除事件的话，跨场景不销毁的面板的事件也会被清掉
                //EventBus.ClearAll();
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

        /// <summary>
        /// 真正切换场景
        /// </summary>
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

            //EventBus.ClearAll();
        }

        public void Dispose()
        {
            EventBus<RequestLoadScene>.Unsubscribe(LoadAsync);
            EventBus<RequestActiveScene>.Unsubscribe(Active);
        }
    }
}
