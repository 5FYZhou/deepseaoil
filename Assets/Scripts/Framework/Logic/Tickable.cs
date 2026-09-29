using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSeaOil.Logic
{
    public interface IFixedTickable
    {
        void FixedTick(LogicContext ctx)
        {

        }
    }

    public interface ITickable
    {
        void Tick(LogicContext ctx)
        {

        }
    }
}
