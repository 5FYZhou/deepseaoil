using DeepSeaOil.Logic.Service;
using UnityEngine;

namespace DeepSeaOil.Presentation
{
    public sealed class GameTime : IGameTime
    {
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        public float TimeScale => Time.timeScale;

        public void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
        }
    }
}
