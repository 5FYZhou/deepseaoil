using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>阻挡地貌的物理体管理器：is_obstacle 的格子挂一个碰撞盒，状态变化自动借还池</summary>
    /// <remarks>只认 tile_state.is_obstacle；贴图仍归 Tilemap / TileOverlayDirector，本类不碰渲染。</remarks>
    public sealed class TileObstacleDirector : MonoBehaviour
    {
        [Header("池容量")]
        [Tooltip("阻挡体上限：卡累计创建数，闲置再久也占额度")]
        [SerializeField] private int maxPoolSize = 32;

        private readonly Dictionary<Vector3Int, GameObject> _active = new();
        private readonly HashSet<TileStateType> _obstacleStates = new();

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private bool _attached;

        /// <summary>装配几何并收集阻挡状态；池的 factory 首次触发在 TryGet，故 _geometry 赋值早于它</summary>
        public void Initialize(in GridGeometry geometry)
        {
            _geometry = geometry;

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
            if (_pool == null) return;

            // 先归还旧对象：离开阻挡态的格必须立刻放行
            if (_active.TryGetValue(evt.Cell, out GameObject old))
            {
                _pool.Release(old);
                _active.Remove(evt.Cell);
            }

            if (!_obstacleStates.Contains(evt.State)) return;

            if (!_geometry.IsValid) return;

            if (!_pool.TryGet(out GameObject obj) || obj == null) return;

            Vector2 center = _geometry.CellCenter(evt.Cell);

            obj.transform.position = new Vector3(center.x, center.y, 0f);

            _active[evt.Cell] = obj;
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
