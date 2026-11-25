using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Actor;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>阻挡地貌的物理体管理器：is_obstacle 的格子挂一个碰撞盒，状态变化自动借还池</summary>
    /// <remarks>只认 tile_state.is_obstacle；贴图仍归 Tilemap / TileOverlayDirector，本类不碰渲染。</remarks>
    public sealed class TileObstacleDirector : MonoBehaviour
    {
        private const int PushCapacity = 16;

        private const float PushMargin = 0.05f;

        private static readonly Vector2Int[] PushDirections =
        {
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 1),
        };

        [Header("池容量")]
        [Tooltip("阻挡体上限：卡累计创建数，闲置再久也占额度")]
        [SerializeField] private int maxPoolSize = 128;

        private readonly Dictionary<Vector3Int, GameObject> _active = new();
        private readonly HashSet<TileStateType> _obstacleStates = new();
        private readonly Collider2D[] _pushBuffer = new Collider2D[PushCapacity];
        private readonly List<EnemyActor> _pushedEnemies = new(8);
        private readonly List<Rigidbody2D> _pushedBodies = new(8);

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private GridLogic _grid;
        private ContactFilter2D _pushFilter;
        private bool _attached;

        /// <summary>装配几何并收集阻挡状态，重复调用 no-op；池的 factory 首次触发在 TryGet，故 _geometry 赋值早于它</summary>
        public void Initialize(in GridGeometry geometry)
        {
            if (_pool != null) return;

            _geometry = geometry;
            _pushFilter = new ContactFilter2D { useTriggers = false };

            _obstacleStates.Clear();

            IReadOnlyList<TileStateSpec> specs = ConfigModule.GetAllTileStates();

            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].IsObstacle) _obstacleStates.Add(specs[i].Id);
            }

            _pool = new Pool<GameObject>(
                factory: CreateObstacleObject,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "TileObstaclePool",
                maxSize: maxPoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                onDestroy: Destroy);
        }

        public void SetGrid(GridLogic grid)
        {
            _grid = grid;
        }

        public void Attach()
        {
            if (_attached) return;

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached) return;

            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
            _attached = false;

            ClearAll();
        }

        public void ClearAll()
        {
            if (_pool == null) return;

            foreach (KeyValuePair<Vector3Int, GameObject> kv in _active)
            {
                if (kv.Value != null) _pool.Release(kv.Value);
            }

            _active.Clear();
        }

        private void OnDestroy()
        {
            Detach();
            _pool?.Dispose();
            _pool = null;
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            // Attach 早于 Initialize 时事件先到：没有池就什么都不做
            if (this == null || _pool == null) return;

            // 先归还旧对象：离开阻挡态的格必须立刻放行
            if (_active.TryGetValue(evt.Cell, out GameObject old))
            {
                if (old != null) _pool.Release(old);
                _active.Remove(evt.Cell);
            }

            if (!_obstacleStates.Contains(evt.State)) return;

            if (!_geometry.IsValid) return;

            if (!_pool.TryGet(out GameObject obj) || obj == null) return;

            Vector2 center = _geometry.CellCenter(evt.Cell);

            obj.transform.position = new Vector3(center.x, center.y, 0f);

            PushOverlappingEnemies(evt.Cell, center);

            _active[evt.Cell] = obj;
        }

        private void PushOverlappingEnemies(Vector3Int obstacleCell, Vector2 center)
        {
            if (_grid == null) return;

            int hits = Physics2D.OverlapBox(
                center,
                Vector2.one * _geometry.CellSize,
                0f,
                _pushFilter,
                _pushBuffer);

            if (hits == 0) return;

            _pushedEnemies.Clear();
            _pushedBodies.Clear();

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var registeredCell = new Vector3Int(
                        obstacleCell.x + dx,
                        obstacleCell.y + dy,
                        obstacleCell.z);

                    if (!_grid.Registry.TryGetIn(registeredCell, out List<IEffectTarget> targets)) continue;

                    for (int i = 0; i < targets.Count; i++)
                    {
                        EnemyActor enemy = targets[i] as EnemyActor;

                        if (enemy == null || !enemy.IsAlive || _pushedEnemies.Contains(enemy)) continue;

                        Rigidbody2D body = FindBody(enemy, hits);

                        if (body == null || _grid.WorldToCell(body.position) != obstacleCell) continue;

                        _pushedEnemies.Add(enemy);
                        _pushedBodies.Add(body);
                    }
                }
            }

            for (int i = 0; i < _pushedEnemies.Count; i++)
            {
                TryPush(_pushedEnemies[i], _pushedBodies[i], obstacleCell, center);
            }

            Physics2D.SyncTransforms();
        }

        private Rigidbody2D FindBody(EnemyActor enemy, int hits)
        {
            Rigidbody2D best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Rigidbody2D body = _pushBuffer[i] != null ? _pushBuffer[i].attachedRigidbody : null;

                if (body == null || _pushedBodies.Contains(body)) continue;

                float sqr = (body.position - enemy.Position).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = body;
                }
            }

            return best;
        }

        private bool TryPush(EnemyActor enemy, Rigidbody2D body, Vector3Int obstacleCell, Vector2 center)
        {
            Vector2 preferred = (body != null ? body.position : enemy.Position) - center;
            float bestAlignment = float.MinValue;
            Vector3Int bestCell = default;
            Vector2 bestPoint = default;

            for (int pass = 0; pass < 2 && bestAlignment == float.MinValue; pass++)
            {
                int first = pass == 0 ? 0 : 4;
                int end = pass == 0 ? 4 : PushDirections.Length;

                for (int i = first; i < end; i++)
                {
                    Vector2Int step = PushDirections[i];
                    var candidate = new Vector3Int(
                        obstacleCell.x + step.x,
                        obstacleCell.y + step.y,
                        obstacleCell.z);

                    if (!TryResolveTarget(enemy, candidate, center, preferred, out Vector2 point)) continue;

                    Vector2 direction = new Vector2(step.x, step.y).normalized;
                    float alignment = preferred.sqrMagnitude > 0.0001f
                        ? Vector2.Dot(direction, preferred.normalized)
                        : 0f;

                    if (alignment <= bestAlignment) continue;

                    bestAlignment = alignment;
                    bestCell = candidate;
                    bestPoint = point;
                }
            }

            if (bestAlignment == float.MinValue) return false;

            if (body != null)
            {
                body.position = bestPoint;
                body.velocity = Vector2.zero;
            }

            enemy.transform.position = new Vector3(bestPoint.x, bestPoint.y, enemy.transform.position.z);
            _grid.Registry.Move(enemy, bestCell);

            return true;
        }

        private bool TryResolveTarget(
            EnemyActor enemy,
            Vector3Int candidate,
            Vector2 obstacleCenter,
            Vector2 preferred,
            out Vector2 point)
        {
            point = default;

            if (!_grid.HasCell(candidate) || _grid.IsObstacle(candidate)) return false;

            float radius = enemy.Radius;
            float skin = ConfigModule.NavSkin;

            if (_grid.Nav != null && !_grid.Nav.IsWalkable(candidate, radius, skin)) return false;

            Vector2 direction = (_geometry.CellCenter(candidate) - obstacleCenter).normalized;

            float half = _geometry.CellSize * 0.5f;
            float centerDistance = _geometry.CellSize;
            float requiredDistance = half + radius + skin + PushMargin;
            float projectedDistance = Mathf.Abs(direction.x) > 0.1f && Mathf.Abs(direction.y) > 0.1f
                ? requiredDistance * 1.41421356f
                : requiredDistance;
            float targetDistance = Mathf.Max(centerDistance, projectedDistance);

            point = obstacleCenter + direction * targetDistance;

            if (_grid.WorldToCell(point) != candidate) return false;

            if (_grid.Nav != null
                && _grid.Nav.TrySampleAt(point, out int sample)
                && !_grid.Nav.IsSampleFree(sample, radius, skin))
            {
                return false;
            }

            return true;
        }

        private GameObject CreateObstacleObject()
        {
            var go = new GameObject("TileObstacle");

            go.transform.SetParent(transform, false);

            var box = go.AddComponent<BoxCollider2D>();

            box.size = Vector2.one * (_geometry.IsValid ? _geometry.CellSize : 1f);

            go.SetActive(false);

            return go;
        }
    }
}
