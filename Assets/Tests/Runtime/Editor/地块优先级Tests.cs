// ---------------------------------------------------------------------------
// 地块覆盖秩 · 行为测试
//
//   秩大压秩小，平级后写赢；空地双向放行；直接写入（落点/播种/读档）不吃秩。
//   这些用例自带 TileStateSpec，不读真表：验的是裁决逻辑，不是某一行的数值。
//
// 【跑法】Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 地块优先级Tests
    {
        private static readonly Vector3Int Center = Vector3Int.zero;

        private static readonly Vector3Int Right = new Vector3Int(1, 0, 0);

        [Test]
        public void 秩低不覆盖秩高()
        {
            GridLogic grid = NewGrid(mudPriority: 10, hardPriority: 20);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(
                TileStateType.MudSkid,
                grid.StateOf(Right),
                "秩 10 的泥浆铺到了秩 20 的格子上：裁决没生效");
        }

        [Test]
        public void 秩高覆盖秩低()
        {
            GridLogic grid = NewGrid(mudPriority: 10, hardPriority: 5);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(
                TileStateType.Mud,
                grid.StateOf(Right),
                "秩 10 压不过秩 5：反了");
        }

        [Test]
        public void 平级后写赢()
        {
            GridLogic grid = NewGrid(mudPriority: 10, hardPriority: 10);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Right), "平级必须允许覆盖（后写赢）");
        }

        [Test]
        public void 直接写入无视秩()
        {
            GridLogic grid = NewGrid(mudPriority: 10, hardPriority: 99);

            Assert.IsTrue(grid.ForceTileState(Right, TileStateType.Mud), "直接写入被秩拦住了");

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Right));
        }

        [Test]
        public void 空地双向放行()
        {
            GridLogic grid = NewGrid(mudPriority: 10, hardPriority: 20);

            Assert.IsTrue(grid.SwitchTileState(Right, TileStateType.Normal), "回空地必须永远成立");

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(Right));

            Assert.IsTrue(grid.SwitchTileState(Right, TileStateType.Mud), "空地必须能被任何秩写上");

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Right));
        }

        private static GridLogic NewGrid(int mudPriority, int hardPriority)
        {
            var mud = new TileStateSpec(
                RowFactory.TileStateRow((int)TileStateType.Mud, duration: 10f, priority: mudPriority));

            var hard = new TileStateSpec(
                RowFactory.TileStateRow((int)TileStateType.MudSkid, duration: 10f, priority: hardPriority));

            var normal = new TileStateSpec(RowFactory.TileStateRow((int)TileStateType.Normal, priority: 1));

            var specs = new List<TileStateSpec> { mud, hard };

            var grid = new GridLogic(
                new GridGeometry(Vector2.zero, 1f),
                specs,
                id =>
                {
                    switch (id)
                    {
                        case TileStateType.Mud: return new TableTileState(mud);

                        case TileStateType.MudSkid: return new TableTileState(hard);

                        case TileStateType.Normal: return new TableTileState(normal);

                        default: return null;
                    }
                });

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    grid.RegisterCell(new Vector3Int(x, y, 0));
                }
            }

            grid.ForceTileState(Right, TileStateType.MudSkid);

            return grid;
        }
    }
}
