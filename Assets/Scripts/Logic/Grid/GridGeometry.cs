using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子几何：<b>纯数据</b>的"世界坐标 ↔ 格子坐标"换算。正方形格子，按角点定义：<see cref="Origin"/> 是格 <c>(0,0)</c> 的左下角世界坐标，与 Unity <c>Tilemap.CellToWorld(Vector3Int.zero)</c> 同义。</summary>
    /// <remarks>构造时 <c>cellSize ≤ 0</c> / NaN / Inf 一律存 0。换算口径：<see cref="IsValid"/> 为 false 时不钳位、不换算，一律返回零值 —— "没接线"必须是安全降级，不能把角色钉在原点。</remarks>
    public readonly struct GridGeometry
    {
        public readonly Vector2 Origin;

        public readonly float CellSize;

        public bool IsValid => CellSize > 0f;

        public GridGeometry(Vector2 origin, float cellSize)
        {
            Origin = origin;
            CellSize = cellSize > 0f && !float.IsNaN(cellSize) && !float.IsInfinity(cellSize) ? cellSize : 0f;
        }

        /// <summary>世界坐标 → 格子坐标：向下取整，落在格内任意位置都给同一格。负坐标必须向下取整：用 <c>(int)</c> 截断会让 x=0 左侧那一格与 x=0 那一格重叠。</summary>
        public Vector3Int WorldToCell(Vector2 world)
        {
            if (!IsValid) return Vector3Int.zero;

            float x = (world.x - Origin.x) / CellSize;
            float y = (world.y - Origin.y) / CellSize;

            return new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
        }

        /// <summary>格子坐标 → 该格中心的世界坐标（<see cref="Origin"/> 是角点，不是中心）。</summary>
        public Vector2 CellCenter(Vector3Int cell)
        {
            if (!IsValid) return Vector2.zero;

            return new Vector2(
                Origin.x + (cell.x + 0.5f) * CellSize,
                Origin.y + (cell.y + 0.5f) * CellSize);
        }
    }
}
