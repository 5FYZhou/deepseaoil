using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Service
{
    // 假设先用enum当标识
    public enum audioType
    {
        None = 0,
        Walk
    };

    public interface IAudioService
    {
        void Play(audioType type);
    }
}
