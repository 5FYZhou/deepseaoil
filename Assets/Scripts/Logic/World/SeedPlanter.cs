using DeepseaOil.Data;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Logic.World
{
    // 播种否决理由，Reject 按此顺序短路：先查手上有没有种子
    public enum PlantRejection
    {
        None = 0,

        OutOfFloor = 1,

        NotEmpty = 2,

        NoSeed = 3,

        Blocked = 4,
    }

    // 播种裁决：非阻挡格都能种（元素格覆写或直投二级质变），且手上必须有种子
    public static class SeedPlanter
    {
        public static PlantRejection Reject(
            GridLogic grid,
            Vector3Int cell,
            SeedType holding)
        {
            if (holding == SeedType.None) return PlantRejection.NoSeed;

            if (grid == null || !grid.HasCell(cell)) return PlantRejection.OutOfFloor;

            return grid.IsObstacle(cell) ? PlantRejection.Blocked : PlantRejection.None;
        }

        public static bool CanPlant(GridLogic grid, Vector3Int cell, SeedType holding)
        {
            return Reject(grid, cell, holding) == PlantRejection.None;
        }

        /// <summary>播种执行：先判直投二级质变，否则覆写地貌；false = 没动过任何东西</summary>
        /// <remarks>种子是否扣除由调用方负责，本方法不做资源判定；表里没有 SpawnTile 这一行也返回 false</remarks>
        public static bool ExecutePlant(
            GridLogic grid,
            Vector3Int cell,
            SeedType seedType,
            SeedSpec seedSpec,
            DuoReactionCatalog duoCatalog)
        {
            if (grid == null || seedSpec == null) return false;

            if (!CanPlant(grid, cell, seedType)) return false;

            if (duoCatalog != null && duoCatalog.TryGet(seedSpec.SpawnTile, grid.StateOf(cell), out DuoReactionSpec duo))
            {
                return TileChainReactor.CommitDuoDirect(grid, cell, duo);
            }

            if (!grid.SwitchTileState(cell, seedSpec.SpawnTile)) return false;

            TileChainReactor.TriggerDuo(grid, cell, duoCatalog);

            return true;
        }
    }
}
