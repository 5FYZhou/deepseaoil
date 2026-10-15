using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>限深 BFS 取下一步：只服务敌人绕障，支持局部最优回溯</summary>
    public static class PathProbe
    {
        private static readonly Vector3Int[] Neighbors4 =
        {
            new Vector3Int(0, -1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
        };

        /// <summary>返回起点走到终点的下一步；超步数时回溯至局部最接近目标的节点</summary>
        /// <remarks>bestNode 初值不可用 start 的曼哈顿距离：绕障必经横向折返，必然更远，会把所有候选丢光而退化成撞墙</remarks>
        public static bool TryFindStep(
            GridGeometry geometry,
            Vector3Int start,
            Vector3Int goal,
            int maxSteps,
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
            if (start == goal) return false;

            queue.Clear();
            visited.Clear();
            cameFrom.Clear();

            visited.Add(start);
            queue.Enqueue(start);

            Vector3Int bestNode = start;
            int minDistanceToGoal = int.MaxValue;
            bool reachedGoal = false;

            for (int depth = 0; depth < maxSteps && queue.Count > 0; depth++)
            {
                frontier.Clear();
                int layer = queue.Count;

                for (int i = 0; i < layer; i++) frontier.Add(queue.Dequeue());

                for (int i = 0; i < frontier.Count; i++)
                {
                    Vector3Int current = frontier[i];

                    if (current == goal)
                    {
                        bestNode = current;
                        reachedGoal = true;
                        break;
                    }

                    if (current != start)
                    {
                        int dist = ManhattanDistance(current, goal);
                        if (dist < minDistanceToGoal)
                        {
                            minDistanceToGoal = dist;
                            bestNode = current;
                        }
                    }

                    for (int n = 0; n < Neighbors4.Length; n++)
                    {
                        Vector3Int next = current + Neighbors4[n];

                        // 通行性必须先查：若先 visited.Add(next)，墙体会被写进 cameFrom，回溯出的"下一步"就不是 start 的邻格
                        if (!isWalkable(next)) continue;
                        if (!visited.Add(next)) continue;

                        cameFrom[next] = current;
                        queue.Enqueue(next);
                    }
                }

                if (reachedGoal) break;
            }

            if (bestNode != start)
            {
                return TraceStep(start, bestNode, cameFrom, out step);
            }

            return false;
        }

        /// <remarks>回溯只在 cameFrom 上走，返回格必然是 start 的四邻格，调用方按格心方向位移即不会斜穿墙角</remarks>
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

        private static int ManhattanDistance(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
