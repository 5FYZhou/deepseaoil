using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;
using cfg.dso;

namespace DeepseaOil.Presentation.Adapters
{
    /// 格子系统与 Tilemap 的适配器：地板与几何进逻辑层，状态画出来
    /// 唯一认识 Tilemap 的地方；订阅由组合根收口（Attach/Detach）。效果贴图只盖状态、不改地板。
    public sealed class TilemapAdapter : MonoBehaviour
    {
        [Serializable]
        public struct StateTileBinding
        {
            [Tooltip("格子状态（tile_state 表的 id）")]
            public TileStateType State;

            [Tooltip("该状态在效果层上用的贴图；留空 = 走 `tiles/Tile_<状态>` 约定懒加载")]
            public TileBase Tile;
        }

        [Serializable]
        public struct EffectLayerBinding
        {
            [Tooltip("这一层的 Tilemap。同一格的状态贴图只画在它归属的那一层里")]
            public Tilemap Tilemap;

            [Tooltip("这一层负责哪些状态；同一状态只能出现在一层。留空 = 只作兜底层，不主动接状态")]
            public TileStateType[] States;
        }

        [Tooltip("地板层：提供格子几何与合法格集合。必接。")]
        [SerializeField] private Tilemap groundTilemap = default;

        [Tooltip("效果层（动效分组）：每层一张 Tilemap、一个材质；同一状态只登记在一层。")]
        [SerializeField] private EffectLayerBinding[] effectLayers = new EffectLayerBinding[0];

        [Tooltip("未在任何效果层登记的状态落到这一层；留空 = 取 effectLayers[0]")]
        [SerializeField] private Tilemap fallbackEffectTilemap = default;

        [Tooltip("状态 → 贴图（特例覆盖）。命中即用；未配的状态按 tiles/Tile_<状态> 的约定懒加载。")]
        [SerializeField] private StateTileBinding[] stateTiles = new StateTileBinding[0];

        [Tooltip("初始地块编辑层（可选）：用笔刷在此绘制开局特殊地块。读取后自动隐藏")]
        [SerializeField] private Tilemap initialSetupTilemap = default;

        [Header("装饰件（留空则自愈挂载）")]
        [Tooltip("覆盖物立面管理器")]
        [SerializeField] private TileOverlayDirector overlayDirector = default;

        [Tooltip("物理阻挡体管理器")]
        [SerializeField] private TileObstacleDirector obstacleDirector = default;

        /// 每格只记一层；前提：效果层开局全空，刷过 tile 改按每层记原值。
        private readonly Dictionary<Vector3Int, Tilemap> _cellLayer = new();

        /// 同状态只能进一层；重复在 Awake/OnValidate 报错。
        private readonly Dictionary<TileStateType, Tilemap> _stateToLayer = new();

        private bool _warnedNoLayer;

        private bool _attached;

        /// 热路径缓存：只缓存取到过的贴图，避免反复寻址资源系统。
        private readonly Dictionary<TileStateType, TileBase> _runtimeTileCache = new();

        /// -1 = 还没载入；读完该层会被 SetActive(false)。
        private int _initialSetupLoaded = -1;

        public bool IsWired => groundTilemap != null;

        /// 先自愈装饰件再订阅；必须在灌入初始状态之前调。
        public void Attach()
        {
            if (_attached) return;

            GridGeometry geometry = ReadGeometry();

            EnsureOverlay(in geometry);

            EnsureObstacle(in geometry);

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);

            _attached = true;
        }

        public void Detach()
        {
            if (!_attached) return;

            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);

            overlayDirector?.Detach();

            obstacleDirector?.Detach();

            _attached = false;
        }

        /// 清空全部装饰件实例；切场景/清场用
        public void ClearAll()
        {
            overlayDirector?.ClearAll();

            obstacleDirector?.ClearAll();
        }

        /// 自愈装配覆盖物管理器；几何非法时跳过
        private void EnsureOverlay(in GridGeometry geometry)
        {
            if (!geometry.IsValid) return;

            if (overlayDirector == null) overlayDirector = GetComponent<TileOverlayDirector>();

            if (overlayDirector == null) overlayDirector = gameObject.AddComponent<TileOverlayDirector>();

            overlayDirector.Initialize(in geometry);

            overlayDirector.Attach();
        }

        /// 自愈装配阻挡体管理器；几何非法时跳过
        private void EnsureObstacle(in GridGeometry geometry)
        {
            if (!geometry.IsValid) return;

            if (obstacleDirector == null) obstacleDirector = GetComponent<TileObstacleDirector>();

            if (obstacleDirector == null) obstacleDirector = gameObject.AddComponent<TileObstacleDirector>();

            obstacleDirector.Initialize(in geometry);

            obstacleDirector.Attach();
        }

        /// 角点语义与 GridGeometry 对齐：CellToWorld(zero)=格左下角、GetCellCenterWorld(zero)=格心，差半格。
        public GridGeometry ReadGeometry()
        {
            if (groundTilemap == null) return default;

            Vector3 cellSize = groundTilemap.cellSize;

            if (Mathf.Abs(cellSize.x - cellSize.y) > 1e-4f)
            {
                Debug.LogError(
                    $"[Grid] 地板 Tilemap 的格子不是正方形（{cellSize.x} × {cellSize.y}）。" +
                    "格子系统只支持正方形格，几何按 x 取值。", this);
            }

            Vector3 corner = groundTilemap.CellToWorld(Vector3Int.zero);

            var geometry = new GridGeometry(new Vector2(corner.x, corner.y), cellSize.x);

            VerifyGeometry(geometry);

            return geometry;
        }

        /// <summary>地板层已画格子的矩形边界；空层返回 false（空 Tilemap 的 cellBounds 是 1×1×1）</summary>
        public bool TryGetCellBounds(out BoundsInt bounds)
        {
            bounds = default;

            if (groundTilemap == null || groundTilemap.GetUsedTilesCount() == 0) return false;

            bounds = groundTilemap.cellBounds;

            return bounds.size.x > 0 && bounds.size.y > 0;
        }

        /// 把地板层全部格子登记进逻辑层，只有登记过的格才能被砸出状态、被减速
        public int RegisterCells(GridLogic grid)
        {
            if (grid == null || groundTilemap == null) return 0;

            int count = 0;

            BoundsInt bounds = groundTilemap.cellBounds;

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (groundTilemap.GetTile(cell) == null) continue;

                grid.RegisterCell(cell);
                count++;
            }

            if (count == 0)
            {
                Debug.LogError(
                    "[Grid] 地板 Tilemap 上一个 tile 都没有：落点不会产生任何效果。" +
                    "检查 TilemapAdapter 的 groundTilemap 是否指向了画好地板的那一层。", this);
            }

            return count;
        }

        /// 读 InitialSetup 层刷的地块进逻辑层，读完关掉该层，返回切了状态的格数
        /// 策划用笔刷画开局地块的入口（替代 tile_initial.xlsx）；必须在 Attach() 之后调；重复调用返回首次结果；返回 0 = 没接线或全是 Normal。
        public int LoadInitialSetupTiles(GridLogic grid)
        {
            if (initialSetupTilemap == null || grid == null) return 0;

            if (_initialSetupLoaded >= 0) return _initialSetupLoaded;

            int count = 0;

            BoundsInt bounds = initialSetupTilemap.cellBounds;

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                TileBase tile = initialSetupTilemap.GetTile(cell);

                if (tile == null) continue;

                if (!grid.HasCell(cell))
                {
                    Debug.LogWarning(
                        $"[Grid] InitialSetup 层在格 {cell} 刷了地块，但这一格没有地板（不在地板层上）：已忽略。" +
                        "两张 Tilemap 必须挂在同一个 Grid 下，否则坐标对不上。", this);

                    continue;
                }

                TileStateType state = ResolveSetupState(tile);

                grid.RegisterCell(cell);

                if (state == TileStateType.Normal) continue;

                if (grid.SwitchTileState(cell, state)) count++;
            }

            _initialSetupLoaded = count;

            // 关层：初始地块只是编辑期输入，留着白吃一次渲染
            initialSetupTilemap.gameObject.SetActive(false);

            return count;
        }

        /// InitialSetup 贴图 → 状态：先查 stateTiles，未命中按资产名解析
        /// 顺序不能换：先剥 Tile_ 前缀，失败再用资产全名（Object.name 是资产 m_Name）。
        private TileStateType ResolveSetupState(TileBase tile)
        {
            if (stateTiles != null)
            {
                for (int i = 0; i < stateTiles.Length; i++)
                {
                    if (stateTiles[i].Tile != tile) continue;

                    return stateTiles[i].State;
                }
            }

            const string prefix = "Tile_";

            string name = tile.name;

            if (name.StartsWith(prefix, StringComparison.Ordinal) && Enum.TryParse(name.Substring(prefix.Length), out TileStateType stripped))
            {
                return stripped;
            }

            if (Enum.TryParse(name, out TileStateType exact)) return exact;

            Debug.LogWarning(
                $"[Grid] InitialSetup 层的贴图「{name}」按名字解析不出地块状态：该格只当普通地板处理。" +
                "要么把资产名改成状态的枚举名（如 Mud / BasicFire），要么在 stateTiles 里给它配一行。", this);

            return TileStateType.Normal;
        }

        private void Awake()
        {
            // 接线自检：这几条错误的共同点是不报错也能跑
            if (groundTilemap == null)
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter.groundTilemap 未接线：格子系统拿不到几何，落点不会产生任何效果。", this);
            }

            ValidateEffectLayers();

            BuildStateLayerMap();

            // InitialSetup 读完会 SetActive(false)，指错层会误关地板/效果层
            if (initialSetupTilemap != null && initialSetupTilemap == groundTilemap)
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter 的 initialSetupTilemap 与地板层指向了同一层：" +
                    "初始地块读完会把这个物体整个 SetActive(false)，地板会一起消失。请单独建一个 InitialSetup 层。", this);
            }

            ValidateStateTiles();
        }

        private void ValidateEffectLayers()
        {
            int usable = 0;

            if (effectLayers != null)
            {
                for (int i = 0; i < effectLayers.Length; i++)
                {
                    Tilemap layer = effectLayers[i].Tilemap;

                    // 空槽合法：没接层就跳过
                    if (layer == null) continue;

                    usable++;

                    if (layer == groundTilemap)
                    {
                        Debug.LogError(
                            $"[Grid] TilemapAdapter.effectLayers 第 {i} 行与 groundTilemap 指向了同一层：状态结束时会把地板一起擦掉" +
                            "（表现为地上出现一块空洞）。请用不同的 Tilemap。", this);
                    }

                    if (initialSetupTilemap != null && layer == initialSetupTilemap)
                    {
                        Debug.LogError(
                            $"[Grid] TilemapAdapter.effectLayers 第 {i} 行与 initialSetupTilemap 指向了同一层：" +
                            "初始地块读完会把这个物体整个 SetActive(false)，效果层会一起消失。请单独建一个 InitialSetup 层。", this);
                    }

                    ValidateLayerStates(i);
                }
            }

            if (fallbackEffectTilemap != null)
            {
                if (fallbackEffectTilemap == groundTilemap)
                {
                    Debug.LogError(
                        "[Grid] TilemapAdapter 的 fallbackEffectTilemap 与地板层指向了同一层：" +
                        "未登记的状态会画到地板上，回到 Normal 时还会把地板擦掉。请指向一张效果层。", this);
                }

                if (fallbackEffectTilemap == initialSetupTilemap)
                {
                    Debug.LogError(
                        "[Grid] TilemapAdapter 的 fallbackEffectTilemap 与 initialSetupTilemap 指向了同一层：" +
                        "初始地块读完会把这层整个 SetActive(false)，兜底的状态贴图看不见。请单独建一个 InitialSetup 层。", this);
                }
            }

            if (usable == 0 && fallbackEffectTilemap == null)
            {
                Debug.LogWarning(
                    "[Grid] TilemapAdapter 的 effectLayers 一层都没接、fallbackEffectTilemap 也留空：" +
                    "状态变化只记逻辑、不显示（泥浆不会出现）。至少给一层接上 Tilemap。", this);

                return;
            }

            if (usable > 0 && fallbackEffectTilemap == null && effectLayers[0].Tilemap == null)
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter 的 fallbackEffectTilemap 留空、effectLayers[0] 也没接 Tilemap：" +
                    "没在任何层登记的状态无处可落（只记逻辑、不显示）。给第 0 层接上 Tilemap，或显式配 fallbackEffectTilemap。", this);
            }
        }

        private void ValidateLayerStates(int index)
        {
            TileStateType[] states = effectLayers[index].States;

            if (states == null) return;

            for (int j = 0; j < states.Length; j++)
            {
                TileStateType state = states[j];

                if (state == TileStateType.Normal)
                {
                    Debug.LogError(
                        $"[Grid] TilemapAdapter.effectLayers 第 {index} 行登记了 Normal（= 擦除）：效果层上「回到原样」由逻辑直接触发，不该绑层。" +
                        "如果这一层想画的是泥浆，把状态改成 Mud（表 id = 2）。", this);
                }

                if (state == TileStateType.None)
                {
                    Debug.LogWarning(
                        $"[Grid] TilemapAdapter.effectLayers 第 {index} 行的 States 里有 None（默认值，多半是没选）：这一项永远不会被用到。", this);
                }

                for (int k = 0; k < index; k++)
                {
                    TileStateType[] earlier = effectLayers[k].States;

                    if (earlier == null || Array.IndexOf(earlier, state) < 0) continue;

                    Debug.LogError(
                        $"[Grid] TilemapAdapter.effectLayers 里 {state} 同时登记在第 {k} 层与第 {index} 层：" +
                        "只会落到先查到的那一层，另一层永远收不到它。", this);

                    break;
                }
            }
        }

        private void BuildStateLayerMap()
        {
            _stateToLayer.Clear();

            if (effectLayers == null) return;

            for (int i = 0; i < effectLayers.Length; i++)
            {
                Tilemap layer = effectLayers[i].Tilemap;

                TileStateType[] states = effectLayers[i].States;

                if (layer == null || states == null) continue;

                for (int j = 0; j < states.Length; j++)
                {
                    if (states[j] == TileStateType.Normal || states[j] == TileStateType.None) continue;

                    if (_stateToLayer.ContainsKey(states[j])) continue;

                    _stateToLayer[states[j]] = layer;
                }
            }
        }

#if UNITY_EDITOR
        /// 只读配置数组，不建映射：OnValidate 调用频繁。
        private void OnValidate()
        {
            ValidateStateTiles();

            ValidateEffectLayers();
        }
#endif

        /// 校验 stateTiles：Normal 与重复状态必须报出来
        /// Normal 在事件里当擦除；重复状态只取第一条
        private void ValidateStateTiles()
        {
            // 空表合法（全走 tiles/Tile_{状态}）
            if (stateTiles == null || stateTiles.Length == 0) return;

            for (int i = 0; i < stateTiles.Length; i++)
            {
                StateTileBinding binding = stateTiles[i];

                if (binding.State == TileStateType.Normal)
                {
                    Debug.LogError(
                        $"[Grid] TilemapAdapter.stateTiles 第 {i} 行绑的是 Normal（= 擦除）：效果层上「回到原样」由逻辑直接触发，不需要（也不该有）贴图绑定。" +
                        "如果这一行想画的是泥浆，把 State 改成 Mud（表 id = 2）。", this);
                }

                if (binding.State == TileStateType.None)
                {
                    Debug.LogWarning(
                        $"[Grid] TilemapAdapter.stateTiles 第 {i} 行的 State 是 None（默认值，多半是没选）：这一行永远不会被用到。", this);
                }

                for (int j = i + 1; j < stateTiles.Length; j++)
                {
                    if (stateTiles[j].State != binding.State) continue;

                    Debug.LogError(
                        $"[Grid] TilemapAdapter.stateTiles 里 {binding.State} 出现了两次（第 {i} 行与第 {j} 行）：只会取第一条，后面的静默失效。", this);
                }
            }
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            // 先擦旧层：状态换组后旧层会留残影
            if (_cellLayer.TryGetValue(evt.Cell, out Tilemap previous))
            {
                if (previous != null) previous.SetTile(evt.Cell, null);

                _cellLayer.Remove(evt.Cell);
            }

            if (evt.State == TileStateType.Normal) return;

            Tilemap target = ResolveLayer(evt.State);

            if (target == null)
            {
                WarnNoLayer();

                return;
            }

            TileBase tile = TileFor(evt.State);

            if (tile == null) return;

            target.SetTile(evt.Cell, tile);

            _cellLayer[evt.Cell] = target;
        }

        private Tilemap ResolveLayer(TileStateType state)
        {
            if (_stateToLayer.TryGetValue(state, out Tilemap layer)) return layer;

            if (fallbackEffectTilemap != null) return fallbackEffectTilemap;

            if (effectLayers == null || effectLayers.Length == 0) return null;

            return effectLayers[0].Tilemap;
        }

        private void WarnNoLayer()
        {
            if (_warnedNoLayer) return;

            _warnedNoLayer = true;

            Debug.LogWarning(
                "[Grid] TilemapAdapter 没有可用的效果层：状态只记逻辑、不显示。" +
                "检查 effectLayers 是否接了 Tilemap。", this);
        }

        /// 状态 → 效果层贴图：特例优先，未配的按约定懒加载；取不到只警告一次并返回 null
        private TileBase TileFor(TileStateType state)
        {
            if (stateTiles != null)
            {
                for (int i = 0; i < stateTiles.Length; i++)
                {
                    if (stateTiles[i].State != state) continue;

                    // 绑了状态但贴图留空 = 明确要求逻辑生效但不显示
                    if (stateTiles[i].Tile == null) return null;

                    return stateTiles[i].Tile;
                }
            }

            if (_runtimeTileCache.TryGetValue(state, out TileBase cached)) return cached;

            // 资源系统没起来时不寻址，否则每次状态变化都抛异常
            if (!AssetModule.IsInitialized) return null;

            string key = $"tiles/Tile_{state}";
            TileBase tile = AssetModule.Load<TileBase>(key);

            if (tile == null)
            {
                Debug.LogWarning($"[Grid] 未找到地块资源: {key}，该格将不显示覆盖贴图。");

                return null;
            }

            _runtimeTileCache[state] = tile;

            return tile;
        }

        private void VerifyGeometry(in GridGeometry geometry)
        {
            Vector3 center = groundTilemap.GetCellCenterWorld(Vector3Int.zero);
            Vector2 expected = geometry.CellCenter(Vector3Int.zero);

            if (Vector2.Distance(new Vector2(center.x, center.y), expected) <= 1e-3f) return;

            Debug.LogError(
                $"[Grid] 格子几何自检失败：CellToWorld(0,0) + 半格 = {expected}，" +
                $"而 GetCellCenterWorld(0,0) = ({center.x}, {center.y})。" +
                "两者的差会让所有落点整体偏移，检查 Grid 的 Cell Size / Tile Anchor。", this);
        }
    }
}
