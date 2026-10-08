using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子几何：纯数据的坐标换算。</summary>
    /// <remarks>构造时 cellSize ≤ 0 / NaN / Inf 一律存 0。IsValid 为 false 时不换算</remarks>
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

        /// <summary>世界坐标 → 格子坐标：向下取整，负坐标也必须向下取整，用 (int) 截断会与 x=0 那格重叠</summary>
        public Vector3Int WorldToCell(Vector2 world)
        {
            if (!IsValid) return Vector3Int.zero;

            float x = (world.x - Origin.x) / CellSize;
            float y = (world.y - Origin.y) / CellSize;

            return new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
        }

        /// <summary>格子坐标 → 该格中心的世界坐标（Origin 是角点，不是中心）</summary>
        public Vector2 CellCenter(Vector3Int cell)
        {
            if (!IsValid) return Vector2.zero;

            return new Vector2(
                Origin.x + (cell.x + 0.5f) * CellSize,
                Origin.y + (cell.y + 0.5f) * CellSize);
        }
    }
}
