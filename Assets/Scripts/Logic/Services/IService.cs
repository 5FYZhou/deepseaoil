using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Service
{
    public interface IService
    {
        void Init();
        void Tick(float unscaledDeltaTime);
        void Dispose();
    }
}
