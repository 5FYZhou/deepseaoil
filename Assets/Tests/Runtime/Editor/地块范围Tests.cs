// ---------------------------------------------------------------------------
// 地块范围 · 行为测试（"impact_radius 只是表里一个数字"这一类静默缺陷）
//
//   ① 0 = 落点单格（曾同时被构造器钳成 1.0、又被 >1.0 门槛放行，两个错误互相抵消）
//   ② 1.0 = 十字 5 格、1.5 = 九宫 9 格，地貌与伤害同构铺设
//   ③ 永久地貌不被次级覆盖；二级反应优先时不铺一级地貌
//
// 【跑法】Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 地块范围Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetModule.Dispose();

            if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
            ConfigModule.BindAssets();

            ReactionResolver.Initialize(ConfigModule.GetElementRules());

            EventBus<TileStateChanged>.Clear();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

        /// <summary>只记命中次数的伤害替身</summary>
        private sealed class HitTarget : IDamageable
        {
            public bool IsAlive => true;

            public Vector2 Position { get; set; }

            public int Hits;

            public void TakeDamage(in Damage damage) => Hits++;
        }

        private static GridLogic NewGrid(EnemyCellRegistry registry)
        {
            return new GridLogic(
                new GridGeometry(Vector2.zero, 1f),
                ConfigModule.GetAllTileStates(),
                CreateState,
                ConfigModule.GetDuoReactions(),
                registry);
        }

        private static ITileState CreateState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        private static void RegisterAround(GridLogic grid, Vector3Int center, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    grid.RegisterCell(new Vector3Int(center.x + dx, center.y + dy, center.z));
                }
            }
        }

        /// <summary>① 半径 0 只改落点：泥浆遇水冲成稀泥，邻格一格都不许动</summary>
        [Test]
        public void 半径零只改落点一格()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.Mud);

            ReactionOutcome outcome = ReactionResolver.Resolve(TileStateType.Mud, BallType.Water);

            Assert.AreEqual(0f, outcome.ImpactRadius, 1e-4f, "impact_radius=0 被钳回了非零：范围铺设会多铺一圈");

            grid.OnBallHit(center, BallType.Water);

            Assert.AreEqual(TileStateType.MudSkid, grid.StateOf(center), "泥浆遇水该冲成稀泥");

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(1, 0, 0)), "半径 0 不许波及邻格");
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(0, 1, 0)), "半径 0 不许波及邻格");
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(1, 1, 0)), "半径 0 不许波及邻格");
        }

        /// <summary>② 半径 1.0：十字 5 格全部铺泥浆、5 格全部结算伤害，对角线不动</summary>
        [Test]
        public void 半径一点零铺十字五格并五格结算伤害()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.BasicEarth);

            var cells = new[]
            {
                center,
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            };

            var targets = new HitTarget[cells.Length];

            for (int i = 0; i < cells.Length; i++)
            {
                targets[i] = new HitTarget { Position = grid.Geometry.CellCenter(cells[i]) };

                registry.Register(cells[i], targets[i]);
            }

            grid.OnBallHit(center, BallType.Water);

            for (int i = 0; i < cells.Length; i++)
            {
                Assert.AreEqual(TileStateType.Mud, grid.StateOf(cells[i]), $"十字第 {i} 格没铺上泥浆");
                Assert.AreEqual(1, targets[i].Hits, $"十字第 {i} 格没结算一次伤害");
            }

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(1, 1, 0)), "对角线不该被 1.0 半径纳入");
        }

        /// <summary>③ 半径 1.5：花浇水炸开九宫 9 格草丛，两格开外不动</summary>
        [Test]
        public void 半径一点五铺九宫九格()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.BasicFlower);

            grid.OnBallHit(center, BallType.Water);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(center), "花浇水该长成植物区");

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Assert.AreEqual(
                        TileStateType.BasicPlant,
                        grid.StateOf(new Vector3Int(dx, dy, 0)),
                        $"九宫格 ({dx},{dy}) 没长草");
                }
            }

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(2, 0, 0)), "半径 1.5 不该铺到两格开外");
        }

        /// <summary>④ 永久地貌（水/土/火/电源/冰面）不被次级铺设顶掉</summary>
        [Test]
        public void 永久地貌不被次级覆盖()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);
            var water = new Vector3Int(1, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.BasicEarth);
            grid.SwitchTileState(water, TileStateType.BasicWater);

            grid.OnBallHit(center, BallType.Water);

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(center), "落点该变泥浆");
            Assert.AreEqual(TileStateType.BasicWater, grid.StateOf(water), "永久水格被次级铺设顶掉了");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(new Vector3Int(0, 1, 0)), "空邻格该被铺成泥浆");
        }

        /// <summary>⑤ 二级反应优先：邻格是燎原火海，而不是一级规则要铺的蒸汽</summary>
        [Test]
        public void 二级优先时不铺一级地貌()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var fire = new Vector3Int(0, 0, 0);
            var plant = new Vector3Int(1, 0, 0);

            RegisterAround(grid, fire, 2);

            grid.SwitchTileState(fire, TileStateType.BasicFire);
            grid.SwitchTileState(plant, TileStateType.BasicPlant);

            grid.OnBallHit(fire, BallType.Water);

            Assert.AreEqual(TileStateType.FlameField, grid.StateOf(fire), "火池旁有植物时水球该激发燎原并跳过一级覆盖");

            Assert.AreEqual(
                TileStateType.FlameField,
                grid.StateOf(new Vector3Int(0, 1, 0)),
                "邻格该是二级铺的燎原火海；被蒸汽顶掉说明一级地貌在二级激发后仍然铺了");
        }
    }
}
