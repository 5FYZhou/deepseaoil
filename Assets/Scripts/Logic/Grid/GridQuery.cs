using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子的空间查询：邻接与范围。静态纯函数，只吃几何，不查任何持有状态。</summary>
    /// <remarks>当前零消费者：为"一格的状态影响另一格"（联动 / 蔓延 / 连锁）预留，纯函数、可测。三个函数都走"调用方给缓冲"（返回新 List 会让每次落地都产生垃圾），缓冲区先被 <c>Clear</c>，调用方不必自己清。</remarks>
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

        /// <summary>以 <paramref name="center"/> 为中心、半径 <paramref name="radius"/> 内的格心集合（含中心格自己）。</summary>
        /// <param name="radius">半径（世界单位）；<c>≤ 0</c> 或非数时只返回中心格（几何非法时同样）。</param>
        /// <remarks>判据是"格心到中心的距离"，不是"格与圆相交"：按相交判会在斜角上多出一圈只沾到一点边的格，与画出来的圆对不上。</remarks>
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

            // 按包围盒枚举，不按"半径 / 格边长"估格数：估错会漏格（漏格不报错，只是"范围比看起来小"）。
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
