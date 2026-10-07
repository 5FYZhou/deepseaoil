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

        /// <remarks>它没有每帧要做的事：切场景由事件驱动（<see cref="Load"/>），本方法只是把接口补齐。</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void Load(RequestChangeScene evt)
        {
            // ① 恢复时间( pauseService.SetPaused已有
            //gameTime.SetTimeScale(1f);

            // ② 恢复 PauseService
            pauseService.SetPaused(false);

            // ③ 清理事件
            EventBus.ClearAll();

            // ④ 清资源侧冷却期（保留预加载条目，不动 refCount > 0 的）
            //    必须在 LoadScene 之前：否则新场景会命中上一场景的高频缓存
            AssetModule.OnSceneSwitch();

            // ⑤ 换场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(evt.sceneName);
        }

        public void Dispose()
        {
            EventBus<RequestChangeScene>.Unsubscribe(Load);
        }
    }
}
