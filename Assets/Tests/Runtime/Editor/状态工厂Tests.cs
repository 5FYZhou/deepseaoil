// ---------------------------------------------------------------------------
// 状态工厂一致性 · 行为测试
//
// 守的是这一类"不报错、只是格子空着"的缺口：
//
//   1. 「规则表命中的状态」必须是「有实现的状态」的子集
//      element_rule / tile_state 里写出来的每个 result_id / id，都要能造出状态实例。
//      少了实现的表现不是异常，而是"反应发生了、格子却什么都没变" ——
//      上游那版就是这样：SwitchTo 对造不出来的状态也返回 true，于是上层照常发事件、
//      照常把空状态机存回去，而 StateOf 下一次读回来仍是 Normal。
//
//   2. 切到一个"没有实现"的状态必须失败，且不留下空状态机
//      （判据：SwitchState 返回 false；随后再切一个真有实现的状态仍能成功）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using NUnit.Framework;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Tests
{
    public class 状态工厂Tests
    {
        /// <summary>造一份"生产同款"的格子：geometry + 表里的状态清单 + 表驱动的状态工厂。</summary>
        private static GridLogic NewGrid()
        {
            var geometry = new GridGeometry(Vector2.zero, 1f);

            IReadOnlyList<TileStateSpec> specs = ConfigModule.GetAllTileStates();

            var grid = new GridLogic(geometry, specs, CreateState);

            grid.RegisterCell(Vector3Int.zero);

            return grid;
        }

        /// <summary>与 <c>CombatRoot.CreateTileState</c> 同一条判据：配置里有这一行就能造，没有就返回 <c>null</c>。</summary>
        private static ITileState CreateState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        [Test]
        public void 配置里的每个状态都能造出实例()
        {
            IReadOnlyList<TileStateSpec> specs = ConfigModule.GetAllTileStates();

            Assert.Greater(specs.Count, 0, "tile_state 一行都没有：格子系统没有状态可切");

            var missing = new List<string>();

            for (int i = 0; i < specs.Count; i++)
            {
                TileStateType id = specs[i].Id;

                if (CreateState(id) == null) missing.Add($"{id}（id={(int)id}）");
            }

            Assert.IsEmpty(missing,
                "这些状态在 tile_state 里有行、却造不出实例（规则命中它们时格子会空着）：\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void 规则表命中的状态都有实现()
        {
            IReadOnlyList<ElementRuleSpec> rules = ConfigModule.GetElementRules();

            Assert.Greater(rules.Count, 0, "element_rule 一行都没有：投掷落地不会产生任何反应");

            var missing = new List<string>();

            for (int i = 0; i < rules.Count; i++)
            {
                TileStateType result = rules[i].ResultTileType;

                // None = "不改动"（兜底行的写法），Normal = 落回常规：两者都不需要状态实现。
                if (result == TileStateType.None || result == TileStateType.Normal) continue;

                if (CreateState(result) != null) continue;

                missing.Add($"优先级 {rules[i].Priority} → {result}（id={(int)result}）");
            }

            Assert.IsEmpty(missing,
                "这些规则的 result_id 没有对应的状态实现：反应判定会通过、格子却什么都不变（最坏的一类静默）。\n  "
                + string.Join("\n  ", missing));
        }

        [Test]
        public void 没有实现的状态切不进去且不留空状态机()
        {
            GridLogic grid = NewGrid();

            Vector3Int cell = Vector3Int.zero;

            // None(0) 在 tile_state 表里没有行 —— 这是"真的没有实现"，不是"打错了 id"。
            Assert.IsFalse(grid.SwitchState(cell, TileStateType.None, applyEnterImpact: true),
                "切到一个没有实现的状态必须返回 false");

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell),
                "切换失败后格子状态必须保持原样（不能变成「有状态机但读作常规」）");

            // 空状态机没有留下来：现在切一个真有实现的状态，必须还能成功。
            Assert.IsTrue(grid.SwitchState(cell, TileStateType.Mud, applyEnterImpact: true),
                "失败的那次不该污染状态机：随后切一个真有实现的状态仍应成功");

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell));

            // 再切回常规：状态机应当被摘掉，重新可切。
            Assert.IsTrue(grid.SwitchState(cell, TileStateType.Normal, applyEnterImpact: false));
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell));
            Assert.IsTrue(grid.SwitchState(cell, TileStateType.Mud, applyEnterImpact: true),
                "落回常规后状态机应当被摘掉，再切回来必须成功");
        }
    }
}
