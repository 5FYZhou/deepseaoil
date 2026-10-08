using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子空间查询：邻接与范围，静态纯函数；当前零消费者，为格间联动预留；缓冲由调用方给，先被 Clear</summary>
    public static class GridQuery
    {
        public static void GetNeighbors4(Vector3Int cell, List<Vector3Int> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();
            buffer.Add(new Vector3Int(cell.x + 1, cell.y, cell.z));
            buffer.Add(new Vector3Int(cell.x - 1, cell.y, cell.z));
            buffer.Add(new Vector3Int(cell.x, cell.y + 1, cell.z));
            buffer.Add(new Vector3Int(cell.x, cell.y - 1, cell.z));
        }

        public static void GetNeighbors8(Vector3Int cell, List<Vector3Int> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    buffer.Add(new Vector3Int(cell.x + dx, cell.y + dy, cell.z));
                }
            }
        }

        /// <summary>以 center 为中心、半径 radius（世界单位）内的格心集合，含中心格；radius≤0、非数或几何非法时只返回中心格；判据是格心距离而非格与圆相交</summary>
        public static void GetWithinRadius(
            in GridGeometry geometry,
            Vector3Int center,
            float radius,
            List<Vector3Int> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();
            buffer.Add(center);

            if (!geometry.IsValid) return;

            if (float.IsNaN(radius) || radius <= 0f) return;

            Vector2 centerWorld = geometry.CellCenter(center);

            // 按包围盒枚举，不按半径/格边长估格数：估错会漏格
            int span = Mathf.CeilToInt(radius / geometry.CellSize);

            for (int dy = -span; dy <= span; dy++)
            {
                for (int dx = -span; dx <= span; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    var candidate = new Vector3Int(center.x + dx, center.y + dy, center.z);

                    if (Vector2.Distance(centerWorld, geometry.CellCenter(candidate)) <= radius)
                    {
                        buffer.Add(candidate);
                    }
                }
            }
        }
    }
}
