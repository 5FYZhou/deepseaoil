using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>地貌贴图配置，关联地貌状态与立面贴图</summary>
    [Serializable]
    public struct TileOverlayBinding
    {
        [Tooltip("对应地貌状态")]
        public TileStateType State;

        [Tooltip("纵深贴图。留空则走 tiles/overlays/Overlay_<状态> 约定加载")]
        public Sprite Sprite;

        [Tooltip("垂直微调偏移")]
        public float ExtraOffsetY;
    }

    /// <summary>纵深立面覆盖物管理器：脚底深度对齐 Y-Sort 频带</summary>
    /// <remarks>贴地底纹仍归 Tilemap，本类只管凸出地面的 1x2 纵深立面件，状态切换自动借还池。</remarks>
    public sealed class TileOverlayDirector : MonoBehaviour
    {
        [Header("覆盖物映射表")]
        [Tooltip("各状态立面贴图绑定。未配的将尝试约定路径懒加载")]
        [SerializeField] private TileOverlayBinding[] bindings = new TileOverlayBinding[0];

        [Header("池容量")]
        [Tooltip("立面件上限：卡累计创建数，闲置再久也占额度")]
        [SerializeField] private int maxPoolSize = 256;

        private readonly Dictionary<Vector3Int, GameObject> _activeOverlays = new();
        private readonly Dictionary<TileStateType, TileOverlayBinding> _bindingMap = new();
        private readonly Dictionary<TileStateType, Sprite> _lazySpriteCache = new();

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private bool _attached;
        private int _droppedOverlays;

        /// <summary>装配几何并初始化对象池</summary>
        public void Initialize(in GridGeometry geometry)
        {
            _geometry = geometry;

            _bindingMap.Clear();
            for (int i = 0; i < bindings.Length; i++)
            {
                _bindingMap[bindings[i].State] = bindings[i];
            }

            _pool = new Pool<GameObject>(
                factory: CreateOverlayObject,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "TileOverlayPool",
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
            foreach (KeyValuePair<Vector3Int, GameObject> kv in _activeOverlays)
            {
                if (kv.Value != null) _pool.Release(kv.Value);
            }

            _activeOverlays.Clear();
        }

        private void OnDestroy()
        {
            Detach();
            _pool?.Dispose();
            _pool = null;
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            if (!_geometry.IsValid) return;

            // 离开覆盖态时归还旧对象
            if (_activeOverlays.TryGetValue(evt.Cell, out GameObject activeObj))
            {
                _pool.Release(activeObj);
                _activeOverlays.Remove(evt.Cell);
            }

            if (evt.State == TileStateType.Normal) return;

            if (!TryGetOverlay(evt.State, out Sprite sprite, out float extraOffsetY)) return;

            if (!_pool.TryGet(out GameObject obj) || obj == null)
            {
                CountDropped();
                return;
            }

            Vector2 center = _geometry.CellCenter(evt.Cell);
            float cellSize = _geometry.CellSize;

            // 格底 Y：2.5D 深度遮挡的基准
            float footY = center.y - cellSize * 0.5f;

            // 贴图 Pivot 默认底边居中，坐标直接贴齐格底
            obj.transform.position = new Vector3(center.x, footY + extraOffsetY, 0f);

            var renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            renderer.sortingOrder = RenderOrder.ActorOrder(footY) - 1;

            _activeOverlays[evt.Cell] = obj;
        }

        private bool TryGetOverlay(TileStateType state, out Sprite sprite, out float extraOffsetY)
        {
            sprite = null;
            extraOffsetY = 0f;

            // 门禁：只处理 bindings 里登记过的状态，未登记的地貌直接跳过，绝不盲查 AssetModule
            if (!_bindingMap.TryGetValue(state, out TileOverlayBinding binding))
            {
                return false;
            }

            extraOffsetY = binding.ExtraOffsetY;

            // 若 Inspector 已拖入贴图，直接使用
            if (binding.Sprite != null)
            {
                sprite = binding.Sprite;
                return true;
            }

            // 若留空则仅对该登记状态尝试一次约定路径加载
            if (_lazySpriteCache.TryGetValue(state, out sprite))
            {
                return sprite != null;
            }

            if (AssetModule.IsInitialized)
            {
                string key = $"tiles/overlays/Overlay_{state}";
                sprite = AssetModule.Load<Sprite>(key);
                _lazySpriteCache[state] = sprite;
                return sprite != null;
            }

            return false;
        }

        private GameObject CreateOverlayObject()
        {
            var go = new GameObject("TileOverlay");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = RenderOrder.OverlayLayer;

            go.SetActive(false);
            return go;
        }

        /// <summary>丢件计数：只报头一次与每 64 次，免得刷屏</summary>
        private void CountDropped()
        {
            _droppedOverlays++;

            if (_droppedOverlays != 1 && _droppedOverlays % 64 != 0) return;

            Debug.LogWarning(
                $"[Grid] TileOverlay 立面件借不到：累计 {_droppedOverlays} 次（上限 {maxPoolSize}）。" +
                "调大 TileOverlayDirector.maxPoolSize，或检查 bindings 是否登记了这个地貌状态。", this);
        }
    }
}