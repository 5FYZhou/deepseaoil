using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Events;

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

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void Load(RequestChangeScene evt)
        {
            // ① 恢复时间 pauseService.SetPaused已有
            //gameTime.SetTimeScale(1f);

            // ② 恢复 PauseService
            pauseService.SetPaused(false);

            // ③ 清理事件
            EventBus.ClearAll();

            // ④ 换场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(evt.sceneName);
        }

        public void Dispose()
        {
            EventBus<RequestChangeScene>.Unsubscribe(Load);
        }
    }
}
