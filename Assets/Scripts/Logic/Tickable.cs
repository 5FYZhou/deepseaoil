using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic
{
    public interface IFixedTickable
    {
        void FixedTick(LogicContext ctx)
        {

        }
    }

    public interface ITickable
    {
        void Tick(UILogicContext ctx)
        {

        }
    }
}
