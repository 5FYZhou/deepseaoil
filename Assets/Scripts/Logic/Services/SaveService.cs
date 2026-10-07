using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Service
{
    public struct GameData
    {

    }

    public class SaveService : IService
    {
        private GameData _data;

        public SaveService()
        {
        }

        public void Init()
        {
        }

        /// <remarks>本轮是壳：存档系统的实做留给"存档重构"那一轮（§12 定案）。</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void Save(GameData data)
        {
            
        }

        public GameData Load()
        {
            return _data;
        }

        public void Dispose()
        {
        }
    }
}
