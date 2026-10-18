using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic
{
    public interface IGameStateRequest
    {
        /// <summary>请求切到某状态，是否真切由实现方判定</summary>
        void RequestState(GameState state);

        void Resume();
    }
}
