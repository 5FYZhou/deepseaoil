using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepSeaOil.Logic.Input;


namespace DeepSeaOil.Logic
{
    public readonly struct LogicContext
    {
        public readonly float now;
        public readonly float deltaTime;

        public readonly WorldInfo worldInfo;
        public readonly InputSnapshot inputSnapshot;

        public LogicContext(float now, float deltaTime, in WorldInfo worldInfo, in InputSnapshot inputSnapshot)
        {
            this.now = now;
            this.deltaTime = deltaTime;
            this.worldInfo = worldInfo;
            this.inputSnapshot = inputSnapshot;
        }
    }

    public readonly struct TimeContext
    {
        public readonly float scaledDt;
        public readonly float unscaledDt;

        public TimeContext(float scaledDt, float unscaledDt)
        {
            this.scaledDt = scaledDt;
            this.unscaledDt = unscaledDt;
        }
    }
}
