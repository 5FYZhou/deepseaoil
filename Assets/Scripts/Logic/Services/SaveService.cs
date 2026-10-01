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

        public void Tick(float unscaledDeltaTime)
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
