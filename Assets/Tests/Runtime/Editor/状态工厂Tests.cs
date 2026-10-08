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
//   3. 常规格也要有元素初值
//      元素层的 FlushStateElement 曾经把"状态是 Normal"当成"摘掉记录"，于是常规格表
//      （tile_state id=1 空地）自己的元素永远刷不上去 —— 水球砸空地于是算成"基础水地块"
//      而不是泥浆。判据：RegisterCell 之后该格元素 == 表里 Normal 那一行的元素四件。
//
// 【跑法】Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// 【为什么要在 SetUp 里自己 Init】测试是独立的 EditMode 程序集、也不进 PlayMode，Init 链的调用方
//   GameRoot 在 EditMode 里根本不跑；不自己初始化就会撞 ConfigModule 的 EnsureAssets 守卫
//   （"玩法数值在 BindAssets 之前被读取"）。口径与 Data层Tests 的 OneTimeSetUp 一致。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using NUnit.Framework;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Tests
{
    public class 状态工厂Tests
    {
        /// <summary>走完 Init 链的第二段：没有它，任何 <c>ConfigModule.GetXxx()</c> 都会抛。</summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // 与 Data层Tests 同一口径（可重复执行：AssetModule.Dispose 是幂等的，ConfigModule 靠 IsReady 守卫跳过）。
            AssetModule.Dispose();

            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
            ConfigModule.BindAssets();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

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
        public void 常规格也有表里的元素初值()
        {
            TileStateSpec normal = ConfigModule.GetTileState(TileStateType.Normal);

            // 元素层的初值必须真的刷上去（曾经 "状态是 Normal ⇒ 摘掉记录" 把这条吞了）
            var reactor = new TileElementReactor(ConfigModule.GetElementRules());
            var grid = new GridLogic(new GridGeometry(Vector2.zero, 1f), ConfigModule.GetAllTileStates(), CreateState, reactor);

            var cell = new Vector3Int(3, 3, 0);
            grid.RegisterCell(cell);

            ElementValue seeded = reactor.GetElement(cell);

            Assert.AreEqual(normal.Element.Tags, seeded.Tags, "常规格的标签位没有按表刷上去");
            Assert.AreEqual(normal.Element.Temperature, seeded.Temperature);
            Assert.AreEqual(normal.Element.Wet, seeded.Wet);
            Assert.AreEqual(normal.Element.Conductivity, seeded.Conductivity);
        }

        [Test]
        public void 水球砸常规格落到有贴图的状态()
        {
            // 判据不写死"Mud"：只要求结果状态**有实现**，且不是 Normal（Normal 会被适配器当成擦除）。
            // 这样表里调整地面脾性时这条用例不会假红，而"反应算出个空状态"仍会被抓住。
            var rules = ConfigModule.GetElementRules();
            var reactor = new TileElementReactor(rules);
            var grid = new GridLogic(new GridGeometry(Vector2.zero, 1f), ConfigModule.GetAllTileStates(), CreateState, reactor);

            var cell = new Vector3Int(0, 0, 0);
            grid.RegisterCell(cell);

            ProjectileSpec water = ConfigModule.GetBall(BallType.Water);

            Assert.IsNotNull(water, "projectile 表里没有水球");

            bool changed = grid.OnBallHit(cell, water.Element);

            TileStateType landed = grid.StateOf(cell);

            Assert.IsTrue(changed, "水球落地没有产生状态转换");
            Assert.AreNotEqual(TileStateType.Normal, landed, "落地后仍是常规格：反应算了但没落地");
            Assert.IsNotNull(CreateState(landed), $"{landed} 没有实现：格子会空着");
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
