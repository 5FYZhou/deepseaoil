using System.Collections.Generic;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using cfg.dso;

namespace DeepseaOil.Presentation.Lighting
{
    /// <summary>地块元素动态点光源管理器：火/电持续光与蒸汽爆轰瞬态白光</summary>
    /// <remarks>点光只能从预制体实例化：Light2D 的照射层 m_ApplyToSortingLayers 是私有字段且无公共访问器，运行期 AddComponent 会被 Awake 填成全部层，点光将漏照 Effect/UI。</remarks>
    [DisallowMultipleComponent]
    public sealed class TileLightDirector : MonoBehaviour
    {
        /// <summary>持续光类别，决定强度调制曲线</summary>
        public enum LightKind
        {
            Fire = 0,
            Electric = 1,
        }

        private const string LightPrefabPath = "lighting/TilePointLight";

        // 呼吸与抽搐的调制基准：持续光 inst.Intensity 恒为 1，Tick 里只写 scale，避免多帧累加漂移
        private const float FireRate = 15.70796f;
        private const float FireFloor = 0.85f;
        private const float FirePeak = 1.05f;
        private const float ElectricRate = 18f;
        private const float ElectricFloor = 0.90f;
        private const float ElectricPeak = 1.20f;
        private const float GoldenRatio = 0.6180339f;
        private const float SteamRadius = 2.2f;
        private const float SteamInnerRadius = 0.5f;
        private const float SteamDuration = 0.32f;

        [Header("容量上限")]
        [Tooltip("对象池保留上限；一盏灯服务一整块连片地貌，16 盏足够覆盖数块火海")]
        [SerializeField] private int maxPoolSize = 32;

        [Tooltip("可选接线（留空自动抓取）")]
        [SerializeField] private TilemapAdapter tilemapAdapter = default;

        private readonly struct LightRequest
        {
            public readonly LightKind Kind;
            public readonly Color Color;
            public readonly float Radius;
            public readonly float InnerRadius;

            public LightRequest(LightKind kind, Color color, float radius, float innerRadius)
            {
                Kind = kind;
                Color = color;
                Radius = radius;
                InnerRadius = innerRadius;
            }
        }

        /// <summary>连成一片的同状态地块共用一盏灯：逐格一盏会在元素反应时轻松撞穿渲染器的 16 盏上限</summary>
        private sealed class LightCluster
        {
            public LightKind Kind;
            public Color Color;
            public float Radius;

            public GameObject Go;
            public Light2D Light;
            public float PhaseOffset;

            public readonly List<Vector3Int> Cells = new List<Vector3Int>();

            public Vector2 Center;
            public float ExtentX;
            public float ExtentY;
        }

        private sealed class LightInstance
        {
            public GameObject Go;
            public Light2D Light;
            public float Elapsed;
            public float Duration;
            public float Intensity = 1f;
        }

        private readonly List<LightCluster> _clusters = new List<LightCluster>();
        private readonly Dictionary<Vector3Int, LightCluster> _cellToCluster = new Dictionary<Vector3Int, LightCluster>();
        private readonly Dictionary<Vector3Int, TileStateType> _litStates = new Dictionary<Vector3Int, TileStateType>();
        private readonly List<LightInstance> _activeTransients = new List<LightInstance>();

        // 拆分团块用的 Scratch：不逐次分配
        private readonly Queue<Vector3Int> _floodQueue = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> _floodVisited = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> _floodScratch = new List<Vector3Int>();
        private readonly List<Vector3Int> _neighbours = new List<Vector3Int>(4);

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private bool _attached;

        /// <summary>当前借出的点光源总数（连片地貌共用一盏，故远小于发光格数）</summary>
        public int ActiveLightCount => _clusters.Count + _activeTransients.Count;

        /// <summary>参与发光的格子总数；与 ActiveLightCount 的比值就是聚类省下的盏数</summary>
        public int LitCellCount => _litStates.Count;

        private void Awake()
        {
            _pool = new Pool<GameObject>(
                factory: CreateLightObject,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "TilePointLightPool",
                maxSize: maxPoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop,
                onDestroy: Destroy);
        }

        private void OnDestroy()
        {
            Detach();

            _pool?.Dispose();
            _pool = null;
        }

        /// <summary>装配几何、点光预制体与事件订阅；重复调用 no-op</summary>
        public void Attach()
        {
            if (_attached) return;

            if (tilemapAdapter == null) tilemapAdapter = FindObjectOfType<TilemapAdapter>();

            if (tilemapAdapter != null) _geometry = tilemapAdapter.ReadGeometry();

            if (Resources.Load<GameObject>(LightPrefabPath) == null)
            {
                Debug.LogError(
                    $"[Light] 找不到点光源预制体 Resources/{LightPrefabPath}：点光源不会生效。" +
                    "先跑 DeepseaOil ▸ 光照与后处理 ▸ 一键装配动态点光源系统。", this);

                return;
            }

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
            EventBus<GridBlastOccurred>.Subscribe(OnGridBlastOccurred);

            _attached = true;

#if UNITY_EDITOR
            VerifyLitLayers();
#endif
        }

        public void Detach()
        {
            if (!_attached) return;

            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
            EventBus<GridBlastOccurred>.Unsubscribe(OnGridBlastOccurred);

            _attached = false;

            ClearAll();
        }

        /// <summary>逐帧推进呼吸/抽搐与瞬态衰减；由 CombatRoot.RenderTick 驱动</summary>
        public void Tick(float time, float deltaTime)
        {
            for (int i = 0; i < _clusters.Count; i++)
            {
                LightCluster cluster = _clusters[i];

                if (cluster.Light == null) continue;

                if (cluster.Kind == LightKind.Fire)
                {
                    float wave = (Mathf.Sin(time * FireRate + cluster.PhaseOffset) + 1f) * 0.5f;

                    cluster.Light.intensity = Mathf.Lerp(FireFloor, FirePeak, wave);
                }
                else
                {
                    // 阶跃而非正弦：电弧要的是不连续的高频抽搐
                    float step = Mathf.Floor(time * ElectricRate + cluster.PhaseOffset);

                    cluster.Light.intensity = Mathf.Lerp(ElectricFloor, ElectricPeak, Mathf.Repeat(step * GoldenRatio, 1f));
                }
            }

            for (int i = _activeTransients.Count - 1; i >= 0; i--)
            {
                LightInstance inst = _activeTransients[i];
                inst.Elapsed += deltaTime;

                if (inst.Elapsed >= inst.Duration)
                {
                    RecycleInstance(inst);
                    _activeTransients.RemoveAt(i);

                    continue;
                }

                if (inst.Light != null) inst.Light.intensity = (1f - inst.Elapsed / inst.Duration) * inst.Intensity;
            }
        }

        /// <summary>归还全部借出的点光源；复活与关卡重开必须调用，否则旧光常驻并耗尽池位</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _clusters.Count; i++)
            {
                ReleaseCluster(_clusters[i]);
            }

            _clusters.Clear();
            _cellToCluster.Clear();
            _litStates.Clear();

            for (int i = 0; i < _activeTransients.Count; i++)
            {
                RecycleInstance(_activeTransients[i]);
            }

            _activeTransients.Clear();
        }

        /// <summary>地块状态到持续光的映射；未命中表示该状态不发光</summary>
        public static bool TryGetContinuousLight(TileStateType state, out LightKind kind, out Color color, out float radius, out float innerRadius)
        {
            switch (state)
            {
                case TileStateType.BasicFire:
                case TileStateType.FlameField:
                    kind = LightKind.Fire;
                    color = new Color(1f, 0.549f, 0.259f, 1f); // #FF8C42 暖橙红
                    radius = 1.8f;
                    innerRadius = 0.36f;
                    return true;

                case TileStateType.BasicElectricity:
                case TileStateType.ConductZone:
                case TileStateType.ChargedThorn:
                    kind = LightKind.Electric;
                    color = new Color(0.490f, 0.976f, 1f, 1f); // #7DF9FF 高亮青白
                    radius = 1.5f;
                    innerRadius = 0.30f;
                    return true;

                default:
                    kind = LightKind.Fire;
                    color = Color.white;
                    radius = 0f;
                    innerRadius = 0f;
                    return false;
            }
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            RemoveCell(evt.Cell);

            if (!TryGetContinuousLight(evt.State, out LightKind kind, out Color color, out float radius, out float innerRadius)) return;

            AddCell(evt.Cell, new LightRequest(kind, color, radius, innerRadius));
        }

        /// <summary>把发光格并进相邻团块（多块相邻则合并），没有相邻块就新开一盏</summary>
        private void AddCell(Vector3Int cell, in LightRequest request)
        {
            if (!_geometry.IsValid) return;

            _litStates[cell] = ToState(request.Kind);

            LightCluster target = null;

            CollectNeighbours(cell);

            for (int i = 0; i < _neighbours.Count; i++)
            {
                if (!_cellToCluster.TryGetValue(_neighbours[i], out LightCluster neighbour)) continue;

                if (target == null)
                {
                    target = neighbour;
                    continue;
                }

                if (!ReferenceEquals(target, neighbour)) MergeClusters(target, neighbour);
            }

            if (target == null)
            {
                target = new LightCluster
                {
                    Kind = request.Kind,
                    Color = request.Color,
                    Radius = request.Radius,

                    // 同团同相位：整片火海一起呼吸；相位由团块中心决定，相邻团错开
                    PhaseOffset = (cell.x * 17 + cell.y * 31) % 100 * 0.1f,
                };

                _clusters.Add(target);
            }

            target.Cells.Add(cell);
            _cellToCluster[cell] = target;

            RefreshClusterLight(target);
        }

        /// <summary>摘掉一格；团块被挖散时重新团块化，空的直接还灯</summary>
        private void RemoveCell(Vector3Int cell)
        {
            _litStates.Remove(cell);

            if (!_cellToCluster.TryGetValue(cell, out LightCluster cluster))
            {
                // 相邻格可能带着光但本格没记账，仍要尝试并入
                return;
            }

            _cellToCluster.Remove(cell);
            cluster.Cells.Remove(cell);

            ReclusterAfterSplit(cluster);

            if (cluster.Cells.Count == 0)
            {
                ReleaseCluster(cluster);
                _clusters.Remove(cluster);

                return;
            }

            RefreshClusterLight(cluster);
        }

        /// <summary>拿走一格后若团块断了，把断开的那部分拆成新团块（各自一盏灯）</summary>
        private void ReclusterAfterSplit(LightCluster cluster)
        {
            if (cluster.Cells.Count <= 1) return;

            _floodVisited.Clear();
            _floodQueue.Clear();
            _floodScratch.Clear();

            _floodQueue.Enqueue(cluster.Cells[0]);
            _floodVisited.Add(cluster.Cells[0]);

            while (_floodQueue.Count > 0)
            {
                Vector3Int current = _floodQueue.Dequeue();
                _floodScratch.Add(current);

                CollectNeighbours(current);

                for (int i = 0; i < _neighbours.Count; i++)
                {
                    Vector3Int next = _neighbours[i];

                    if (!_cellToCluster.TryGetValue(next, out LightCluster owner) || !ReferenceEquals(owner, cluster)) continue;
                    if (!_floodVisited.Add(next)) continue;

                    _floodQueue.Enqueue(next);
                }
            }

            if (_floodScratch.Count == cluster.Cells.Count) return;

            var split = new LightCluster
            {
                Kind = cluster.Kind,
                Color = cluster.Color,
                Radius = cluster.Radius,
                PhaseOffset = cluster.PhaseOffset,
            };

            for (int i = 0; i < _floodScratch.Count; i++)
            {
                Vector3Int member = _floodScratch[i];

                cluster.Cells.Remove(member);
                split.Cells.Add(member);
                _cellToCluster[member] = split;
            }

            _clusters.Add(split);
            RefreshClusterLight(split);
        }

        private void MergeClusters(LightCluster keep, LightCluster absorb)
        {
            for (int i = 0; i < absorb.Cells.Count; i++)
            {
                Vector3Int member = absorb.Cells[i];

                keep.Cells.Add(member);
                _cellToCluster[member] = keep;
            }

            absorb.Cells.Clear();
            ReleaseCluster(absorb);
            _clusters.Remove(absorb);
        }

        /// <summary>灯放在团块质心，半径覆盖到最远成员：一块火海共用一盏</summary>
        private void RefreshClusterLight(LightCluster cluster)
        {
            float cellSize = _geometry.CellSize;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < cluster.Cells.Count; i++)
            {
                Vector2 center = _geometry.CellCenter(cluster.Cells[i]);

                if (center.x < minX) minX = center.x;
                if (center.x > maxX) maxX = center.x;
                if (center.y < minY) minY = center.y;
                if (center.y > maxY) maxY = center.y;
            }

            cluster.Center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            cluster.ExtentX = maxX - minX;
            cluster.ExtentY = maxY - minY;

            float span = Mathf.Sqrt(cluster.ExtentX * cluster.ExtentX + cluster.ExtentY * cluster.ExtentY) * 0.5f;
            float radius = Mathf.Max(cluster.Radius, span + cellSize);
            float innerRadius = radius * (cluster.Radius > 0f ? cluster.Radius * 0.2f / cluster.Radius : 0.2f);

            if (cluster.Light == null)
            {
                if (!TryRentLight(cluster.Cells[0], cluster.Center, cluster.Color, radius, innerRadius, 1f, out GameObject go, out Light2D light)) return;

                cluster.Go = go;
                cluster.Light = light;

                return;
            }

            cluster.Go.transform.position = new Vector3(cluster.Center.x, cluster.Center.y, 0f);

            cluster.Light.pointLightOuterRadius = radius;
            cluster.Light.pointLightInnerRadius = innerRadius;
        }

        /// <summary>只认四邻：对角线相邻的两块地不共用一盏，避免光斑跨格跳动</summary>
        private void CollectNeighbours(Vector3Int cell)
        {
            _neighbours.Clear();

            _neighbours.Add(new Vector3Int(cell.x + 1, cell.y, 0));
            _neighbours.Add(new Vector3Int(cell.x - 1, cell.y, 0));
            _neighbours.Add(new Vector3Int(cell.x, cell.y + 1, 0));
            _neighbours.Add(new Vector3Int(cell.x, cell.y - 1, 0));
        }

        private static TileStateType ToState(LightKind kind)
        {
            return kind == LightKind.Fire ? TileStateType.BasicFire : TileStateType.BasicElectricity;
        }

        private void ReleaseCluster(LightCluster cluster)
        {
            if (cluster.Go == null || _pool == null) return;

            _pool.Release(cluster.Go);

            cluster.Go = null;
            cluster.Light = null;
        }

        private void OnGridBlastOccurred(GridBlastOccurred evt)
        {
            if (evt.ResultTile != TileStateType.Steam) return;

            if (!TryRentLight(Vector3Int.zero, evt.Center, Color.white, SteamRadius, SteamInnerRadius, 1f, out GameObject go, out Light2D light)) return;

            _activeTransients.Add(new LightInstance
            {
                Go = go,
                Light = light,
                Elapsed = 0f,
                Duration = SteamDuration,
            });
        }

        private bool TryRentLight(Vector3Int cell, Vector2 center, Color color, float radius, float innerRadius, float intensity, out GameObject go, out Light2D light)
        {
            go = null;
            light = null;

            if (_pool == null) return false;

            if (!_pool.TryGet(out go) || go == null) return false;

            light = go.GetComponent<Light2D>();

            if (light == null)
            {
                Debug.LogError($"[Light] 点光对象上没有 Light2D 组件，本格光已丢弃：{cell}", this);

                _pool.Release(go);
                go = null;

                return false;
            }

            go.transform.position = new Vector3(center.x, center.y, 0f);

            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = innerRadius;
            light.intensity = intensity;

            return true;
        }

        private GameObject CreateLightObject()
        {
            GameObject prefab = Resources.Load<GameObject>(LightPrefabPath);

            if (prefab == null) return null;

            GameObject go = Instantiate(prefab, transform);

            go.name = "TilePointLight";

            return go;
        }

        private void RecycleInstance(LightInstance inst)
        {
            if (inst.Go == null || _pool == null) return;

            _pool.Release(inst.Go);
        }

#if UNITY_EDITOR
        // 照射层没有公共访问器，只能读私有序列化字段做自检：配错会让点光漏照 Effect/UI 且不报错
        private void VerifyLitLayers()
        {
            GameObject prefab = Resources.Load<GameObject>(LightPrefabPath);

            if (prefab == null) return;

            Light2D light = prefab.GetComponent<Light2D>();

            if (light == null) return;

            var serialized = new UnityEditor.SerializedObject(light);
            UnityEditor.SerializedProperty layers = serialized.FindProperty("m_ApplyToSortingLayers");

            if (layers == null)
            {
                Debug.LogError("[Light] 读不到 Light2D 的照射层字段，点光隔离无法自检。", this);
                return;
            }

            int groundId = SortingLayer.NameToID(SortingLayerConsts.Ground);
            int actorId = SortingLayer.NameToID(SortingLayerConsts.Actor);

            bool groundFound = false;
            bool actorFound = false;

            for (int i = 0; i < layers.arraySize; i++)
            {
                int id = layers.GetArrayElementAtIndex(i).intValue;

                if (id == groundId) groundFound = true;
                if (id == actorId) actorFound = true;
            }

            if (groundFound && actorFound && layers.arraySize == 2) return;

            Debug.LogError(
                $"[Light] 点光预制体的照射层不是恰好 Ground+Actor（数组长度 {layers.arraySize}）：" +
                "点光会漏照 Effect/UI/Aim。重跑 一键装配动态点光源系统 修正。", this);
        }
#endif
    }
}
