using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;
using cfg.dso;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>格子系统与 Unity Tilemap 的适配器，把地板格子与几何灌进逻辑层，把状态变化画出来</summary>
    /// <remarks>本文件是唯一认识 Tilemap 的地方，逻辑层零引擎类型，几何折算只在这里发生。订阅时机由组合根收口（Attach/Detach），不自己 OnEnable 订阅。地板层提供格集合与几何，效果层盖状态贴图。效果贴图不改地板，否则分不清"本来是泥"还是"被打成泥"。状态 → 贴图先查 stateTiles（特例覆盖），未配的按 `tiles/Tile_{状态}` 约定懒加载；新增状态把贴图放进约定路径即可，Inspector 不必再加行。</remarks>
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

        [Tooltip("地板层：提供格子几何与合法格集合。必接。")]
        [SerializeField] private Tilemap groundTilemap = default;

        [Tooltip("效果层：状态贴图盖在这一层。必接。")]
        [SerializeField] private Tilemap effectTilemap = default;

        [Tooltip("状态 → 贴图（特例覆盖）。命中即用；未配的状态按 tiles/Tile_<状态> 的约定懒加载。")]
        [SerializeField] private StateTileBinding[] stateTiles = new StateTileBinding[0];

        [Tooltip("初始地块编辑层（可选）：用笔刷在此绘制开局特殊地块。读取后自动隐藏")]
        [SerializeField] private Tilemap initialSetupTilemap = default;

        /// <remarks>只记第一次覆盖，否则第二次覆盖会把"泥浆"当原值，这一格永远回不到原样且不报错。前提：效果层只有 Show/Restore 两个写者，别处改动会被还原盖掉；effectTilemap 被换掉时旧记录会贴到新层上。原值取自 effectTilemap，拿地板图还原会抹掉效果层装饰。</remarks>
        private readonly Dictionary<Vector3Int, TileBase> _previousTiles = new();

        /// <summary>懒加载到的状态贴图，状态 → 贴图</summary>
        /// <remarks>存在的理由：TileFor 在"格子状态变了"的热路径上（战斗里成片变泥），每次寻址都会走一遍资源系统。这里只缓存**取到过**的贴图：未配置的状态在 stateTiles 里已有特例覆盖，不进这张表，字典保持很小。</remarks>
        private readonly Dictionary<TileStateType, TileBase> _runtimeTileCache = new();

        /// <summary>InitialSetup 层首次载入的结果，-1 = 还没载入</summary>
        /// <remarks>读完那层会被 SetActive(false)，再扫一遍既拿不到数据（Tilemap 不保证对非激活对象可见）也会让调用方以为"这次读到 0"。</remarks>
        private int _initialSetupLoaded = -1;

        public bool IsWired => groundTilemap != null;

        /// <summary>开始听"格子状态变了"，必须在灌入初始状态之前调，否则那批初始泥浆不会被画出来</summary>
        public void Attach()
        {
            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
        }

        public void Detach()
        {
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
        }

        /// <summary>读一次格子几何</summary>
        /// <remarks>角点语义必须与 GridGeometry 对齐：CellToWorld(zero) 是格 (0,0) 左下角、GetCellCenterWorld(zero) 是格心，差半格，用错会让全场落点整体偏半格且难倒推，故对齐后自检一次并报错。</remarks>
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

        /// <summary>把地板层全部格子登记进逻辑层，只有登记过的格才能被砸出状态、被减速</summary>
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

        /// <summary>把 InitialSetup 编辑层上刷出来的地块读进逻辑层（含开局状态），读完当场关掉该层，返回真正切了状态的格数</summary>
        /// <remarks>
        /// 关卡设计的入口：策划在编辑器里用笔刷画"开局就有的火池／冰面／植物"，不再手填 tile_initial.xlsx。
        /// 调用时机必须在 Attach() 之后：那批初始泥浆靠 TileStateChanged 事件画到效果层上，订阅晚一步就只能看见逻辑没有贴图。
        /// 本层只在编辑器里有意义，运行时 SetActive(false) 掉，省掉一次多余的 Tilemap 渲染。
        /// 读一次就够：第二次调用直接返回首次结果，不重扫也不重复报日志（同一场景里可能有两个驱动器各调一次）。
        /// 返回 0 有两种含义：这层没接线／没刷东西（调用方据此退回表驱动），或刷的恰好全是 Normal。
        /// </remarks>
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

                grid.RegisterCell(cell, state);

                if (state == TileStateType.Normal) continue;

                if (grid.SwitchState(cell, state, applyEnterImpact: false)) count++;
            }

            _initialSetupLoaded = count;

            // 读完立刻关层：初始地块只是编辑期的输入，留着会白吃一次 Tilemap 渲染
            initialSetupTilemap.gameObject.SetActive(false);

            Debug.Log($"[Grid] 从 InitialSetup 层成功载入 {count} 个初始特殊地块。");

            return count;
        }

        /// <summary>InitialSetup 层的贴图 → 状态：先查 stateTiles 特例覆盖，未命中再按 Tile_&lt;状态&gt; 资产名兜底</summary>
        /// <remarks>
        /// 通道②是两条：先剥 <c>Tile_</c> 前缀再解析，失败后拿资产全名再解析一次。
        /// 两条都要，且顺序不能换 —— Unity 里 <c>Object.name</c> 是资产内部的 m_Name（本工程的 <c>tiles/Tile_Mud.asset</c> 名字就是 <c>Mud</c>，文件名前缀不算），
        /// 所以"文件名带 Tile_ 前缀"这套约定对现有美术件并不成立；只留剥离一条，笔刷刷上去的泥浆会静默落回 Normal（实测过）。
        /// 反过来，资产全名等于 <c>Tile_Mud</c> 时第一条命中，第二条根本不会跑，故对按约定命名的贴图毫无影响。
        /// 解析不出来时不能静默当 Normal：那格会静默地什么都没发生，而策划只会看见"我刷了但没生效"。
        /// </remarks>
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
            // 接线自检：这三条错误的共同点是不报错也能跑
            if (groundTilemap == null)
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter.groundTilemap 未接线：格子系统拿不到几何，落点不会产生任何效果。", this);
            }

            if (effectTilemap == null)
            {
                Debug.LogWarning(
                    "[Grid] TilemapAdapter.effectTilemap 未接线：状态变化只记逻辑、不显示（泥浆不会出现）。", this);
            }
            else if (effectTilemap == groundTilemap)
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter 的 groundTilemap 与 effectTilemap 指向了同一层：状态结束时会把地板一起擦掉" +
                    "（表现为地上出现一块空洞）。请用两层不同的 Tilemap。", this);
            }

            // InitialSetup 读完会被 SetActive(false)：指错层等于开局把地板或效果层关掉，而且现象离病因很远
            if (initialSetupTilemap != null && (initialSetupTilemap == groundTilemap || initialSetupTilemap == effectTilemap))
            {
                Debug.LogError(
                    "[Grid] TilemapAdapter 的 initialSetupTilemap 与地板层／效果层指向了同一层：" +
                    "初始地块读完会把这个物体整个 SetActive(false)，地板或效果会一起消失。请单独建一个 InitialSetup 层。", this);
            }

            ValidateStateTiles();
        }

#if UNITY_EDITOR
        /// <remarks>接线属于一眼可查的错，不该等进 Play 才发现，这里也跑一遍自检。只读 stateTiles 并打日志，不写序列化字段；幂等，重复跑不改变状态。</remarks>
        private void OnValidate()
        {
            ValidateStateTiles();
        }
#endif

        /// <summary>校验 stateTiles 这张特例表：Normal 与重复状态都必须报出来</summary>
        /// <remarks>两条都不报错也能跑但一定画错：Normal 在 OnTileStateChanged 里当擦除，绑贴图自相矛盾，真正想画的状态反而查不到贴图；重复状态只取第一条命中，后面的静默失效。行内贴图留空是**合法**的（= 该状态不上贴图）。</remarks>
        private void ValidateStateTiles()
        {
            // 空表是合法配置（= 全部走 `tiles/Tile_<状态>` 约定），不是接线错误：不报日志，取不到贴图时由 TileFor 单独出声。
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
            if (effectTilemap == null) return;

            if (evt.State == TileStateType.Normal)
            {
                Restore(evt.Cell);
                return;
            }

            Show(evt.Cell, TileFor(evt.State));
        }

        /// <summary>状态 → 效果层贴图：Inspector 特例优先，未配置的按约定懒加载</summary>
        /// <remarks>
        /// 三级顺序不能换：① stateTiles 是策划／美术的特例覆盖（同一状态换皮、临时占位都靠它）；
        /// ② 运行期缓存让热路径只查一次字典，不反复寻址；③ 没配过才按 `tiles/Tile_{状态}` 约定取，
        /// 因此新增地块状态**不需要动 Inspector**，也就不存在"忘了配一行、泥浆静默不显示"。
        /// 取不到只报一条警告并返回 null：渲染链上抛异常会打断整批还画得出来的格子。
        /// </remarks>
        private TileBase TileFor(TileStateType state)
        {
            if (stateTiles != null)
            {
                for (int i = 0; i < stateTiles.Length; i++)
                {
                    if (stateTiles[i].State != state) continue;

                    // 绑了状态但贴图留空 = 明确要求"逻辑生效但不显示"，不再走约定去猜
                    if (stateTiles[i].Tile == null) return null;

                    return stateTiles[i].Tile;
                }
            }

            if (_runtimeTileCache.TryGetValue(state, out TileBase cached)) return cached;

            // 资源系统没起来时不去寻址（BuildSettings 里单开这个场景调试时会出现），
            // 否则每次状态变化都会从 AssetModule 抛一条 InvalidOperationException 打断渲染
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

        private void Show(Vector3Int cell, TileBase tile)
        {
            if (tile == null) return;

            if (!_previousTiles.ContainsKey(cell))
            {
                _previousTiles[cell] = effectTilemap.GetTile(cell);
            }

            effectTilemap.SetTile(cell, tile);
        }

        private void Restore(Vector3Int cell)
        {
            if (!_previousTiles.TryGetValue(cell, out TileBase original)) return;

            _previousTiles.Remove(cell);

            effectTilemap.SetTile(cell, original);
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
