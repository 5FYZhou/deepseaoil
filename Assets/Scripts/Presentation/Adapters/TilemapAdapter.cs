using System;
using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;
using cfg.demo;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>格子系统与 Unity Tilemap 的适配器，把地板格子与几何灌进逻辑层，把状态变化画出来</summary>
    /// <remarks>本文件是唯一认识 Tilemap 的地方，逻辑层零引擎类型，几何折算只在这里发生。订阅时机由组合根收口（Attach/Detach），不自己 OnEnable 订阅。地板层提供格集合与几何，效果层盖状态贴图。效果贴图不改地板，否则分不清"本来是泥"还是"被打成泥"。状态 → 贴图查 stateTiles，新增状态在 Inspector 加一行即可。</remarks>
    public sealed class TilemapAdapter : MonoBehaviour
    {
        [Serializable]
        public struct StateTileBinding
        {
            [Tooltip("格子状态（tile_state 表的 id）")]
            public TileStateType State;

            [Tooltip("该状态在效果层上用的贴图；留空 = 逻辑生效但不显示")]
            public TileBase Tile;
        }

        [Tooltip("地板层：提供格子几何与合法格集合。必接。")]
        [SerializeField] private Tilemap groundTilemap = default;

        [Tooltip("效果层：状态贴图盖在这一层。必接。")]
        [SerializeField] private Tilemap effectTilemap = default;

        [Tooltip("状态 → 贴图。新增状态在这里加一行，不用改代码。")]
        [SerializeField] private StateTileBinding[] stateTiles = new StateTileBinding[0];

        /// <remarks>只记第一次覆盖，否则第二次覆盖会把"泥浆"当原值，这一格永远回不到原样且不报错。前提：效果层只有 Show/Restore 两个写者，别处改动会被还原盖掉；effectTilemap 被换掉时旧记录会贴到新层上。原值取自 effectTilemap，拿地板图还原会抹掉效果层装饰。</remarks>
        private readonly Dictionary<Vector3Int, TileBase> _previousTiles = new();

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

            ValidateStateTiles();
        }

#if UNITY_EDITOR
        /// <remarks>接线属于一眼可查的错，不该等进 Play 才发现，这里也跑一遍自检。只读 stateTiles 并打日志，不写序列化字段；幂等，重复跑不改变状态。</remarks>
        private void OnValidate()
        {
            ValidateStateTiles();
        }
#endif

        /// <summary>校验 stateTiles 这张"状态 → 贴图"表，Normal 与重复状态都必须报出来</summary>
        /// <remarks>两条都不报错也能跑但一定画错：Normal 在 OnTileStateChanged 里当擦除，绑贴图自相矛盾，真正想画的状态反而查不到贴图；重复状态只取第一条命中，后面的静默失效。</remarks>
        private void ValidateStateTiles()
        {
            if (stateTiles == null || stateTiles.Length == 0)
            {
                Debug.LogWarning(
                    "[Grid] TilemapAdapter.stateTiles 是空的：任何格子状态都不会被画出来（逻辑仍然生效）。" +
                    "把「状态 → 贴图」填进去，例如 Mud → Tile_Mud。", this);

                return;
            }

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

        private TileBase TileFor(TileStateType state)
        {
            if (stateTiles == null) return null;

            for (int i = 0; i < stateTiles.Length; i++)
            {
                if (stateTiles[i].State == state) return stateTiles[i].Tile;
            }

            return null;
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
