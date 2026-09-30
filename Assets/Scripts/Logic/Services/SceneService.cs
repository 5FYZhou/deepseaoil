using DeepseaOil.Data;
using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Service
{
    public sealed class SceneService : IService
    {
        private readonly IGameTime gameTime;
        private readonly PauseService pauseService;

        public SceneService(
            IGameTime gameTime,
            PauseService pauseService)
        {
            this.gameTime = gameTime;
            this.pauseService = pauseService;
        }

        public void Init()
        {
        }

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void Load(string sceneName)
        {
            // ① 恢复时间
            gameTime.SetTimeScale(1f);

            // ② 恢复 PauseService
            pauseService.SetPaused(false);

            // ③ 清理事件
            EventBus.ClearAll();

            // ④ 清资源侧冷却期（保留预加载条目，不动 refCount > 0 的）
            //    必须在 LoadScene 之前：否则新场景会命中上一场景的高频缓存
            AssetModule.OnSceneSwitch();

            // ⑤ 换场景
            UnityEngine.SceneManagement.SceneManager
                .LoadScene(sceneName);
        }

        public void Dispose()
        {
        }
    }
}
