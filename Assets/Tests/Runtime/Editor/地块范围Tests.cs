// ---------------------------------------------------------------------------
// 地块范围 · 行为测试（"impact_radius 只是表里一个数字"这一类静默缺陷）
//
//   ① 0 = 落点单格（曾同时被构造器钳成 1.0、又被 >1.0 门槛放行，两个错误互相抵消）
//   ② 1.0 = 十字 5 格、1.5 = 九宫 9 格，地貌与伤害同构铺设
//   ③ 次级地貌按优先级覆盖基础地貌；二级反应优先时不铺一级地貌
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

        /// <summary>① 泥浆遇水冲成稀泥，并按当前表的 1.0 半径铺十字</summary>
        [Test]
        public void 泥浆遇水冲稀并铺十字()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.Mud);

            ReactionOutcome outcome = ReactionResolver.Resolve(TileStateType.Mud, BallType.Water);

            Assert.AreEqual(1f, outcome.ImpactRadius, 1e-4f, "泥浆遇水半径应取当前表值 1.0");

            grid.OnBallHit(center, BallType.Water);

            Assert.AreEqual(TileStateType.MudSkid, grid.StateOf(center), "泥浆遇水该冲成稀泥");

            Assert.AreEqual(TileStateType.MudSkid, grid.StateOf(new Vector3Int(1, 0, 0)), "十字东格没铺稀泥");
            Assert.AreEqual(TileStateType.MudSkid, grid.StateOf(new Vector3Int(0, 1, 0)), "十字北格没铺稀泥");
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(1, 1, 0)), "对角线不该进入十字");
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

        [TestCase(TileStateType.BasicEarth, BallType.Earth, "基础土 + 土块")]
        [TestCase(TileStateType.BasicWater, BallType.Water, "基础水 + 水球")]
        public void 同元素命中保留伤害并铺十字(TileStateType tileState, BallType ball, string label)
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            var crossNeighbors = new[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, -1, 0),
            };

            var diagonals = new[]
            {
                new Vector3Int(1, 1, 0),
                new Vector3Int(-1, 1, 0),
                new Vector3Int(1, -1, 0),
                new Vector3Int(-1, -1, 0),
            };

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, tileState);

            var centerTarget = new HitTarget { Position = grid.Geometry.CellCenter(center) };
            var crossTargets = new HitTarget[crossNeighbors.Length];
            var diagonalTargets = new HitTarget[diagonals.Length];

            registry.Register(center, centerTarget);

            for (int i = 0; i < crossNeighbors.Length; i++)
            {
                crossTargets[i] = new HitTarget { Position = grid.Geometry.CellCenter(crossNeighbors[i]) };
                registry.Register(crossNeighbors[i], crossTargets[i]);
            }

            for (int i = 0; i < diagonals.Length; i++)
            {
                diagonalTargets[i] = new HitTarget { Position = grid.Geometry.CellCenter(diagonals[i]) };
                registry.Register(diagonals[i], diagonalTargets[i]);
            }

            ReactionOutcome outcome = ReactionResolver.Resolve(tileState, ball);

            Assert.AreEqual(tileState, outcome.NextTile, label);
            Assert.AreEqual(1, outcome.InstantDamage, $"{label} 应保留落点伤害");
            Assert.AreEqual(1f, outcome.ImpactRadius, 1e-4f, $"{label} 应使用半径 1");

            grid.OnBallHit(center, ball);

            Assert.AreEqual(tileState, grid.StateOf(center), $"{label} 落点地貌应保持不变");
            Assert.AreEqual(1, centerTarget.Hits, $"{label} 落点应结算一次伤害");

            for (int i = 0; i < crossNeighbors.Length; i++)
            {
                Assert.AreEqual(tileState, grid.StateOf(crossNeighbors[i]), $"{label} 十字四邻应铺对应元素");
                Assert.AreEqual(1, crossTargets[i].Hits, $"{label} 十字四邻应各结算一次伤害");
            }

            for (int i = 0; i < diagonals.Length; i++)
            {
                Assert.AreEqual(TileStateType.Normal, grid.StateOf(diagonals[i]), $"{label} 对角地貌不应变化");
                Assert.AreEqual(0, diagonalTargets[i].Hits, $"{label} 对角不应受到范围伤害");
            }
        }

        /// <summary>③ 半径 2.0：花浇水扩到二环，圆形边界外不动</summary>
        [Test]
        public void 半径二扩展到二环()
        {
            TileChainReactor.Clear();

            var registry = new EnemyCellRegistry();

            GridLogic grid = NewGrid(registry);

            var center = new Vector3Int(0, 0, 0);

            RegisterAround(grid, center, 2);

            grid.SwitchTileState(center, TileStateType.BasicFlower);

            grid.OnBallHit(center, BallType.Water);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(center), "花浇水该长成植物区");
            Assert.AreEqual(
                2f,
                ReactionResolver.Resolve(TileStateType.BasicFlower, BallType.Water).ImpactRadius,
                1e-4f,
                "花浇水半径应取当前表值 2.0");

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

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(new Vector3Int(2, 0, 0)), "半径 2.0 应覆盖二环十字");
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(2, 2, 0)), "圆形半径 2.0 不应覆盖二环对角");
        }

        /// <summary>④ 次级扩散只按优先级覆盖：泥浆 12 覆盖基础水 10</summary>
        [Test]
        public void 次级扩散按优先级覆盖基础地貌()
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
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(water), "泥浆优先级高于基础水，次级扩散应覆盖");
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
