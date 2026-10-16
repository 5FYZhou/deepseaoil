#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DeepseaOil.Logic.Grid;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <summary>导航诊断：把净空与流场画成 Gizmo，用来肉眼核对"这一步该去哪"</summary>
    /// <remarks>挂在 CombatRoot 同一个物体上、**选中它**才画；只读不写。橙色 = 该半径格内一个站得住的采样点都没有，暗红 = 站得住但连不上目标格（流场没覆盖，怪顶墙的典型长相），青色箭头 = 该格会往哪走，黄圈 = 目标格。半径不同的怪各有一张场，fieldIndex 选一张画；drawSamples 打开后只在目标格附近画采样点（站得住绿、站不住红）。</remarks>
    [AddComponentMenu("DeepseaOil/Nav Field Gizmos")]
    public sealed class NavFieldGizmos : MonoBehaviour
    {
        [Tooltip("画流场的下一步箭头")]
        [SerializeField] private bool drawField = true;

        [Tooltip("画站不住 / 连不上的格")]
        [SerializeField] private bool drawClearance = true;

        [Tooltip("画目标格附近的采样点：绿=站得住，红=站不住")]
        [SerializeField] private bool drawSamples;

        [Tooltip("采样点画多大范围（格，以目标格为中心）")]
        [SerializeField] private int sampleWindow = 6;

        [Tooltip("画哪一张场：按场的创建顺序（哪种怪先出现就是第几张）")]
        [SerializeField] private int fieldIndex;

        [Tooltip("箭头长度占格宽比例")]
        [SerializeField] private float arrowScale = 0.4f;

        private void OnDrawGizmosSelected()
        {
            var root = GetComponent<CombatRoot>();

            GridLogic grid = root != null ? root.Grid : null;

            if (grid == null || !grid.Geometry.IsValid) return;

            NavMap map = grid.Nav;

            if (map == null || !map.IsValid) return;

            NavField field = grid.NavFields.FieldAt(fieldIndex);

            GridGeometry geometry = grid.Geometry;
            int walkable = 0;
            int reached = 0;

            foreach (Vector3Int cell in grid.Cells)
            {
                Vector3 center = geometry.CellCenter(cell);

                bool standable = field == null
                    ? !grid.IsObstacle(cell)
                    : map.IsWalkable(cell, field.Radius, field.Skin) && !grid.IsObstacle(cell);

                if (!standable)
                {
                    if (drawClearance)
                    {
                        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
                        Gizmos.DrawWireCube(center, Vector3.one * geometry.CellSize * 0.8f);
                    }

                    continue;
                }

                walkable++;

                bool linked = field != null && field.CellDistOf(cell) >= 0;

                if (linked) reached++;

                // 站得住却连不上目标：场没覆盖到这一片，怪会退回直线顶着走
                if (!linked)
                {
                    if (drawClearance)
                    {
                        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.6f);
                        Gizmos.DrawWireCube(center, Vector3.one * geometry.CellSize * 0.5f);
                    }

                    continue;
                }

                if (!drawField) continue;

                DrawCellArrow(map, field, cell, geometry);
            }

            if (field == null) return;

            if (drawSamples) DrawSamples(grid, map, field, geometry);

            Vector3 goal = geometry.CellCenter(field.Goal);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(goal, geometry.CellSize * 0.5f);

#if UNITY_EDITOR
            Handles.Label(
                goal + Vector3.up * 0.6f,
                $"半径 {field.Radius:F2} + 皮厚 {field.Skin:F2}\n可站 {walkable} / 已连 {reached} / 全图 {map.CellCount}\n采样间距 {map.Spacing:F2} / 场重建 {grid.NavFields.TotalRebuilds} 次");
#endif
        }

        /// <summary>一格的箭头：取本格里离目标最远的可站采样点，指到它的下一步</summary>
        /// <remarks>取最远的那个点而不是格心：格心常常正好被栅栏压住，画出来会看不出这一格往哪走。</remarks>
        private void DrawCellArrow(NavMap map, NavField field, Vector3Int cell, in GridGeometry geometry)
        {
            if (!map.TryIndex(cell, out int cellIndex)) return;

            int best = -1;
            int bestDist = -1;

            for (int k = 0; k < NavMap.SamplesPerCell; k++)
            {
                int index = map.SampleIndexOf(cellIndex, k % NavMap.Subdivision, k / NavMap.Subdivision);
                int dist = field.DistOf(index);

                if (dist <= bestDist) continue;

                bestDist = dist;
                best = index;
            }

            if (best < 0) return;
            if (!field.TryGetStep(best, out int next)) return;

            Vector3 from = map.SampleCenter(best);
            Vector3 to = map.SampleCenter(next);
            Vector3 tip = from + (to - from) * arrowScale;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(from, tip);
            Gizmos.DrawSphere(tip, geometry.CellSize * 0.06f);
        }

        /// <summary>目标格附近的采样点：站得住绿、站不住红；离得远的格子不画，几万个点会把编辑器拖死</summary>
        private void DrawSamples(GridLogic grid, NavMap map, NavField field, in GridGeometry geometry)
        {
            Vector3Int goal = field.Goal;
            float size = geometry.CellSize * 0.08f;

            for (int dy = -sampleWindow; dy <= sampleWindow; dy++)
            {
                for (int dx = -sampleWindow; dx <= sampleWindow; dx++)
                {
                    var cell = new Vector3Int(goal.x + dx, goal.y + dy, 0);

                    if (!map.TryIndex(cell, out int cellIndex)) continue;

                    for (int j = 0; j < NavMap.Subdivision; j++)
                    {
                        for (int i = 0; i < NavMap.Subdivision; i++)
                        {
                            int index = map.SampleIndexOf(cellIndex, i, j);
                            bool free = map.IsSampleFree(index, field.Radius, field.Skin) && !grid.IsObstacle(cell);

                            Gizmos.color = free ? new Color(0.2f, 1f, 0.3f, 0.8f) : new Color(1f, 0.2f, 0.2f, 0.8f);
                            Gizmos.DrawWireCube(map.SampleCenter(index), Vector3.one * size);
                        }
                    }
                }
            }
        }
    }
}
#endif
