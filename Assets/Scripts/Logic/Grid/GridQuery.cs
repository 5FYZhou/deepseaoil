using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 格子的空间查询：邻接与范围。<b>静态纯函数</b>，只吃几何，不查任何持有状态。
    /// </summary>
    /// <remarks>
    /// <b>为什么先把它们写出来（当前零消费者）：</b>格子系统立项时的明确目标是"只实现泥浆格、
    /// 但骨架完整"，而"一格的状态影响另一格"（联动 / 蔓延 / 连锁）必然要问这三个问题。
    /// 它们都是纯函数，可测、无状态，先立在这里比将来塞进 <c>GridLogic</c> 再拆便宜。
    /// <para><b>全部走"调用方给缓冲"的形状</b>：这三个函数将来会在一次状态转换里被连续调用，
    /// 返回新 List 会让每次落地都产生垃圾；缓冲由调用方复用。</para>
    /// <para><b>缓冲区会先被 <c>Clear</c></b>：调用方不需要自己清，但也不要指望它保留旧内容。</para>
    /// </remarks>
    public static class GridQuery
    {
        /// <summary>四邻（上下左右），不含自己。</summary>
        public static void GetNeighbors4(Vector3Int cell, List<Vector3Int> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();
            buffer.Add(new Vector3Int(cell.x + 1, cell.y, cell.z));
            buffer.Add(new Vector3Int(cell.x - 1, cell.y, cell.z));
            buffer.Add(new Vector3Int(cell.x, cell.y + 1, cell.z));
            buffer.Add(new Vector3Int(cell.x, cell.y - 1, cell.z));
        }

        /// <summary>八邻（含对角），不含自己。</summary>
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

        /// <summary>
        /// 以 <paramref name="center"/> 为中心、半径 <paramref name="radius"/> 内的<b>格心</b>集合（含中心格自己）。
        /// </summary>
        /// <param name="geometry">格子几何。</param>
        /// <param name="center">中心格。</param>
        /// <param name="radius">半径（世界单位）；<c>0</c> 时只返回中心格。</param>
        /// <param name="buffer">输出缓冲，会先被清空。</param>
        /// <remarks>
        /// 判据是"<b>格心</b>到中心的距离"，不是"格与圆相交"：格子是离散的，
        /// 按相交判会让斜角上多出一圈只沾到一点边的格，且与画出来的圆看起来对不上
        /// （白模的落地圈曾经栽在"相交语义比画出来的圈大一个身位"上）。
        /// </remarks>
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

            // 按包围盒枚举，不按"半径 / 格边长"估格数：格子边长与实际半径的比例关系
            // 在调用方那边不可见，估错会漏格（而漏格不报错，只是"范围比看起来小"）。
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
