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
    /// <summary>地块元素动态点光源调度器：孤立格逐格小灯，连片地貌整片共用一盏重心柔光灯</summary>
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

        // 呼吸与抽搐的调制基准：连片灯更暗更柔，孤立格才用满亮度
        private const float FireRate = 15.70796f;
        private const float IsolatedFireFloor = 0.85f;
        private const float IsolatedFirePeak = 1.05f;
        private const float GroupFireFloor = 0.35f;
        private const float GroupFirePeak = 0.48f;
        private const float ElectricRate = 18f;
        private const float IsolatedElectricFloor = 0.90f;
        private const float IsolatedElectricPeak = 1.20f;
        private const float GroupElectricFloor = 0.30f;
        private const float GroupElectricPeak = 0.45f;
        private const float GoldenRatio = 0.6180339f;
        private const float SteamRadius = 2.2f;
        private const float SteamInnerRadius = 0.5f;
        private const float SteamDuration = 0.32f;

        // 连片灯光斑上限：依据格数开平方自适应，防止散布很广的火海被一盏巨灯跨屏覆盖
        private const float FireGroupRadiusScale = 1.35f;
        private const float FireGroupRadiusMin = 3.8f;
        private const float FireGroupRadiusMax = 6.5f;
        private const float ElectricGroupRadiusScale = 1.25f;
        private const float ElectricGroupRadiusMin = 3.2f;
        private const float ElectricGroupRadiusMax = 5.5f;

        // 区域至少这么多格才够格共用一盏：低于此数逐格小灯更贴合形状
        private const int MinGroupCells = 3;

        [Header("容量上限")]
        [Tooltip("对象池总量上限；连片地貌共用一盏，16 盏足够覆盖数块火海")]
        [SerializeField] private int maxPoolSize = 16;

        [Tooltip("可选接线（留空自动抓取）")]
        [SerializeField] private TilemapAdapter tilemapAdapter = default;

        /// <summary>连片地貌共用的氛围灯：位置随几何重心走，半径随格数开平方走</summary>
        private sealed class GroupLight
        {
            public GameObject Go;
            public Light2D Light;

            public readonly List<Vector3Int> Members = new List<Vector3Int>();

            public float PhaseOffset;
            public bool Used;
        }

        private sealed class CellLight
        {
            public GameObject Go;
            public Light2D Light;
        }

        // 发光格是唯一真源，任何时候都记账：几何未就绪也不丢状态，就绪当帧一次性补建
        private readonly HashSet<Vector3Int> _fireCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> _electricCells = new HashSet<Vector3Int>();

        private readonly Dictionary<Vector3Int, CellLight> _isolatedFireLights = new Dictionary<Vector3Int, CellLight>();
        private readonly Dictionary<Vector3Int, CellLight> _isolatedElectricLights = new Dictionary<Vector3Int, CellLight>();

        private readonly List<GroupLight> _fireGroups = new List<GroupLight>();
        private readonly List<GroupLight> _electricGroups = new List<GroupLight>();
        private readonly List<Vector3Int> _staleCells = new List<Vector3Int>();

        private readonly List<TransientInstance> _activeTransients = new List<TransientInstance>();

        // 洪泛 Scratch：0 GC，一次只处理单一类别，故可与连通区域判定复用
        private readonly List<Vector3Int> _floodCells = new List<Vector3Int>();
        private readonly List<Vector3Int> _floodNeighbours = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> _floodVisited = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> _floodUsed = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> _floodQueue = new List<Vector3Int>();
        private readonly List<int> _groupRegionStarts = new List<int>();
        private readonly List<int> _groupRegionCounts = new List<int>();

        // 静态连通区域判定的 Scratch：扫描标记随每次调用自增，免去逐次分配 HashSet
        private static readonly Dictionary<Vector3Int, int> _regionMarks = new Dictionary<Vector3Int, int>();
        private static readonly List<Vector3Int> _regionQueue = new List<Vector3Int>();
        private static readonly List<Vector3Int> _regionScratch = new List<Vector3Int>();
        private static readonly List<Vector3Int> _regionNeighbours = new List<Vector3Int>();
        private static int _regionAttempt;

        private sealed class TransientInstance
        {
            public GameObject Go;
            public Light2D Light;
            public float Elapsed;
            public float Duration;
        }

        private Pool<GameObject> _pool;
        private GridGeometry _geometry;
        private bool _attached;
        private bool _geometryResolved;
        private bool _fireDirty;
        private bool _electricDirty;

        /// <summary>当前借出的点光源总数（连片地貌共用一盏，故远小于发光格数）</summary>
        public int ActiveLightCount => _isolatedFireLights.Count + _isolatedElectricLights.Count + _fireGroups.Count + _electricGroups.Count + _activeTransients.Count;

        /// <summary>参与发光的格子总数；与 ActiveLightCount 的差值就是连片合并省下的盏数</summary>
        public int LitCellCount => _fireCells.Count + _electricCells.Count;

        /// <summary>连片火海占用的氛围灯盏数；孤立火格各自一盏不计入</summary>
        public int FireGroupCount => CountConnectedRegions(_fireCells);

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

            if (Resources.Load<GameObject>(LightPrefabPath) == null)
            {
                Debug.LogError($"[Light] 找不到点光源预制体 Resources/{LightPrefabPath}：点光池已停用，缺失不影响网格光场。", this);

                return;
            }

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
            EventBus<GridBlastOccurred>.Subscribe(OnGridBlastOccurred);

            _attached = true;

            TryResolveGeometry();

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
            TryResolveGeometry();

            if (_fireDirty) RefreshLights(_fireCells, _fireGroups, LightKind.Fire);
            if (_electricDirty) RefreshLights(_electricCells, _electricGroups, LightKind.Electric);

            for (int i = 0; i < _fireGroups.Count; i++)
            {
                Light2D light = _fireGroups[i].Light;

                if (light == null) continue;

                light.intensity = Mathf.Lerp(GroupFireFloor, GroupFirePeak, Wave(time, _fireGroups[i].PhaseOffset));
            }

            for (int i = 0; i < _electricGroups.Count; i++)
            {
                Light2D light = _electricGroups[i].Light;

                if (light == null) continue;

                light.intensity = Mathf.Lerp(GroupElectricFloor, GroupElectricPeak, ElectricStep(time, _electricGroups[i].PhaseOffset));
            }

            // 相位取格坐标：相邻孤立格错开，不会整片同频闪
            foreach (KeyValuePair<Vector3Int, CellLight> kv in _isolatedFireLights)
            {
                if (kv.Value.Light == null) continue;

                kv.Value.Light.intensity = Mathf.Lerp(IsolatedFireFloor, IsolatedFirePeak, Wave(time, kv.Key.x));
            }

            foreach (KeyValuePair<Vector3Int, CellLight> kv in _isolatedElectricLights)
            {
                if (kv.Value.Light == null) continue;

                kv.Value.Light.intensity = Mathf.Lerp(IsolatedElectricFloor, IsolatedElectricPeak, ElectricStep(time, kv.Key.x));
            }

            for (int i = _activeTransients.Count - 1; i >= 0; i--)
            {
                TransientInstance inst = _activeTransients[i];
                inst.Elapsed += deltaTime;

                if (inst.Elapsed >= inst.Duration)
                {
                    if (inst.Go != null && _pool != null) _pool.Release(inst.Go);

                    _activeTransients.RemoveAt(i);

                    continue;
                }

                if (inst.Light != null) inst.Light.intensity = 1f - inst.Elapsed / inst.Duration;
            }
        }

        /// <summary>归还全部借出的点光源；复活与关卡重开必须调用，否则旧光常驻并耗尽池位</summary>
        public void ClearAll()
        {
            ReleaseGroups(_fireGroups);
            ReleaseGroups(_electricGroups);

            ReleaseIsolated(_isolatedFireLights);
            ReleaseIsolated(_isolatedElectricLights);

            for (int i = 0; i < _activeTransients.Count; i++)
            {
                if (_activeTransients[i].Go != null && _pool != null) _pool.Release(_activeTransients[i].Go);
            }

            _activeTransients.Clear();
            _fireCells.Clear();
            _electricCells.Clear();
            _floodUsed.Clear();
            _fireDirty = false;
            _electricDirty = false;
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

        /// <summary>按四邻接统计「够格共用一盏灯」的连片区域数；供灯数预算的机器判据使用</summary>
        /// <remarks>用扫描标记代替 HashSet 去重：诊断统计每秒都可能被调到，不能逐次分配</remarks>
        public static int CountConnectedRegions(ICollection<Vector3Int> cells)
        {
            if (cells == null || cells.Count < MinGroupCells) return 0;

            _regionAttempt++;
            _regionQueue.Clear();
            _regionScratch.Clear();

            int regions = 0;

            foreach (Vector3Int start in cells)
            {
                if (_regionMarks.TryGetValue(start, out int mark) && mark == _regionAttempt) continue;

                _regionMarks[start] = _regionAttempt;
                _regionQueue.Clear();
                _regionQueue.Add(start);

                int cursor = 0;

                while (cursor < _regionQueue.Count)
                {
                    Vector3Int current = _regionQueue[cursor];
                    cursor++;

                    _regionScratch.Add(current);

                    FillNeighbours(current, _regionNeighbours);

                    for (int i = 0; i < _regionNeighbours.Count; i++)
                    {
                        Vector3Int next = _regionNeighbours[i];

                        if (!cells.Contains(next)) continue;
                        if (_regionMarks.TryGetValue(next, out int visitedMark) && visitedMark == _regionAttempt) continue;

                        _regionMarks[next] = _regionAttempt;
                        _regionQueue.Add(next);
                    }
                }

                if (cursor >= MinGroupCells) regions++;
            }

            return regions;
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            bool wasFire = _fireCells.Remove(evt.Cell);
            bool wasElectric = _electricCells.Remove(evt.Cell);

            if (!TryGetContinuousLight(evt.State, out LightKind kind, out _, out _, out _)) return;

            if (kind == LightKind.Fire)
            {
                _fireCells.Add(evt.Cell);
                _fireDirty = true;
            }
            else
            {
                _electricCells.Add(evt.Cell);
                _electricDirty = true;
            }

            // 同格由火转电时另一类的灯也要跟着撤，否则两种光会在同一格叠住
            if (wasFire) _fireDirty = true;
            if (wasElectric) _electricDirty = true;
        }

        private void OnGridBlastOccurred(GridBlastOccurred evt)
        {
            if (evt.ResultTile != TileStateType.Steam) return;

            if (!TryRentTransient(evt.Center, out GameObject go, out Light2D light)) return;

            _activeTransients.Add(new TransientInstance
            {
                Go = go,
                Light = light,
                Elapsed = 0f,
                Duration = SteamDuration,
            });
        }

        /// <summary>几何由 TilemapAdapter 迟提供：抓不到就每帧重试，绝不把已发生的状态变化丢掉</summary>
        private void TryResolveGeometry()
        {
            if (_geometryResolved) return;

            if (tilemapAdapter == null) tilemapAdapter = FindObjectOfType<TilemapAdapter>();

            if (tilemapAdapter == null) return;

            GridGeometry geometry = tilemapAdapter.ReadGeometry();

            if (!geometry.IsValid) return;

            _geometry = geometry;
            _geometryResolved = true;

            _fireDirty = true;
            _electricDirty = true;
        }

        /// <summary>把一类发光格重建成「连片各一盏 + 孤立各一盏」；只在结构变化的那一帧跑</summary>
        private void RefreshLights(HashSet<Vector3Int> cells, List<GroupLight> groups, LightKind kind)
        {
            if (kind == LightKind.Fire) _fireDirty = false;
            else _electricDirty = false;

            if (!_geometryResolved) return;

            CollectConnectedCells(cells);

            for (int i = 0; i < groups.Count; i++) groups[i].Used = false;

            RebuildGroupLights(groups, kind);
            RebuildIsolatedLights(cells, kind);

            // 用不上的整片灯当帧就归还：留在列表里会一直占着池位
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i].Used) continue;

                if (groups[i].Go != null && _pool != null) _pool.Release(groups[i].Go);

                groups.RemoveAt(i);
            }
        }

        /// <summary>按四邻接把发光格切成孤立格与连片区域，结果落在 _floodCells 与 _floodUsed</summary>
        private void CollectConnectedCells(HashSet<Vector3Int> cells)
        {
            _floodCells.Clear();
            _floodUsed.Clear();
            _floodVisited.Clear();
            _groupRegionStarts.Clear();
            _groupRegionCounts.Clear();

            foreach (Vector3Int start in cells)
            {
                if (!_floodVisited.Add(start)) continue;

                _floodQueue.Clear();
                _floodQueue.Add(start);

                int regionStart = _floodCells.Count;
                int cursor = 0;

                while (cursor < _floodQueue.Count)
                {
                    Vector3Int current = _floodQueue[cursor];
                    cursor++;

                    _floodCells.Add(current);

                    FillNeighbours(current, _floodNeighbours);

                    for (int i = 0; i < _floodNeighbours.Count; i++)
                    {
                        Vector3Int next = _floodNeighbours[i];

                        if (!cells.Contains(next) || !_floodVisited.Add(next)) continue;

                        _floodQueue.Add(next);
                    }
                }

                // 一个区域统一分流：够格则整片共用一盏，否则逐格小灯
                if (cursor - regionStart < MinGroupCells) continue;

                _groupRegionStarts.Add(regionStart);
                _groupRegionCounts.Add(cursor - regionStart);

                for (int i = regionStart; i < _floodCells.Count; i++) _floodUsed.Add(_floodCells[i]);
            }
        }

        private void RebuildGroupLights(List<GroupLight> groups, LightKind kind)
        {
            for (int i = 0; i < _groupRegionStarts.Count; i++)
            {
                BuildOneGroup(groups, kind, _groupRegionStarts[i], _groupRegionCounts[i]);
            }
        }

        /// <summary>整片连片区域共用一盏：灯落在成员格心包围盒的中心，半径按格数开平方自适应</summary>
        private void BuildOneGroup(List<GroupLight> groups, LightKind kind, int start, int count)
        {
            GroupLight group = RentGroup(groups);

            if (group == null) return;

            group.Members.Clear();

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = start; i < start + count; i++)
            {
                Vector3Int cell = _floodCells[i];

                group.Members.Add(cell);

                Vector2 center = _geometry.CellCenter(cell);

                if (center.x < minX) minX = center.x;
                if (center.x > maxX) maxX = center.x;
                if (center.y < minY) minY = center.y;
                if (center.y > maxY) maxY = center.y;
            }

            group.Go.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);

            // 相位由重心决定：相邻火海错开呼吸，不会整片同频闪
            group.PhaseOffset = ((minX + maxX) * 0.5f) % 6.2832f;

            float radius = kind == LightKind.Fire
                ? Mathf.Clamp(Mathf.Sqrt(count) * FireGroupRadiusScale, FireGroupRadiusMin, FireGroupRadiusMax)
                : Mathf.Clamp(Mathf.Sqrt(count) * ElectricGroupRadiusScale, ElectricGroupRadiusMin, ElectricGroupRadiusMax);

            group.Light.color = kind == LightKind.Fire
                ? new Color(1f, 0.52f, 0.20f, 1f)
                : new Color(0.490f, 0.976f, 1f, 1f);

            group.Light.pointLightOuterRadius = radius;
            group.Light.pointLightInnerRadius = radius * (kind == LightKind.Fire ? 0.45f : 0.4f);
        }

        private void RebuildIsolatedLights(HashSet<Vector3Int> cells, LightKind kind)
        {
            Dictionary<Vector3Int, CellLight> map = kind == LightKind.Fire ? _isolatedFireLights : _isolatedElectricLights;
            Color color = kind == LightKind.Fire ? new Color(1f, 0.549f, 0.259f, 1f) : new Color(0.490f, 0.976f, 1f, 1f);
            float radius = kind == LightKind.Fire ? 1.8f : 1.5f;
            float innerRadius = kind == LightKind.Fire ? 0.36f : 0.30f;

            _floodVisited.Clear();

            foreach (Vector3Int cell in cells)
            {
                if (_floodUsed.Contains(cell)) continue;

                _floodVisited.Add(cell);

                if (!map.TryGetValue(cell, out CellLight instance))
                {
                    if (!TryRentLight(cell, color, radius, innerRadius, out GameObject go, out Light2D light)) continue;

                    instance = new CellLight { Go = go, Light = light };
                    map[cell] = instance;
                }

                Vector2 center = _geometry.CellCenter(cell);

                instance.Go.transform.position = new Vector3(center.x, center.y, 0f);
                instance.Light.color = color;
                instance.Light.pointLightOuterRadius = radius;
                instance.Light.pointLightInnerRadius = innerRadius;
            }

            _staleCells.Clear();

            foreach (KeyValuePair<Vector3Int, CellLight> kv in map)
            {
                if (_floodVisited.Contains(kv.Key)) continue;

                _staleCells.Add(kv.Key);
            }

            for (int i = 0; i < _staleCells.Count; i++)
            {
                if (!map.TryGetValue(_staleCells[i], out CellLight stale)) continue;

                if (stale.Go != null && _pool != null) _pool.Release(stale.Go);

                map.Remove(_staleCells[i]);
            }
        }

        /// <summary>同一片区域跨帧沿用同一盏灯：不按帧 churn 对象池</summary>
        private GroupLight RentGroup(List<GroupLight> groups)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Used) continue;

                groups[i].Used = true;

                return groups[i];
            }

            if (!TryRentLight(default, Color.white, 1f, 0.2f, out GameObject go, out Light2D light)) return null;

            var created = new GroupLight { Go = go, Light = light, Used = true };
            groups.Add(created);

            return created;
        }

        private void ReleaseGroups(List<GroupLight> groups)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Go != null && _pool != null) _pool.Release(groups[i].Go);
            }

            groups.Clear();
        }

        private void ReleaseIsolated(Dictionary<Vector3Int, CellLight> map)
        {
            foreach (KeyValuePair<Vector3Int, CellLight> kv in map)
            {
                if (kv.Value.Go != null && _pool != null) _pool.Release(kv.Value.Go);
            }

            map.Clear();
        }

        private bool TryRentLight(Vector3Int cell, Color color, float radius, float innerRadius, out GameObject go, out Light2D light)
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

            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = innerRadius;
            light.intensity = 1f;

            return true;
        }

        private bool TryRentTransient(Vector2 center, out GameObject go, out Light2D light)
        {
            if (!TryRentLight(Vector3Int.zero, Color.white, SteamRadius, SteamInnerRadius, out go, out light)) return false;

            go.transform.position = new Vector3(center.x, center.y, 0f);

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

        private static float Wave(float time, float phase)
        {
            return (Mathf.Sin(time * FireRate + phase) + 1f) * 0.5f;
        }

        /// <summary>阶跃而非正弦：电弧要的是不连续的高频抽搐</summary>
        private static float ElectricStep(float time, float phase)
        {
            return Mathf.Repeat(Mathf.Floor(time * ElectricRate + phase) * GoldenRatio, 1f);
        }

        private static void FillNeighbours(Vector3Int cell, List<Vector3Int> into)
        {
            into.Clear();

            into.Add(new Vector3Int(cell.x + 1, cell.y, 0));
            into.Add(new Vector3Int(cell.x - 1, cell.y, 0));
            into.Add(new Vector3Int(cell.x, cell.y + 1, 0));
            into.Add(new Vector3Int(cell.x, cell.y - 1, 0));
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
