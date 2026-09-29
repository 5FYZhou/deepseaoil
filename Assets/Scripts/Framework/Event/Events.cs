using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSeaOil.Logic.Events
{
    // Intent
    public readonly struct RequestPause { }

    public readonly struct RequestResume { }


    // Fact

    public readonly struct GamePaused { }

    public readonly struct GameResumed { }
}
