using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Events;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Service
{
    public sealed class SceneService : IService
    {
        private readonly PauseService pauseService;

        public SceneService(PauseService pauseService)
        {
            this.pauseService = pauseService;
        }

        public void Init()
        {
            EventBus<RequestChangeScene>.Subscribe(Load);
        }

        /// <remarks>无每帧工作，切场景由事件驱动（Load）</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void Load(RequestChangeScene evt)
        {
            pauseService.SetPaused(false);

            EventBus.ClearAll();

            // 清资源侧冷却期（保留预加载条目，不动 refCount>0）：须在 LoadScene 前，否则新场景命中上一场景高频缓存
            AssetModule.OnSceneSwitch();

            UnityEngine.SceneManagement.SceneManager.LoadScene(evt.sceneName);
        }

        public void Dispose()
        {
            EventBus<RequestChangeScene>.Unsubscribe(Load);
        }
    }
}
