using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Drop
{
    /// <summary>
    /// 一次掉落物产出请求：产什么、从哪出、落在哪。
    /// </summary>
    /// <remarks>
    /// 只装结果（起点与落点），不装产法参数，也不带"产出方是谁"：
    /// 怎么产、何时产由产出方决定，持有者只负责造、持、驱、清。
    /// </remarks>
    public readonly struct DropSpawnRequest
    {
        /// <summary>掉落物的种类（决定取值定义与实体实现）。</summary>
        public readonly DropType Type;

        /// <summary>生成点（抛物线起点）。</summary>
        public readonly Vector2 Origin;

        /// <summary>落点（抛物线终点，也是"等待被拾取"时所在的位置）。</summary>
        public readonly Vector2 Landing;

        public DropSpawnRequest(DropType type, Vector2 origin, Vector2 landing)
        {
            Type = type;
            Origin = origin;
            Landing = landing;
        }
    }
}
