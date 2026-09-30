
namespace DeepseaOil.Logic.Service
{
    public interface IGameTime
    {
        void SetTimeScale(float scale);

        float UnscaledDeltaTime { get; }

        float TimeScale { get; }
    }


}