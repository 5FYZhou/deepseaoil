using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation.Element
{
    public interface IEffect
    {
        void Effect();
    }

    public enum TileType
    {
        None,
        Water,
        Soil,
        Fire,
        Ice,
        Elec,
        Plant
    }

    public class EfcTileChange : IEffect
    {
        public TileType tileType;

        public void Effect()
        {
            // 目标地块（图片）类型变为tileType
        }
    }

    public class EfcSlowDown : IEffect
    {
        public float duration;
        public float MUD_SLOW_FACTOR;

        public void Effect()
        {
            // 给该地块的敌人施加减速
        }
    }
}
 