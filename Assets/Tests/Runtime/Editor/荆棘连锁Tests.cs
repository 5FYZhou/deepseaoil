// ---------------------------------------------------------------------------
// 带电荆棘连锁 · 行为测试
//
// 守的是"荆棘连锁"被误接成"延烧"这条静默缺陷：
//   ① 生成当帧不点火：植物区旁投水得到荆棘十字，整张网不许出现 FlameField
//   ② 0.1s 步长沿十字传染，只吃基础植物区（水/泥浆/基础花不碰）
//   ③ 到期退回基础植物区（不是空地），供玩家复用
//   ④ 火海仍能烧穿荆棘（可燃决定不变）
//
// 【跑法】Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 荆棘连锁Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetModule.Dispose();

            if (!ConfigModule.IsReady) ConfigModule.InitFromStreamingAssets();

            AssetModule.Init();
            ConfigModule.BindAssets();

            EventBus<TileStateChanged>.Clear();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetModule.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            TileChainReactor.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            TileChainReactor.Clear();
        }

        /// <summary>① 0.1s 步长：一环 0.12s、二环 0.23s，且全程不点火。</summary>
        [Test]
        public void 带电荆棘沿十字按0点1秒步长感染植物区()
        {
            GridLogic grid = NewGrid(new EnemyCellRegistry());

            var origin = new Vector3Int(0, 0, 0);
            var first = new Vector3Int(1, 0, 0);
            var second = new Vector3Int(2, 0, 0);

            BuildPlantLine(grid, origin, 3);

            grid.ForceTileState(origin, TileStateType.ChargedThorn);

            TileChainReactor.Trigger(grid, origin, TileStateType.ChargedThorn);

            float now = 0f;

            Advance(grid, ref now, TileChainReactor.ThornSpreadStep * 0.5f);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(first), "不到一个 0.1s 步长，一环不许变");

            Advance(grid, ref now, 0.08f);

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(first), "0.1s 后一环植物必须被传染");
            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(second), "二环要等下一步");

            Advance(grid, ref now, 0.12f);

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(second), "二环必须由一环继续传染（不是一帧全铺）");

            AssertNoFire(grid, origin, first, second);
        }

        /// <summary>② 传染只看基础植物区：水、泥浆、基础花都是绝缘体。</summary>
        [Test]
        public void 带电荆棘只感染基础植物区()
        {
            GridLogic grid = NewGrid(new EnemyCellRegistry());

            var origin = new Vector3Int(0, 0, 0);
            var water = new Vector3Int(1, 0, 0);
            var flower = new Vector3Int(-1, 0, 0);
            var mud = new Vector3Int(0, 1, 0);
            var plant = new Vector3Int(0, -1, 0);

            Register(grid, origin, TileStateType.ChargedThorn);
            Register(grid, water, TileStateType.BasicWater);
            Register(grid, flower, TileStateType.BasicFlower);
            Register(grid, mud, TileStateType.Mud);
            Register(grid, plant, TileStateType.BasicPlant);

            TileChainReactor.Trigger(grid, origin, TileStateType.ChargedThorn);

            float now = 0f;

            Advance(grid, ref now, TileChainReactor.ThornSpreadStep * 3f);

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(plant), "基础植物区必须被传染");
            Assert.AreEqual(TileStateType.BasicWater, grid.StateOf(water), "水不是植物：不许被传染");
            Assert.AreEqual(TileStateType.BasicFlower, grid.StateOf(flower), "基础花是玩家的作物：不许被传染");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(mud), "泥浆不是植物：不许被传染");

            AssertNoFire(grid, origin, water, flower, mud, plant);
        }

        /// <summary>③ 到期去向：荆棘退回植物区，其它临时地貌仍回空地。</summary>
        [Test]
        public void 带电荆棘到期退回植物区()
        {
            TileStateSpec thorn = ConfigModule.TryGetTileState(TileStateType.ChargedThorn);

            Assert.IsNotNull(thorn, "tile_state 必须有带电荆棘这一行");

            GridLogic grid = NewGrid(new EnemyCellRegistry());

            var thornCell = new Vector3Int(0, 0, 0);
            var mudCell = new Vector3Int(5, 0, 0);

            Register(grid, thornCell, TileStateType.ChargedThorn);
            Register(grid, mudCell, TileStateType.Mud);

            float now = 0f;

            Advance(grid, ref now, thorn.Duration * 0.8f);

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(thornCell), "没到期前必须还是荆棘");

            Advance(grid, ref now, thorn.Duration * 0.3f);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(thornCell), "荆棘到期必须退回基础植物区，不是空地");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(mudCell), "同一时刻泥浆还没到期");

            Advance(grid, ref now, 11f);

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(mudCell), "其余地貌到期仍回空地：这里只该多一条荆棘的分支");
        }

        /// <summary>④ 回归：植物区旁投水 → 荆棘十字 + 0.1s 续传，全程不点火；到期整片退回植物区。</summary>
        [Test]
        public void 植物区旁投水长出荆棘十字且不点火()
        {
            GridLogic grid = NewGrid(new EnemyCellRegistry());

            var origin = new Vector3Int(0, 0, 0);
            var power = new Vector3Int(0, 1, 0);
            var east = new Vector3Int(1, 0, 0);
            var west = new Vector3Int(-1, 0, 0);
            var south = new Vector3Int(0, -1, 0);
            var far = new Vector3Int(2, 0, 0);

            Register(grid, origin, TileStateType.BasicPlant);
            Register(grid, power, TileStateType.BasicElectricity);
            Register(grid, east, TileStateType.BasicPlant);
            Register(grid, west, TileStateType.BasicPlant);
            Register(grid, south, TileStateType.Normal);
            Register(grid, far, TileStateType.BasicPlant);

            Assert.IsTrue(grid.OnBallHit(origin, BallType.Water), "植物区旁有电源时投水必须激发二级反应");

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(origin), "电源 × 植物区 → 带电荆棘");

            // 表的 effect_radius=1：即时十字（含空地）当帧就变荆棘
            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(power), "即时十字：电源格也按表被荆棘覆盖");
            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(east), "即时十字：东邻");
            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(west), "即时十字：西邻");
            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(south), "即时十字：南邻空地也按表变荆棘");

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(far), "二环还没到 0.1s 步长");

            AssertNoFire(grid, origin, power, east, west, south, far);

            float now = 0f;

            Advance(grid, ref now, TileChainReactor.ThornSpreadStep * 0.5f);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(far), "半个步长内二环不许变");

            Advance(grid, ref now, 0.08f);

            Assert.AreEqual(TileStateType.ChargedThorn, grid.StateOf(far), "0.1s 后十字继续向外传染二环植物");

            AssertNoFire(grid, origin, power, east, west, south, far);

            Advance(grid, ref now, 4f);

            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(origin), "到期后整片退回植物区：供玩家循环使用");
            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(east), "即时十字同样退回植物区");
            Assert.AreEqual(TileStateType.BasicPlant, grid.StateOf(far), "传染出来的荆棘同样退回植物区");
        }

        /// <summary>⑤ 可燃性不变：真实火海前沿仍能把荆棘烧穿。</summary>
        [Test]
        public void 火海仍能烧穿带电荆棘()
        {
            GridLogic grid = NewGrid(new EnemyCellRegistry());

            var thorn = new Vector3Int(0, 0, 0);
            var flame = new Vector3Int(1, 0, 0);

            Register(grid, thorn, TileStateType.ChargedThorn);
            Register(grid, flame, TileStateType.FlameField);

            TileChainReactor.Trigger(grid, flame, TileStateType.FlameField);

            float now = 0f;

            Advance(grid, ref now, TileChainReactor.FlameSpreadStep + 0.02f);

            Assert.AreEqual(TileStateType.FlameField, grid.StateOf(thorn), "带电荆棘是植物：火海前沿必须能烧穿");
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

        private static void Register(GridLogic grid, Vector3Int cell, TileStateType state)
        {
            grid.RegisterCell(cell);

            if (state != TileStateType.Normal) grid.SwitchTileState(cell, state);
        }

        /// <summary>从 origin 起沿 +x 铺 count 格基础植物区。</summary>
        private static void BuildPlantLine(GridLogic grid, Vector3Int origin, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Register(grid, new Vector3Int(origin.x + i, origin.y, origin.z), TileStateType.BasicPlant);
            }
        }

        private static void Advance(GridLogic grid, ref float now, float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f)
            {
                now += 1f / 60f;

                grid.Tick(now, 1f / 60f);
            }
        }

        private static void AssertNoFire(GridLogic grid, params Vector3Int[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.AreNotEqual(
                    TileStateType.FlameField,
                    grid.StateOf(cells[i]),
                    $"格 {cells[i]} 起火：生成荆棘不该走燎原分支");
            }
        }
    }
}
