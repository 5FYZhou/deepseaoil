using System.Collections.Generic;
using System.Text;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>把场景预制体的碰撞体一次性烘焙成逻辑层阻挡格；不烘焙则敌人只认格子障碍</summary>
    /// <remarks>按身体半径查询格心周围：半径内的碰撞体占用该格，否则敌人站在格心也会蹭墙。热路径不查询物理。</remarks>
    public static class SceneObstacleBaker
    {
        /// <summary>单格查询上限：超出会漏判，宁可报错也不要静默漏</summary>
        private const int HitCapacity = 16;

        private static readonly Collider2D[] Hits = new Collider2D[HitCapacity];

        /// <summary>格 → 判它占用的碰撞体，仅供日志归因</summary>
        private static readonly Dictionary<Vector3Int, Collider2D> Sources = new();

        /// <summary>命中却因"是角色"被排除的碰撞体，仅供日志归因</summary>
        private static readonly List<Collider2D> Rejected = new();

        /// <summary>烘焙已登记格的阻挡；margin=角色碰撞半径。返回新标记的格数，重复调用先清空旧结果</summary>
        public static int Bake(GridLogic grid, float margin)
        {
            if (grid == null || !grid.Geometry.IsValid) return 0;

            grid.ClearSolidCells();

            Sources.Clear();
            Rejected.Clear();

            GridGeometry geometry = grid.Geometry;
            int marked = 0;

            foreach (Vector3Int cell in grid.Cells)
            {
                if (!IsCellSolid(geometry, cell, margin, out Collider2D source)) continue;

                grid.MarkSolidCell(cell);
                Sources[cell] = source;
                marked++;
            }

            LogBake(grid, marked, margin);

            return marked;
        }

        /// <remarks>查询盒 = 格宽 + 2×半径，命中后按最近点距离复判：包围盒相交不算，仅接触也不算</remarks>
        private static bool IsCellSolid(
            in GridGeometry geometry,
            Vector3Int cell,
            float margin,
            out Collider2D source)
        {
            source = null;

            Vector2 center = geometry.CellCenter(cell);
            float extent = geometry.CellSize * 0.5f + Mathf.Max(0f, margin);

            int count = Physics2D.OverlapBoxNonAlloc(center, new Vector2(extent * 2f, extent * 2f), 0f, Hits);

            if (count >= HitCapacity)
            {
                Debug.LogError(
                    $"[ObstacleBake] 格 {cell} 的碰撞体查询命中 {count} 个，已达上限 {HitCapacity}：" +
                    "结果可能漏判。排查该处是否叠了过多碰撞体。");
            }

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = Hits[i];

                if (hit == null) continue;

                if (!IsBlocking(hit))
                {
                    if (!Rejected.Contains(hit)) Rejected.Add(hit);

                    continue;
                }

                if (Vector2.Distance(center, hit.ClosestPoint(center)) > margin) continue;

                source = hit;

                return true;
            }

            return false;
        }

        /// <remarks>会动的角色一律不算障碍：IManagedActor 认标记，IActorMotor 兜住标记与碰撞体不同级的预制体</remarks>
        private static bool IsBlocking(Collider2D collider)
        {
            if (collider == null || collider.isTrigger) return false;

            // 阻挡地貌已由格状态表达，再烘一次会在日志里冒充"场景预制体"，误导出错误归因
            if (collider.GetComponentInParent<TileObstacleDirector>() != null) return false;

            if (collider.GetComponentInParent<IManagedActor>() != null) return false;

            if (collider.GetComponentInParent<IActorMotor>() != null) return false;

            Rigidbody2D body = collider.attachedRigidbody;

            return body == null || body.isKinematic;
        }

        /// <remarks>常态只报总数与排除口径：43 格逐格带名字会淹掉 Console。逐格归因加 LOG_BAKE_DETAIL 编译符号</remarks>
        private static void LogBake(GridLogic grid, int marked, float margin)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var report = new StringBuilder();

            report.Append("[ObstacleBake] 半径 ").Append(margin.ToString("F2"))
                .Append(" 占用 ").Append(marked).Append(" / ").Append(grid.CellCount).Append(" 格");

            report.Append("；已排除 ").Append(Rejected.Count).Append(" 个非障碍碰撞体");

#if LOG_BAKE_DETAIL
            report.Append("：");

            foreach (Vector3Int cell in grid.Cells)
            {
                if (!grid.IsSolidCell(cell)) continue;

                report.Append('(').Append(cell.x).Append(',').Append(cell.y).Append(")<-")
                    .Append(Sources.TryGetValue(cell, out Collider2D src) && src != null ? src.gameObject.name : "?")
                    .Append(' ');
            }

            if (Rejected.Count > 0)
            {
                report.Append("| 已排除角色碰撞体：");

                for (int i = 0; i < Rejected.Count; i++)
                {
                    if (i > 0) report.Append(',');

                    report.Append(Rejected[i].gameObject.name);
                }
            }
#endif

            Debug.Log(report.ToString());
#endif
        }
    }
}
