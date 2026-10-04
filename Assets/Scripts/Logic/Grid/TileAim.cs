using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 瞄准吸附：鼠标世界点 → <b>格子</b>。<b>静态纯函数</b>，不吃 Time、不吃 Camera、不吃 Tilemap。
    /// </summary>
    /// <remarks>
    /// <b>它算出来的格子必须就是球真正会落的那一格。</b>所以投掷与指示器共用本函数，
    /// 不是"两份长得差不多的数学"—— 各写一份必然会漂移，表现是"看着能扔到、其实扔不到"。
    /// <para><b>为什么是"先取鼠标格、超距再沿线回退"而不是"把鼠标点夹到最远距离"：</b>
    /// 需求要的是落点<b>吸附到格子中心</b>。先夹距离再取整会让贴边时落点跳格、
    /// 指示器在边界上抖；沿"玩家 → 鼠标"方向从最远处往回找<b>第一个中心点落在射程内的格</b>，
    /// 得到的格子稳定，且与鼠标指向同侧。</para>
    /// <para><b>回退步长 0.1 与 <see cref="MaxSteps"/> 是刻意写死的：</b>步长决定边界附近取到哪一格
    /// （步长越小越贴近真实射程边界），上限只是防"射程输入异常大"时循环失控。</para>
    /// </remarks>
    public static class TileAim
    {
        /// <summary>超距回退的搜索步长（世界单位）。</summary>
        public const float StepSize = 0.1f;

        /// <summary>回退搜索的最大步数；<c>0.1 × 4096 ≈ 409 米</c>，远超任何合理射程。</summary>
        private const int MaxSteps = 4096;

        /// <summary>
        /// 玩家位置 + 鼠标世界点 → 吸附后的落点格。
        /// </summary>
        /// <param name="geometry">格子几何。非法时返回 <c>false</c>。</param>
        /// <param name="origin">出手点（玩家位置）。</param>
        /// <param name="mouseWorld">鼠标世界坐标（已换算到地面平面）。</param>
        /// <param name="maxDistance">最大投掷距离（世界单位）；非法值按"不限"处理。</param>
        /// <param name="cell">吸附后的格子。</param>
        /// <returns>拿到可用格子为 <c>true</c>；鼠标压在出手点上或几何非法时为 <c>false</c>。</returns>
        public static bool TryGetAimCell(
            in GridGeometry geometry,
            Vector2 origin,
            Vector2 mouseWorld,
            float maxDistance,
            out Vector3Int cell)
        {
            cell = default;

            if (!geometry.IsValid) return false;

            Vector2 direction = mouseWorld - origin;

            // 鼠标压在出手点上：方向不可定义。返回 false 而不是硬塞一个方向 ——
            // 硬塞会让指示器在玩家脚下跳来跳去。
            if (direction.sqrMagnitude < 1e-6f) return false;

            direction.Normalize();

            float reach = Sanitize(maxDistance);

            // ① 鼠标所在格的中心若在射程内，就用它（最常见的一支）。
            Vector3Int mouseCell = geometry.WorldToCell(mouseWorld);

            if (Vector2.Distance(origin, geometry.CellCenter(mouseCell)) <= reach)
            {
                cell = mouseCell;
                return true;
            }

            // ② 超距：沿"出手点 → 鼠标"从最远处往回找第一个中心点在射程内的格。
            float maxSteps = Mathf.Min(MaxSteps, reach / StepSize + 1f);

            for (float step = 0f; step <= maxSteps; step += 1f)
            {
                float distance = reach - step * StepSize;

                if (distance < 0f) break;

                Vector2 point = origin + direction * distance;
                Vector3Int candidate = geometry.WorldToCell(point);

                if (Vector2.Distance(origin, geometry.CellCenter(candidate)) <= reach)
                {
                    cell = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>非法射程（<c>NaN</c> / 无穷 / <c>≤ 0</c>）换成一个"相当于不限"的大值。</summary>
        private static float Sanitize(float maxDistance)
        {
            if (float.IsNaN(maxDistance) || float.IsInfinity(maxDistance) || maxDistance <= 0f) return 1e4f;

            return maxDistance;
        }
    }
}
