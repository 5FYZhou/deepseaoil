using System;
using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;
using cfg.demo;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>
    /// 格子系统与 Unity Tilemap 之间的适配器：把地板的格子与几何灌进逻辑层，把状态变化画出来。
    /// </summary>
    /// <remarks>
    /// <b>它是唯一认识 <c>Tilemap</c> 的地方</b>（逻辑层零引擎类型），也是"逻辑层零引擎类型"的代价与收益：
    /// 折算（<c>cellSize</c> / 角点 / 格子集合）只在这一处发生，逻辑层拿到的永远是纯数据，
    /// 于是整套格子行为可以在 EditMode 里喂 dt 复现。
    /// <para><b>它一半是数据源、一半是表现</b>：几何与合法格灌进逻辑层（数据源），
    /// 状态 → 贴图（表现）。<c>Adapter</c> 覆盖这两个方向，所以不叫 <c>GridPresenter</c>。</para>
    /// <para><b>订阅事实事件而不是被逻辑层直调：</b>跨层只走 <c>EventBus</c>。
    /// 逻辑层发布"某格状态变了"，本类决定那对应哪张贴图 —— 于是"状态 → 美术"的映射只有一份，
    /// 而且逻辑层不需要知道 Tilemap 的存在。</para>
    /// <para><b>订阅时机由组合根收口</b>：它不自己 <c>OnEnable</c> 订阅，而是由 <c>CombatRoot</c>
    /// 在装配期调 <see cref="Attach"/>、销毁期调 <see cref="Detach"/> —— 与瞄准高亮的订阅同一套纪律，
    /// 于是"什么时候开始听、什么时候停止听"只有一个答案。</para>
    /// <para><b>两张 Tilemap 的分工：</b>地板层（提供格集合与几何，也是"这里能不能站"的依据）
    /// 与效果层（叠在地板上的状态贴图）。效果贴图<b>不改地板</b>：改了地板就分不清
    /// "这一格本来是泥"还是"被打成泥"。</para>
    /// <para><b>状态 → 贴图是一张表，不是一个 switch</b>（<c>stateTiles</c>）：新增一种格子状态
    /// 只需要在 Inspector 里加一行，不用回来改这个文件 —— 收口前是 <c>TileFor</c> 里的一串 case
    /// 加一个 <c>mudTile</c> 字段，加状态必然要动代码。</para>
    /// <para><b>格视觉的第二层（占位物：树 / 石头这类有体积的贴图）暂未落地</b>：
    /// 它要等到表里出现第一个"阻挡 / 占位"的格子状态才有生产者；届时由本类按状态 ID
    /// 增删该格的立体图与静态碰撞体（"阻挡＝甲方案"：真源在格状态、执行在物理）。</para>
    /// <para><b><c>using cfg.demo;</c> 在这里吃的是枚举</b>（<see cref="TileStateType"/> 作字典键
    /// 与 <c>evt.State</c> 的比较），不是生成行 —— 判据见 <c>ConfigModule</c> 底部那段注释。</para>
    /// </remarks>
    public sealed class TilemapAdapter : MonoBehaviour
    {
        /// <summary>
        /// 状态 → 贴图的一行。
        /// </summary>
        /// <remarks>
        /// <b>公开</b>（而不是 private 嵌套）：字段由 Unity 的序列化器填，编译器看不见那些赋值 ——
        /// private 字段会被 CS0649 当成"从未赋值"而刷警告。公开字段没有这个问题
        /// （与 <c>WaveLogic.SpawnRequest</c> 同一个先例）。
        /// </remarks>
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

        /// <summary>
        /// 效果层上被覆盖过的格 → <b>被覆盖之前的那张图</b>。
        /// </summary>
        /// <remarks>
        /// <b>为什么名字是 <c>_previousTiles</c> 而不是 <c>_originalTiles</c>：</b>它存的是"覆盖前那一张"，
        /// 而不是"永远最初的那一张" —— 后者在"同格直接转换"（泥 → 冰）时会误导人。
        /// <para><b>为什么原值取自 <c>effectTilemap</c> 而<b>不是</b> <c>groundTilemap</c>：</b>
        /// 两层是两个独立的格子空间。效果层自己原本画着什么（可能是 <c>null</c>，也可能是血迹 / 装饰）
        /// 只有它自己知道 —— 拿地板图去还原会把效果层的装饰抹掉，并永久留下一张地板副本。</para>
        /// <para><b>为什么"只记第一次"</b>（见 <c>Show</c> 里的 <c>if (!ContainsKey)</c>）：
        /// 否则第二次覆盖会把"泥浆"当成原值，这一格永远回不到原样，而且不报错。</para>
        /// <para><b>两条已知局限</b>（依赖不变量"效果层只有 <c>Show</c> / <c>Restore</c> 两个写者"）：
        /// 别处改动会被还原盖掉；<c>effectTilemap</c> 对象被换掉时旧记录会贴到新层上。</para>
        /// </remarks>
        private readonly Dictionary<Vector3Int, TileBase> _previousTiles = new();

        /// <summary>是否已接线（未接线时所有操作是 no-op，不报错刷屏）。</summary>
        public bool IsWired => groundTilemap != null;

        /// <summary>
        /// 开始听"格子状态变了"。<b>由组合根在装配期调一次</b>（在灌入初始状态之前）。
        /// </summary>
        /// <remarks>
        /// <b>为什么要在灌入初始状态之前调：</b>关卡初始状态（<c>LoadInitialStates</c>）走的是同一条
        /// 状态转换路径，订阅晚了那一批泥浆就不会被画出来 —— 而它们是最不该漏的一批。
        /// <para>为什么不用 <c>OnEnable</c>：自动订阅让"何时开始听"由 Unity 的生命周期决定，
        /// 而不是由组合根的装配顺序决定；表现件不该有第二个驱动入口。</para>
        /// </remarks>
        public void Attach()
        {
            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
        }

        /// <summary>停止听（由组合根在销毁期调）。</summary>
        public void Detach()
        {
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
        }

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
                    "检查 TilemapAdapter 的 groundTilemap 是否指向了画好地板的那一层。", this);
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

            if (stateTiles == null || stateTiles.Length == 0)
            {
                Debug.LogWarning(
                    "[Grid] TilemapAdapter.stateTiles 是空的：任何格子状态都不会被画出来（逻辑仍然生效）。" +
                    "把「状态 → 贴图」填进去，例如 Mud → Tile_Mud。", this);
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

        /// <summary>
        /// 状态 → 贴图。<b>数据来自 Inspector 的 <c>stateTiles</c>，新状态不改这里。</b>
        /// </summary>
        /// <returns>返回 <c>null</c> 表示"这个状态没有美术"：逻辑照常生效，只是看不见。
        /// 不报错 —— 状态是逻辑概念，先有逻辑后有美术是常态。</returns>
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
