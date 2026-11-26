// ---------------------------------------------------------------------------
// 地块覆盖秩 · 行为测试
//
//   秩大压秩小，平级后写赢；空地双向放行；直接写入（落点/播种/读档）不吃秩。
//   这些用例自带 TileStateSpec，不读真表：验的是裁决逻辑，不是某一行的数值。
//
// 【跑法】Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Events;
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
        public void 高秩扩散覆盖基础地貌低秩格()
        {
            GridLogic grid = NewGrid(mudPriority: 20, currentPriority: 10, TileStateType.BasicWater);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(
                TileStateType.Mud,
                grid.StateOf(Right),
                "高秩扩散被基础地貌黑名单拦住，或被基础地貌低秩挡住");
        }

        [Test]
        public void 低秩扩散不覆盖基础地貌高秩格()
        {
            GridLogic grid = NewGrid(mudPriority: 10, currentPriority: 20, TileStateType.BasicFire);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(
                TileStateType.BasicFire,
                grid.StateOf(Right),
                "低秩扩散覆盖了基础地貌高秩格");
        }

        [Test]
        public void 基础地貌平级后写赢()
        {
            GridLogic grid = NewGrid(mudPriority: 10, currentPriority: 10, TileStateType.BasicWater);

            grid.SpreadTileInRadius(Center, 1f, TileStateType.Mud);

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Right), "平级必须允许覆盖基础地貌（后写赢）");
        }

        [Test]
        public void 反应落点面对高秩旧地貌仍无条件生效()
        {
            GridLogic grid = NewGrid(mudPriority: 10, currentPriority: 99, TileStateType.BasicWater);

            ReactionResolver.Initialize(new List<ElementRuleSpec>
            {
                RowFactory.ElementRuleSpecOf(
                    id: 1,
                    sourceTile: (int)TileStateType.BasicWater,
                    ballType: (int)BallType.Earth,
                    resultTile: (int)TileStateType.Mud,
                    radius: 0f)
            });

            try
            {
                grid.ForceTileState(Center, TileStateType.BasicWater);

                Assert.IsTrue(grid.OnBallHit(Center, BallType.Earth), "反应落点必须命中");
                Assert.AreEqual(
                    TileStateType.Mud,
                    grid.StateOf(Center),
                    "落点被覆盖秩拦住：反应即使面对高秩旧地貌也必须生效");
            }
            finally
            {
                ReactionResolver.Clear();
            }
        }

        [Test]
        public void 空地双向放行()
        {
            GridLogic grid = NewGrid(mudPriority: 10, currentPriority: 20, TileStateType.BasicWater);

            Assert.IsTrue(grid.SwitchTileState(Right, TileStateType.Normal), "回空地必须永远成立");

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(Right));

            Assert.IsTrue(grid.SwitchTileState(Right, TileStateType.Mud), "空地必须能被任何秩写上");

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Right));
        }

        [Test]
        public void 同状态扩散不重复切换()
        {
            GridLogic grid = NewGrid(mudPriority: 10, currentPriority: 10, TileStateType.BasicWater);

            grid.SpreadTileInRadius(Center, 0f, TileStateType.Mud);

            int changed = 0;

            Action<TileStateChanged> onChanged = evt =>
            {
                if (evt.Cell == Center) changed++;
            };

            EventBus<TileStateChanged>.Subscribe(onChanged);

            try
            {
                Assert.IsFalse(
                    grid.SpreadTileInRadius(Center, 0f, TileStateType.Mud),
                    "同状态扩散不应报告切换");
                Assert.AreEqual(0, changed, "同状态扩散不应发布切换事件");
            }
            finally
            {
                EventBus<TileStateChanged>.Unsubscribe(onChanged);
            }
        }

        private static GridLogic NewGrid(int mudPriority, int currentPriority, TileStateType currentState)
        {
            var mud = new TileStateSpec(
                RowFactory.TileStateRow((int)TileStateType.Mud, duration: 10f, priority: mudPriority));

            var current = new TileStateSpec(
                RowFactory.TileStateRow((int)currentState, duration: 10f, priority: currentPriority));

            var normal = new TileStateSpec(RowFactory.TileStateRow((int)TileStateType.Normal, priority: 1));

            var specs = new List<TileStateSpec> { mud, current };

            var grid = new GridLogic(
                new GridGeometry(Vector2.zero, 1f),
                specs,
                id =>
                {
                    switch (id)
                    {
                        case TileStateType.Mud: return new TableTileState(mud);

                        case TileStateType.BasicWater:
                        case TileStateType.BasicFire:
                            return new TableTileState(current);

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

            grid.ForceTileState(Right, currentState);

            return grid;
        }
    }
}
