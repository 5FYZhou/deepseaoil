using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Drop
{
    /// <summary>
    /// 一次掉落物产出请求：<b>产什么、从哪出、落在哪</b>。
    /// </summary>
    /// <remarks>
    /// <b>"怎么产"不在这里</b>（审查已定：产什么 / 怎么产 / 何时产都由产出方自己判定）——
    /// 所以本结构体只有结果（起点与落点），没有"随机半径"这类产法参数：
    /// 喷泉的落点散布是它自己的事，敌人的掉落也许是"原地生成"。
    /// <para><b>为什么不带"产出方是谁"：</b>持有者（<c>DropDirector</c>）只负责造、持、驱、清，
    /// 不判断掉落规则；知道得越少，产出方越自由。</para>
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
