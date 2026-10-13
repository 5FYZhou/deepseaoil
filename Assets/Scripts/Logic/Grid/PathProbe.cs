using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>限深 BFS 取下一步：只服务敌人绕障，不做全程寻路</summary>
    /// <remarks>找不到或超深度返回 false 且 step=start，调用方退回直线撞墙；缓冲全由调用方持有，本类每次调用零分配。</remarks>
    public static class PathProbe
    {
        /// <summary>搜索深度上限（格），超过即放弃</summary>
        public const int DefaultMaxSteps = 16;

        // 与 GridQuery.GetNeighbors4 同序：下/左/右/上
        private static readonly Vector3Int[] Neighbors4 =
        {
            new Vector3Int(0, -1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
        };

        /// <summary>返回起点走到终点的下一步；几何无效、起点即终点、超 DefaultMaxSteps 都返回 false</summary>
        public static bool TryFindStep(
            GridGeometry geometry,
            Vector3Int start,
            Vector3Int goal,
            System.Func<Vector3Int, bool> isWalkable,
            List<Vector3Int> frontier,
            Queue<Vector3Int> queue,
            HashSet<Vector3Int> visited,
            Dictionary<Vector3Int, Vector3Int> cameFrom,
            out Vector3Int step)
        {
            step = start;

            if (!geometry.IsValid) return false;

            if (isWalkable == null || frontier == null || queue == null) return false;

            if (visited == null || cameFrom == null) return false;

            // 起点即终点没有"下一步"可给
            if (start == goal) return false;

            queue.Clear();
            visited.Clear();
            cameFrom.Clear();

            visited.Add(start);
            queue.Enqueue(start);

            for (int depth = 0; depth < DefaultMaxSteps && queue.Count > 0; depth++)
            {
                frontier.Clear();

                int layer = queue.Count;

                for (int i = 0; i < layer; i++) frontier.Add(queue.Dequeue());

                for (int i = 0; i < frontier.Count; i++)
                {
                    Vector3Int current = frontier[i];

                    if (current == goal) return TraceStep(start, goal, cameFrom, out step);

                    for (int n = 0; n < Neighbors4.Length; n++)
                    {
                        Vector3Int next = current + Neighbors4[n];

                        // 入队即标记：同一格被两条路同时发现会排两次队
                        if (!visited.Add(next)) continue;

                        if (!isWalkable(next)) continue;

                        cameFrom[next] = current;

                        queue.Enqueue(next);
                    }
                }
            }

            return false;
        }

        /// <summary>从 cameFrom 回溯到 start 的下一步；断链说明 goal 不是本次搜索到的</summary>
        private static bool TraceStep(
            Vector3Int start,
            Vector3Int goal,
            Dictionary<Vector3Int, Vector3Int> cameFrom,
            out Vector3Int step)
        {
            step = start;

            Vector3Int current = goal;

            while (cameFrom.TryGetValue(current, out Vector3Int previous))
            {
                if (previous == start)
                {
                    step = current;
                    return true;
                }

                current = previous;
            }

            return false;
        }
    }
}
