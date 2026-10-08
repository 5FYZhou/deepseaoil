using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    // 瞄准吸附静态纯函数：鼠标世界点→格子，不吃 Time/Camera/Tilemap。
    // 投掷与指示器必须共用，否则看着能扔到其实扔不到；先夹距离再取整会贴边跳格。
    // 几何非法、压出手点或无解时返回 false。
    public static class TileAim
    {
        /// <summary>回退步长（世界单位）</summary>
        public const float StepSize = 0.1f;

        /// <summary>回退最大步数，0.1×4096≈409 米</summary>
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

            // 鼠标压在出手点方向不可定义，返回 false
            if (direction.sqrMagnitude < 1e-6f) return false;

            direction.Normalize();

            float reach = Sanitize(maxDistance);

            // ① 鼠标格中心在射程内就用它
            Vector3Int mouseCell = geometry.WorldToCell(mouseWorld);

            if (Vector2.Distance(origin, geometry.CellCenter(mouseCell)) <= reach)
            {
                cell = mouseCell;
                return true;
            }

            // ② 超距从最远处往回找第一个中心在射程内的格
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

        /// <summary>非法射程（NaN/无穷/≤0）按不限处理</summary>
        private static float Sanitize(float maxDistance)
        {
            if (float.IsNaN(maxDistance) || float.IsInfinity(maxDistance) || maxDistance <= 0f) return 1e4f;

            return maxDistance;
        }
    }
}
