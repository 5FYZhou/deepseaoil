using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 格子几何：<b>纯数据</b>的"世界坐标 ↔ 格子坐标"换算。正方形格子，按角点定义。
    /// </summary>
    /// <remarks>
    /// <b>为什么要自己算，而不用 <c>Tilemap.WorldToCell</c>：</b>逻辑层不许碰 <c>Tilemap</c>
    /// （它绑在渲染与物理模块上，一旦出现，格子系统的任何测试都要建场景）。
    /// 组合根在表现层从 <c>Tilemap</c> 读一次 <c>cellSize</c> 与角点填进来 ——
    /// 折算只发生在一处，逻辑层拿到的永远是纯数据，与 <c>BoundsArea</c> 对 <c>BoxCollider2D</c> 的做法一致。
    /// <para><b>角点而不是中心：</b><see cref="Origin"/> 是格 <c>(0,0)</c> 的<b>左下角</b>世界坐标，
    /// 与 Unity <c>Tilemap.CellToWorld(Vector3Int.zero)</c> 同义。用中心会被"半个格"的偏移咬到，
    /// 而那种偏差表现为"落点整体偏半格"，很难从现象倒推。</para>
    /// <para><b><see cref="IsValid"/> 为 false 时不钳位、不换算</b>：与 <c>BoundsArea</c> 同一条纪律 ——
    /// "没接线"必须是安全降级而不是把角色钉在原点。</para>
    /// </remarks>
    public readonly struct GridGeometry
    {
        /// <summary>格 <c>(0,0)</c> 的左下角世界坐标。</summary>
        public readonly Vector2 Origin;

        /// <summary>正方形格子的边长（世界单位）。</summary>
        public readonly float CellSize;

        /// <summary>几何是否可用；<c>false</c> 时所有换算返回零值。</summary>
        public bool IsValid => CellSize > 0f;

        public GridGeometry(Vector2 origin, float cellSize)
        {
            Origin = origin;
            CellSize = cellSize > 0f && !float.IsNaN(cellSize) && !float.IsInfinity(cellSize) ? cellSize : 0f;
        }

        /// <summary>世界坐标 → 格子坐标（向下取整，落在格内任意位置都给同一格）。</summary>
        public Vector3Int WorldToCell(Vector2 world)
        {
            if (!IsValid) return Vector3Int.zero;

            float x = (world.x - Origin.x) / CellSize;
            float y = (world.y - Origin.y) / CellSize;

            // 负坐标必须向下取整：Mathf.FloorToInt 与 (int) 截断在 -0.5 处给出不同答案，
            // 用截断会让 x=0 左侧那一格与 x=0 那一格重叠。
            return new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
        }

        /// <summary>格子坐标 → 该格<b>中心</b>的世界坐标。</summary>
        public Vector2 CellCenter(Vector3Int cell)
        {
            if (!IsValid) return Vector2.zero;

            return new Vector2(
                Origin.x + (cell.x + 0.5f) * CellSize,
                Origin.y + (cell.y + 0.5f) * CellSize);
        }
    }
}
