using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Events
{
    // Intent
    public readonly struct RequestPause { }

    public readonly struct RequestResume { }

    public readonly struct RequestChangeScene 
    {
        public readonly string sceneName;
        public RequestChangeScene(string n) { sceneName = n; }
    }


    // Fact
    public readonly struct GamePaused { }

    public readonly struct GameResumed { }
}
