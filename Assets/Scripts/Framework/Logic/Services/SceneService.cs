using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Events;

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

        // ④ 换场景
        UnityEngine.SceneManagement.SceneManager
            .LoadScene(sceneName);
    }

    public void Dispose()
    {
    }
}