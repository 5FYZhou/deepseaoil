using System.Collections.Generic;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>把场景预制体的碰撞体烘成逻辑层净空图：每格固定采样点到碰撞体的最近距离</summary>
    /// <remarks>烘"距离"而不是"能不能过"：判据含体半径，留在逻辑层比，一张图服务所有怪种。热路径不查询物理。</remarks>
    public static class SceneObstacleBaker
    {
        /// <summary>单点查询上限：超出会高估净空（站不住的点判成可走），宁可报错也不要静默漏</summary>
        private const int HitCapacity = 128;

        private static readonly Collider2D[] Hits = new Collider2D[HitCapacity];

        // 触发器不进候选（同 IsBlocking 口径）
        private static readonly ContactFilter2D Filter = new ContactFilter2D { useTriggers = false };

        private static bool _saturationReported;

        // 同一批装饰体被上千个采样点反复问：分类一次缓存，Bake 开头清
        private static readonly Dictionary<Collider2D, bool> BlockingCache = new Dictionary<Collider2D, bool>();

        /// <summary>烘焙净空图；clearanceCap 是解析上限（世界单位），更远一律饱和到它</summary>
        /// <remarks>净空 = min(到静态碰撞体, 到阻挡地貌格的距离)：阻挡地貌碰撞体不进障碍集，不补距离怪会贴它站。未登记格留 0；几何非法或空返回 null（null = 没烘焙）。</remarks>
        public static NavMap Bake(GridLogic grid, float clearanceCap)
        {
            if (grid == null || !grid.Geometry.IsValid) return null;

            BlockingCache.Clear();

            float cap = clearanceCap > 0f && !float.IsNaN(clearanceCap) ? clearanceCap : 1f;

            if (!TryBounds(grid, out Vector2Int min, out Vector2Int size)) return null;

            GridGeometry geometry = grid.Geometry;
            var map = new NavMap(min, size, in geometry);
            var blocked = new byte[map.CellCount];
            float half = geometry.CellSize * 0.5f;
            int ring = Mathf.CeilToInt(cap / geometry.CellSize);

            for (int i = 0; i < map.CellCount; i++)
            {
                Vector3Int cell = map.CellOf(i);

                // 登记表是"这里有没有地板"的唯一真源；阻挡地貌另算一档，两者都不留净空
                blocked[i] = !grid.HasCell(cell) || grid.IsObstacle(cell) ? (byte)1 : (byte)0;
            }

            for (int i = 0; i < map.CellCount; i++)
            {
                if (blocked[i] != 0) continue;

                Vector3Int cell = map.CellOf(i);

                for (int j = 0; j < NavMap.Subdivision; j++)
                {
                    for (int k = 0; k < NavMap.Subdivision; k++)
                    {
                        int sampleIndex = map.SampleIndexOf(i, k, j);
                        Vector2 point = map.SampleCenter(sampleIndex);
                        float clearance = SampleClearance(point, cap);
                        float terrain = TerrainClearance(map, blocked, cell, point, half, ring, cap);

                        map.SetSampleClearance(sampleIndex, Mathf.Min(clearance, terrain));
                    }
                }
            }

            return map;
        }

        /// <summary>取点到最近阻挡碰撞体的距离，超过 cap 饱和到 cap</summary>
        /// <remarks>查询盒半边长恰为 cap：漏判方向是"高估净空"，故命中数触顶仍要报错。
        /// 实测最近点比几何距离近约 0.01（Physics2D 接触偏移），一格宽缺口量出 0.5 而不是 0.5+，皮厚预算要留出这 0.01。</remarks>
        private static float SampleClearance(Vector2 point, float cap)
        {
            int count = Physics2D.OverlapBox(point, new Vector2(cap * 2f, cap * 2f), 0f, Filter, Hits);

            if (count >= HitCapacity && !_saturationReported)
            {
                _saturationReported = true;

                Debug.LogError(
                    $"[ObstacleBake] 点 ({point.x:F2}, {point.y:F2}) 的碰撞体查询命中 {count} 个，已达上限 {HitCapacity}：" +
                    "净空可能被高估（站不住的点会被判成可走）。每次启动只报一次，排查该处是否叠了过多碰撞体。");
            }

            float nearest = cap;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = Hits[i];

                if (hit == null) continue;

                if (!BlockingCache.TryGetValue(hit, out bool blocking))
                {
                    blocking = IsBlocking(hit);
                    BlockingCache[hit] = blocking;
                }

                if (!blocking) continue;

                float distance = Vector2.Distance(point, hit.ClosestPoint(point));

                if (distance < nearest) nearest = distance;
            }

            return nearest;
        }

        /// <summary>取点到最近"阻挡地貌格"的距离；圈内没有阻挡地貌时饱和到 cap</summary>
        /// <remarks>圈数由解析上限推：cap 之外的点都饱和成 cap，多搜没有意义。地貌格按整格矩形算距离。</remarks>
        private static float TerrainClearance(NavMap map, byte[] blocked, Vector3Int cell, Vector2 point, float half, int ring, float cap)
        {
            float nearest = cap;

            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    var probe = new Vector3Int(cell.x + dx, cell.y + dy, 0);

                    if (!map.TryIndex(probe, out int index) || blocked[index] == 0) continue;

                    float ox = Mathf.Max(Mathf.Abs(point.x - map.CellCenter(probe).x) - half, 0f);
                    float oy = Mathf.Max(Mathf.Abs(point.y - map.CellCenter(probe).y) - half, 0f);
                    float distance = Mathf.Sqrt(ox * ox + oy * oy);

                    if (distance < nearest) nearest = distance;
                }
            }

            return nearest;
        }

        private static bool TryBounds(GridLogic grid, out Vector2Int min, out Vector2Int size)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            bool any = false;

            foreach (Vector3Int cell in grid.Cells)
            {
                any = true;

                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            min = any ? new Vector2Int(minX, minY) : Vector2Int.zero;
            size = any ? new Vector2Int(maxX - minX + 1, maxY - minY + 1) : Vector2Int.zero;

            return any;
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
    }
}
