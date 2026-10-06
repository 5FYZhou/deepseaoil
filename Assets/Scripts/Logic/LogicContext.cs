using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Input;


namespace DeepseaOil.Logic
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

    public readonly struct UILogicContext
    {
        public readonly UIInputSnapshot inputSnapshot;
        // 当前场景
        public readonly GameState gameState; 

        public UILogicContext(UIInputSnapshot inputSnapshot, GameState state)
        {
            this.inputSnapshot = inputSnapshot;
            this.gameState = state;
        }
    }
}
