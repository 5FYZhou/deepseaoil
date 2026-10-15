using DeepseaOil.Data;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Logic.Drop
{
    /// <summary>掉落物产出请求，只装类别与起点落点</summary>
    public readonly struct DropSpawnRequest
    {
        public readonly DropType Type;

        /// <summary>抛物线起点</summary>
        public readonly Vector2 Origin;

        /// <summary>抛物线终点，也是等待拾取时的位置</summary>
        public readonly Vector2 Landing;

        public readonly SeedType Seed;

        public DropSpawnRequest(DropType type, Vector2 origin, Vector2 landing, SeedType seed = SeedType.None)
        {
            Type = type;
            Origin = origin;
            Landing = landing;
            Seed = seed;
        }
    }
}
