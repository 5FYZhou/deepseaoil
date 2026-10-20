using DeepseaOil.Logic.Grid;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人路径跟随：固定前视 ＋ 航点保持 ＋ 拐点扫描 ＋ 直线捷径</summary>
    /// <remarks>只吃流场与位置，不碰场景、不读时间。航点按流场版本失效；找不到下一步返回 false，调用方退回直线意图。</remarks>
    public sealed class EnemyPathFollower
    {
        /// <summary>前视距离（格）：0.75 格够跨过三个采样点，又不会把远处拐角当成近处</summary>
        private const float LookAheadCells = 0.75f;

        /// <summary>航点保持半径（格）：同一张场里小幅移动不换目标，消掉采样点级抖动</summary>
        private const float HoldRadiusCells = 0.15f;

        /// <summary>拐点扫描距离（格）：刹车距离要按真实拐点算，不能只看前视点</summary>
        private const float CornerScanCells = 2.5f;

        private const int MaxCornerScanNodes = 16;

        /// <summary>判定"同向"的方向点积下限：四邻步只有同向与转弯两种，取 0.9 只躲浮点抖动</summary>
        private const float TurnCosine = 0.9f;

        /// <summary>航点离身体太近时不再给方向，交给上层回退，避免零向量</summary>
        private const float MinWaypointCells = 0.1f;

        private Vector2 _waypoint;
        private int _version = -1;
        private bool _hasWaypoint;

        public bool TryResolve(
            NavField field,
            NavMap map,
            GridLogic grid,
            Vector2 position,
            Vector2 velocity,
            bool jamming,
            out Vector2 desired,
            out float cornerDistance,
            out bool cornerAhead)
        {
            desired = Vector2.zero;
            cornerDistance = 0f;
            cornerAhead = false;

            if (field == null || map == null || grid == null) return false;
            if (!field.TryFindStart(position, out int node)) return false;
            if (!TryGetStep(field, node, jamming, out int next)) return false;
            if (next == node) return false;

            if (field.Version != _version)
            {
                _version = field.Version;
                _hasWaypoint = false;
            }

            float cellSize = grid.Geometry.IsValid ? grid.Geometry.CellSize : 1f;
            float lookAhead = LookAheadCells * cellSize;
            float holdRadius = HoldRadiusCells * cellSize;

            Vector2 nodeCenter = map.SampleCenter(node);
            Vector2 nextCenter = map.SampleCenter(next);
            Vector2 leg = nextCenter - nodeCenter;

            if (leg.sqrMagnitude <= 0f) return false;

            float legLength = leg.magnitude;
            Vector2 firstLeg = leg / legLength;

            // 拐点扫描：独立于前视点，刹车要用完整距离
            bool turned = false;
            Vector2 cornerPoint = nextCenter;
            int cursor = next;
            float pathDistance = legLength;
            float cornerScan = CornerScanCells * cellSize;
            Vector2 legHat = firstLeg;

            for (int i = 0; i < MaxCornerScanNodes && pathDistance <= cornerScan; i++)
            {
                if (!field.TryGetStep(cursor, out int after)) break;

                Vector2 from = map.SampleCenter(cursor);
                Vector2 to = map.SampleCenter(after);
                Vector2 step = to - from;

                if (step.sqrMagnitude <= 0f) break;

                if (Vector2.Dot(step.normalized, legHat) < TurnCosine)
                {
                    turned = true;
                    cornerPoint = from;

                    break;
                }

                legHat = step.normalized;
                cursor = after;
                pathDistance += step.magnitude;
            }

            cornerAhead = turned && !jamming;
            cornerDistance = cornerAhead ? (cornerPoint - position).magnitude : 0f;

            // 前视点：沿链累计折线长度，最后一段线性插值；遇到拐点停在拐点中心
            Vector2 waypoint = nextCenter;
            float travelled = legLength;
            int lookCursor = next;
            Vector2 lookLeg = firstLeg;
            int elementNode = grid.IsElementTile(map.CellOfSample(next)) ? next : -1;

            for (int i = 0; i < MaxCornerScanNodes && travelled < lookAhead; i++)
            {
                if (!field.TryGetStep(lookCursor, out int after)) break;

                Vector2 from = map.SampleCenter(lookCursor);
                Vector2 to = map.SampleCenter(after);
                Vector2 step = to - from;

                if (step.sqrMagnitude <= 0f) break;

                if (Vector2.Dot(step.normalized, lookLeg) < TurnCosine)
                {
                    waypoint = from;

                    break;
                }

                float segment = step.magnitude;

                if (travelled + segment >= lookAhead)
                {
                    waypoint = from + step.normalized * (lookAhead - travelled);

                    break;
                }

                travelled += segment;
                lookCursor = after;
                lookLeg = step.normalized;
                waypoint = to;

                if (elementNode < 0 && grid.IsElementTile(map.CellOfSample(after))) elementNode = after;
            }

            // 第一个元素节点夹取：保证身体真的走进元素格，不被直线捷径从格角切掉
            if (elementNode >= 0) waypoint = map.SampleCenter(elementNode);

            // 航点保持：旧航点仍在身前且新航点没跑远就沿用
            if (_hasWaypoint
                && Vector2.Distance(waypoint, _waypoint) <= holdRadius
                && Vector2.Dot(_waypoint - position, velocity) >= 0f)
            {
                waypoint = _waypoint;
            }
            else
            {
                _waypoint = waypoint;
                _hasWaypoint = true;
            }

            // 直线捷径：航点在路径上，直线全通才走；否则退回下一节点中心
            Vector2 target = IsLineWalkable(grid, map, field.Radius, field.Skin, position, waypoint)
                ? waypoint
                : nextCenter;

            desired = target - position;

            float minDistance = MinWaypointCells * cellSize;

            return desired.sqrMagnitude >= minDistance * minDistance;
        }

        /// <summary>该点（不是整格）站不站得住：直线捷径与软分离共用</summary>
        public static bool IsPointWalkable(GridLogic grid, NavMap map, float radius, float skin, Vector2 point)
        {
            if (grid == null || map == null) return false;
            if (!map.TrySampleAt(point, out int index)) return false;
            if (!map.IsSampleFree(index, radius, skin)) return false;

            return !grid.IsObstacle(grid.WorldToCell(point));
        }

        /// <summary>按采样间距逐点验直线；薄墙不会从两个探针之间漏过去</summary>
        private static bool IsLineWalkable(GridLogic grid, NavMap map, float radius, float skin, Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);

            if (distance <= 0f) return true;

            float spacing = map.Spacing;

            if (spacing <= 0f) return false;

            int steps = Mathf.CeilToInt(distance / spacing);

            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);

                if (!IsPointWalkable(grid, map, radius, skin, point)) return false;
            }

            return true;
        }

        /// <remarks>平时取首选；本节点站不住（刷在墙里）或已在目标时退到"代价更小的邻节点"。换步优先取次优邻节点，四邻只剩首选时退回首选（贴墙也要往前压，不能原地不动）。</remarks>
        private static bool TryGetStep(NavField field, int node, bool jamming, out int next)
        {
            next = node;

            return jamming
                ? field.TryGetStepOption(node, true, out next) || field.TryGetStep(node, out next)
                : field.TryGetStep(node, out next) || field.TryGetStepOption(node, false, out next);
        }
    }
}
