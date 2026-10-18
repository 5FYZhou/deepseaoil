using System.Collections.Generic;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Adapters;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>带电地貌的常驻特效：萤火虫盘旋，偶发一道微型折角电弧</summary>
    /// <remarks>借还必须 StopEmittingAndClear，否则池化复用会带出上一格的残留粒子并瞬态暴闪。</remarks>
    [DisallowMultipleComponent]
    public sealed class TileVfxDirector : MonoBehaviour
    {
        private const string DefaultPrefabPath = "effects/Vfx_Tile_Electricity";

        [Header("池容量")]
        [Tooltip("常驻粒子对象上限：按累计创建数卡，达到后新请求被丢弃")]
        [SerializeField] private int maxPoolSize = 32;

        [Tooltip("电格粒子预制体；留空则按约定从 Resources 懒加载 effects/Vfx_Tile_Electricity")]
        [SerializeField] private GameObject electricityVfxPrefab = default;

        [Tooltip("接线（留空自动抓取同一节点上的 TilemapAdapter）")]
        [SerializeField] private TilemapAdapter tilemapAdapter = default;

        private sealed class VfxInstance
        {
            public GameObject Go;

            public ParticleSystem[] Systems;
        }

        private readonly Dictionary<Vector3Int, VfxInstance> _active = new();

        private Pool<VfxInstance> _pool;
        private GridGeometry _geometry;
        private GameObject _prefab;
        private bool _attached;
        private int _dropped;

        /// <summary>当前借出的常驻粒子数量，一个带电格一件</summary>
        public int ActiveVfxCount => _active.Count;

        /// <summary>带电地貌判据：新增带电状态必须同时扩这里，否则只有贴图没有粒子</summary>
        public static bool IsChargedState(TileStateType state)
        {
            switch (state)
            {
                case TileStateType.BasicElectricity:
                case TileStateType.ConductZone:
                case TileStateType.ChargedThorn:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>装配几何并建池，重复调用 no-op</summary>
        public void Initialize(in GridGeometry geometry)
        {
            if (_pool != null) return;

            _geometry = geometry;

            _pool = new Pool<VfxInstance>(
                factory: CreateInstance,
                onGet: OnGetFromPool,
                onRelease: OnReleaseToPool,
                name: "TileVfxPool",
                maxSize: Mathf.Max(1, maxPoolSize),
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                onDestroy: DestroyInstance);
        }

        public void Attach()
        {
            if (_attached) return;

            if (_pool == null)
            {
                TilemapAdapter adapter = tilemapAdapter != null ? tilemapAdapter : GetComponent<TilemapAdapter>();

                if (adapter == null)
                {
                    Debug.LogError(
                        "[Vfx] TileVfxDirector 必须与 TilemapAdapter 挂在同一节点：电格粒子不会生成。", this);

                    return;
                }

                Initialize(adapter.ReadGeometry());
            }

            _prefab = electricityVfxPrefab != null
                ? electricityVfxPrefab
                : Resources.Load<GameObject>(DefaultPrefabPath);

            if (_prefab == null)
            {
                Debug.LogError(
                    $"[Vfx] 找不到电格粒子预制体 Resources/{DefaultPrefabPath}：电格不会出现常驻光点。" +
                    "先跑 DeepseaOil ▸ 特效 ▸ 一键烘焙漏电球状闪电与夜间弥漫件（生成后需重进 PlayMode）。", this);

                return;
            }

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);

            _attached = true;
        }

        public void Detach()
        {
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);

            if (!_attached) return;

            _attached = false;

            ClearAll();
        }

        /// <summary>归还全部活动粒子；复活与切场景都由 TilemapAdapter 级联调用</summary>
        public void ClearAll()
        {
            if (_pool == null) return;

            foreach (KeyValuePair<Vector3Int, VfxInstance> kv in _active)
            {
                if (kv.Value?.Go != null) _pool.Release(kv.Value);
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
            // 静态事件总线可能仍持有已销毁实例的委托：先摘账，避免 Unity 假 null 访问继续抛 MissingReference。
            if (this == null)
            {
                EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);

                return;
            }

            if (_pool == null) return;

            // 先归还旧件：无论新状态是否带电，上一格的光点都不能留在场上
            if (_active.TryGetValue(evt.Cell, out VfxInstance old))
            {
                _pool.Release(old);
                _active.Remove(evt.Cell);
            }

            if (!IsChargedState(evt.State)) return;
            if (!_geometry.IsValid) return;

            if (!_pool.TryGet(out VfxInstance instance) || instance == null)
            {
                CountDropped();

                return;
            }

            if (instance.Go == null)
            {
                _pool.Release(instance);

                CountDropped();

                return;
            }

            Vector2 center = _geometry.CellCenter(evt.Cell);

            instance.Go.transform.position = new Vector3(center.x, center.y, 0f);

            _active[evt.Cell] = instance;
        }

        private VfxInstance CreateInstance()
        {
            GameObject go = Instantiate(_prefab, transform);

            go.name = "TileElectricityVfx";

            var instance = new VfxInstance
            {
                Go = go,
                Systems = go.GetComponentsInChildren<ParticleSystem>(true),
            };

            if (instance.Systems.Length == 0)
            {
                Debug.LogError($"[Vfx] 预制体 {_prefab.name} 上没有 ParticleSystem：该格不会有任何特效。", this);
            }

            go.SetActive(false);

            return instance;
        }

        private static void OnGetFromPool(VfxInstance instance)
        {
            if (instance == null || instance.Go == null) return;

            instance.Go.SetActive(true);

            if (instance.Systems == null) return;

            for (int i = 0; i < instance.Systems.Length; i++)
            {
                if (instance.Systems[i] == null) continue;

                instance.Systems[i].Clear(true);
                instance.Systems[i].Play(true);
            }
        }

        private static void OnReleaseToPool(VfxInstance instance)
        {
            if (instance == null) return;

            if (instance.Systems != null)
            {
                for (int i = 0; i < instance.Systems.Length; i++)
                {
                    if (instance.Systems[i] == null) continue;

                    instance.Systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (instance.Go != null) instance.Go.SetActive(false);
        }

        private static void DestroyInstance(VfxInstance instance)
        {
            if (instance?.Go == null) return;

            // 编辑态（生成工具/EditMode 测试）没有延迟销毁窗口，Destroy 会当场报错
            if (Application.isPlaying) Destroy(instance.Go);
            else DestroyImmediate(instance.Go);
        }

        /// <summary>丢件计数：只报头一次与每 64 次，免得刷屏</summary>
        private void CountDropped()
        {
            _dropped++;

            if (_dropped != 1 && _dropped % 64 != 0) return;

            Debug.LogWarning(
                $"[Vfx] 电格粒子借不到：累计 {_dropped} 次（上限 {maxPoolSize}）。" +
                "调大 TileVfxDirector.maxPoolSize。", this);
        }
    }
}
