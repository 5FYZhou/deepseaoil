using DeepseaOil.Data;

namespace DeepseaOil.Logic.Level
{
    public interface ILevelReader
    {
        int CurrentLevelId { get; }

        int NextLevelId { get; }

        WavePlayMode PlayMode { get; }
    }
}
