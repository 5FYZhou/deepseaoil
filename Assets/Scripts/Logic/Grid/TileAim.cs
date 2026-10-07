using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    // 瞄准吸附的静态纯函数：鼠标世界点 → 格子，不吃 Time、不吃 Camera、不吃 Tilemap。
    // 它算出来的格子必须就是球真正会落的那一格：投掷与指示器共用本函数，各写一份必然漂移，
    // 表现是"看着能扔到、其实扔不到"。
    // 优先支是"鼠标所在格的中心在射程内"；超距才沿线回退（而不是把鼠标点夹到最远距离），
    // 先夹距离再取整会让贴边时落点跳格、指示器在边界上抖。
    // 回退步长 0.1 决定边界附近取到哪一格；MaxSteps 是防"射程输入异常大"时循环失控。
    // 鼠标压在出手点上或几何非法时返回 false；maxDistance 非法值按"不限"处理。
    public static class TileAim
    {
        /// <summary>超距回退的搜索步长（世界单位）。</summary>
        public const float StepSize = 0.1f;

        /// <summary>回退搜索的最大步数；<c>0.1 × 4096 ≈ 409 米</c>，远超任何合理射程。</summary>
        private const int MaxSteps = 4096;

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
