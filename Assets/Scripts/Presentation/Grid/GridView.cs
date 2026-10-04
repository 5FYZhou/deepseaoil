using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;
using cfg.demo;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>
    /// 格子系统与 Unity Tilemap 之间的桥：把地板的格子与几何灌进逻辑层，把状态变化画出来。
    /// </summary>
    /// <remarks>
    /// <b>它是唯一认识 <c>Tilemap</c> 的地方</b>，也是"逻辑层零引擎类型"的代价与收益：
    /// 折算（<c>cellSize</c> / 角点 / 格子集合）只在这一处发生，逻辑层拿到的永远是纯数据，
    /// 于是整套格子行为可以在 EditMode 里喂 dt 复现。
    /// <para><b>订阅事实事件而不是被逻辑层直调：</b>跨层只走 <c>EventBus</c>。
    /// 逻辑层发布"某格状态变了"，本类决定那对应哪张贴图 —— 于是"状态 → 美术"的映射只有一份，
    /// 而且逻辑层不需要知道 Tilemap 的存在。</para>
    /// <para><b>两张 Tilemap 的分工：</b>地板层（提供格集合与几何，也是"这里能不能站"的依据）
    /// 与效果层（叠在地板上的状态贴图）。效果贴图**不改地板**：改了地板就分不清
    /// "这一格本来是泥"还是"被打成泥"。</para>
    /// </remarks>
    public sealed class GridView : MonoBehaviour
    {
        [Tooltip("地板层：提供格子几何与合法格集合。必接。")]
        [SerializeField] private Tilemap groundTilemap = default;

        [Tooltip("效果层：状态贴图盖在这一层。必接。")]
        [SerializeField] private Tilemap effectTilemap = default;

        [Tooltip("泥浆状态的贴图。不接则状态变化只记逻辑、不显示。")]
        [SerializeField] private TileBase mudTile = default;

        /// <summary>效果层上被替换过的格 → 原贴图。</summary>
        /// <remarks>
        /// 记录原贴图而不是"清空该格"：效果层上可能本来就画着别的东西
        /// （血迹、装饰），状态结束时应该还原成它，而不是留一个洞。
        /// </remarks>
        private readonly Dictionary<Vector3Int, TileBase> _originalTiles = new();

        /// <summary>是否已接线（未接线时所有操作是 no-op，不报错刷屏）。</summary>
        public bool IsWired => groundTilemap != null;

        /// <summary>
        /// 读一次格子几何。
        /// </summary>
        /// <remarks>
        /// <b>角点语义必须与 <c>GridGeometry</c> 对齐</b>：<c>CellToWorld(zero)</c> 是格 (0,0) 的左下角，
        /// 而 <c>GetCellCenterWorld(zero)</c> 是它的中心。两者差半个格 —— 用错会让全场落点整体偏半格，
        /// 而那种偏差看起来"只是有点歪"，很难倒推。所以这里对齐之后会自检一次并报错。
        /// </remarks>
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

        /// <summary>
        /// 把地板层的全部格子登记进逻辑层。<b>只有登记过的格才能被砸出状态、被减速。</b>
        /// </summary>
        /// <returns>登记的格数；未接线时为 0。</returns>
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
                    "检查 GridView.groundTilemap 是否指向了画好地板的那一层。", this);
            }

            return count;
        }

        private void Awake()
        {
            // 接线自检：这三条错误的共同点是**不报错也能跑**，只是"什么都没发生"，
            // 而空场景接线时它们最容易发生（拖错层、忘拖贴图）。
            if (groundTilemap == null)
            {
                Debug.LogError(
                    "[Grid] GridView.groundTilemap 未接线：格子系统拿不到几何，落点不会产生任何效果。", this);
            }

            if (effectTilemap == null)
            {
                Debug.LogWarning(
                    "[Grid] GridView.effectTilemap 未接线：状态变化只记逻辑、不显示（泥浆不会出现）。", this);
            }
            else if (effectTilemap == groundTilemap)
            {
                Debug.LogError(
                    "[Grid] GridView 的 groundTilemap 与 effectTilemap 指向了同一层：状态结束时会把地板一起擦掉" +
                    "（表现为地上出现一块空洞）。请用两层不同的 Tilemap。", this);
            }

            if (mudTile == null)
            {
                Debug.LogWarning("[Grid] GridView.mudTile 未接线：泥浆状态不会被画出来（逻辑仍然生效）。", this);
            }
        }

        private void OnEnable()
        {
            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
        }

        private void OnDisable()
        {
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
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

        /// <summary>
        /// 状态 → 贴图。新状态在这里加一行。
        /// </summary>
        /// <remarks>
        /// 返回 <c>null</c> 表示"这个状态没有美术"：逻辑照常生效，只是看不见。
        /// 不报错 —— 状态是逻辑概念，先有逻辑后有美术是常态。
        /// </remarks>
        private TileBase TileFor(TileStateType state)
        {
            switch (state)
            {
                case TileStateType.Mud: return mudTile;
                default: return null;
            }
        }

        private void Show(Vector3Int cell, TileBase tile)
        {
            if (tile == null) return;

            if (!_originalTiles.ContainsKey(cell))
            {
                _originalTiles[cell] = effectTilemap.GetTile(cell);
            }

            effectTilemap.SetTile(cell, tile);
        }

        private void Restore(Vector3Int cell)
        {
            if (!_originalTiles.TryGetValue(cell, out TileBase original)) return;

            _originalTiles.Remove(cell);

            effectTilemap.SetTile(cell, original);
        }

        /// <summary>
        /// 自检：角点 + 半格必须等于"格心"。
        /// </summary>
        /// <remarks>
        /// 只在开发期发现，不抛异常也不停用：几何错了会让落点整体偏半个格，
        /// 而这件事在屏幕上看起来只是"有点歪"，没有报错就永远查不出来。
        /// </remarks>
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
