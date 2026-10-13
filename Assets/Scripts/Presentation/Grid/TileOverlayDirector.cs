using System;
using System.Collections.Generic;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Grid
{
    [Serializable]
    public struct TileOverlayBinding
    {
        [Tooltip("登记该状态有覆盖物立面")]
        public TileStateType State;

        [Tooltip("覆盖物贴图；为空则该状态不显示覆盖物")]
        public Sprite OverlaySprite;

        [Tooltip("覆盖物材质；留空则用 defaultMaterial")]
        public Material OverrideMaterial;

        [Tooltip("垂直微调偏移")]
        public float ExtraOffsetY;
    }

    /// <summary>纵深立面覆盖物管理器：脚底深度对齐 Y-Sort 频带</summary>
    /// <remarks>贴地底纹仍归 Tilemap，本类只管凸出地面的 1x2 纵深立面件，状态切换自动借还池。</remarks>
    public sealed class TileOverlayDirector : MonoBehaviour
    {
        [Header("覆盖物映射表")]
        [Tooltip("登记哪些状态有覆盖物立面")]
        [SerializeField] private TileOverlayBinding[] bindings = new TileOverlayBinding[0];

        [Header("默认材质")]
        [Tooltip("覆盖物默认材质（装配必填）。OverrideMaterial 为空的 binding 用它；未拖则 SpriteRenderer 会显示为粉色 error shader")]
        [SerializeField] private Material defaultMaterial = default;

        [Header("池容量")]
        [Tooltip("立面件上限：卡累计创建数，闲置再久也占额度")]
        [SerializeField] private int maxPoolSize = 256;

        private readonly Dictionary<Vector3Int, GameObject> _activeOverlays = new();
        private readonly Dictionary<TileStateType, TileOverlayBinding> _bindingMap = new();

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private bool _attached;
        private int _droppedOverlays;

        /// <summary>装配几何并初始化对象池，重复调用 no-op</summary>
        public void Initialize(in GridGeometry geometry)
        {
            if (_pool != null) return;

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
            // 先归还旧对象：无论新状态有没有覆盖物，旧的那件都不能留在场上
            if (_activeOverlays.TryGetValue(evt.Cell, out GameObject oldObj))
            {
                _pool.Release(oldObj);
                _activeOverlays.Remove(evt.Cell);
            }

            if (evt.State == TileStateType.Normal) return;
            if (!_geometry.IsValid) return;

            if (!_bindingMap.TryGetValue(evt.State, out TileOverlayBinding binding)) return;

            // 空 Sprite 直接返回：不借池对象，也就不占池额度
            if (binding.OverlaySprite == null) return;

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
            obj.transform.position = new Vector3(center.x, footY + binding.ExtraOffsetY, 0f);

            var renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sprite = binding.OverlaySprite;

            // sharedMaterial 不克隆实例；默认材质靠三元兜底，赋 null 会变粉色
            renderer.sharedMaterial = binding.OverrideMaterial != null
                ? binding.OverrideMaterial
                : defaultMaterial;

            renderer.sortingOrder = RenderOrder.ActorOrder(footY) - 1;

            _activeOverlays[evt.Cell] = obj;
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
                "调大 TileOverlayDirector.maxPoolSize。", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (defaultMaterial == null)
            {
                Debug.LogWarning(
                    "[Overlay] defaultMaterial 未拖：OverrideMaterial 为空的 binding 会显示成粉色。", this);
            }

            if (bindings == null) return;

            for (int i = 0; i < bindings.Length; i++)
            {
                TileStateType state = bindings[i].State;

                if (state == TileStateType.None || state == TileStateType.Normal)
                {
                    Debug.LogWarning(
                        $"[Overlay] bindings 第 {i} 行登记的是 {state}，该状态不该有覆盖物。", this);

                    continue;
                }

                if (bindings[i].OverlaySprite == null)
                {
                    Debug.LogWarning(
                        $"[Overlay] bindings 第 {i} 行登记了 {state}，但 OverlaySprite 为空：该状态不会显示覆盖物。", this);
                }

                for (int j = i + 1; j < bindings.Length; j++)
                {
                    if (bindings[j].State == state)
                    {
                        Debug.LogError(
                            $"[Overlay] bindings 里 {state} 出现了两次（第 {i} 行与第 {j} 行）：只会取第一条。", this);
                    }
                }
            }
        }
#endif
    }
}