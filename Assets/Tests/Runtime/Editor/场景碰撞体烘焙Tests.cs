// ---------------------------------------------------------------------------
// 场景碰撞体烘焙 · 运行期测试
//
// 【为什么在这里】本文件守三条「改错了不报错」的约定：
//   ① 烘焙判据按**身体半径**算距离：退回点采样时薄栅栏整道漏判，敌人正面顶住。
//   ② 排除口径：触发器、IManagedActor、IActorMotor、阻挡地貌池、动态体都不算障碍；
//      漏排除会把地图边界（BoundsArea 是 trigger）与角色自身烘成墙。
//   ③ 重复烘焙幂等：先 Clear 再标记，否则改一次场景重进游戏会越积越多；
//   速度被物理压到零而大脑仍在追时，EnemyLogic 还必须自己挤出脱困缝。
//
// 覆盖边界：实机手感仍须 PlayMode 目视（步骤见 Docs/待办.md）。
//
// 【测试格域为什么挪到 (0.5,0.5)】EditMode 跑在当前打开的场景里、Physics2D 查询又是全局的，
//   而 Range 的栅栏/树/路牌就落在原点附近；断言因此错开半格以上，并先量干净基线、只断言增量。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation.Grid;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 场景碰撞体烘焙Tests
    {
        /// <summary>格域左下角：与场景道具错开半格以上，测试区内不再有场景碰撞体</summary>
        private static readonly Vector2 TestOrigin = new(0.5f, 0.5f);

        private const int Width = 6;
        private const int Height = 6;

        private readonly List<GameObject> _spawned = new();

        private GridLogic _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = NewGrid();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }

            _spawned.Clear();
        }

        /// <summary>与 CombatRoot.Assemble 同一条装配：清单与工厂都来自表</summary>
        private static GridLogic NewGrid()
        {
            ReactionResolver.Initialize(ConfigModule.GetElementRules());
            TileChainReactor.Clear();

            var grid = new GridLogic(
                new GridGeometry(TestOrigin, 1f),
                ConfigModule.GetAllTileStates(),
                CreateState,
                ConfigModule.GetDuoReactions());

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) grid.RegisterCell(new Vector3Int(x, y, 0));
            }

            return grid;
        }

        /// <summary>空网格上的占用数：只用来消掉场景折叠道具造成的基准差</summary>
        private static int CleanBaseline()
        {
            return SceneObstacleBaker.Bake(NewGrid(), 0.5f);
        }

        private static ITileState CreateState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        private GameObject NewBox(string name, Vector2 center, Vector2 size, bool trigger = false, float yOffset = 0f)
        {
            var go = new GameObject(name);

            _spawned.Add(go);
            go.transform.position = new Vector3(center.x, center.y, 0f);

            var box = go.AddComponent<BoxCollider2D>();

            box.size = size;
            box.offset = new Vector2(0f, yOffset);
            box.isTrigger = trigger;

            return go;
        }

        private static void Sync()
        {
            Physics2D.SyncTransforms();
        }

        /// <summary>⑦ 真实预制体：玩家与敌人的碰撞体绝不能被烘成墙，否则角色脚下会长出隐形障碍</summary>
        [Test]
        public void 真实角色预制体不被烘焙()
        {
            var player = (GameObject)Object.Instantiate(Resources.Load<GameObject>("scane_prebs/Player"));

            _spawned.Add(player);
            player.transform.position = new Vector3(2.5f, 2.5f, 0f);

            var enemy = (GameObject)Object.Instantiate(Resources.Load<GameObject>("enemies/Enemy_1"));

            _spawned.Add(enemy);
            enemy.transform.position = new Vector3(4.5f, 4.5f, 0f);

            Sync();

            SceneObstacleBaker.Bake(_grid, 0.5f);

            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(2, 2, 0)), "玩家所在格被标占用");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(4, 4, 0)), "敌人所在格被标占用");

            // 反面锚点：同位置换成没有角色的静态碰撞体，必须判得出来
            GameObject staticProp = NewBox("静态道具", new Vector2(2.5f, 2.5f), new Vector2(1f, 1f));

            Sync();

            Assert.IsTrue(SceneObstacleBaker.Bake(_grid, 0.5f) > 0, "同位置的静态碰撞体也没判出来：判据本身失效了");
            Assert.IsNotNull(staticProp);
        }

        /// <summary>造一只只认网格与执行的敌人，不碰引擎</summary>
        private EnemyLogic NewEnemy(MotorProbe motor)
        {
            var entry = new EnemyTuningEntry { id = 1, radius = 1f };
            var spec = new EnemySpec(RowFactory.EnemyRow(), entry);

            var logic = new EnemyLogic(motor, spec);

            logic.AttachGrid(_grid);
            logic.SetTarget(new Vector2(TestOrigin.x + 0.5f, TestOrigin.y + 0.5f));

            return logic;
        }

        /// <summary>① 薄墙：按半径判距的回归锚点 —— 半格厚的墙必须挡住两侧格，退回点采样就会全漏</summary>
        [Test]
        public void 薄墙按身体半径挡住两侧格()
        {
            // 贴在 y=1.5 的 1 格宽薄墙：格心与四角都不落在它体内，只有按半径算距离才判得出来
            NewBox("薄墙", new Vector2(TestOrigin.x + 1f, TestOrigin.y + 1f), new Vector2(1.2f, 0.1f));
            Sync();

            SceneObstacleBaker.Bake(_grid, 0.5f);

            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(1, 1, 0)), "墙正下方（同排）的格没被标占用：判据退回了点采样");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(1, 2, 0)), "距墙 1 格的排被误标：半径算成了整格外扩");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(1, 4, 0)), "距墙 3 格的排被误标");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(4, 4, 0)), "远处的空格被误标");

            // 同样半格厚、但纵向占满 2 格的墙：半径必须把上下两格都算进来
            var go = new GameObject("竖薄墙");

            _spawned.Add(go);
            go.transform.position = new Vector3(TestOrigin.x + 3f, TestOrigin.y + 1.5f, 0f);

            var box = go.AddComponent<BoxCollider2D>();

            box.size = new Vector2(0.1f, 2f);
            Sync();

            SceneObstacleBaker.Bake(_grid, 0.5f);

            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(3, 1, 0)), "薄墙纵向第 1 格没被标");
            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(3, 2, 0)), "薄墙纵向第 2 格没被标");

            // 半径归零：0.1 厚的墙压不住任何格心，一格都不该标（证明半径真的参与了判据）
            Assert.AreEqual(0, SceneObstacleBaker.Bake(_grid, 0f), "半径 0 时 0.1 厚的墙仍占用了格");
        }

        /// <summary>② 预制体实尺寸：1×4 且 offset y=-0.3 的长条必须按 offset 后的真实范围判</summary>
        [Test]
        public void 带偏移的长条碰撞体按真实范围占用()
        {
            NewBox("栅栏", new Vector2(TestOrigin.x + 3.5f, TestOrigin.y + 2.5f), new Vector2(1f, 4f), yOffset: -0.3f);
            Sync();

            CleanBaseline();
            SceneObstacleBaker.Bake(_grid, 0.5f);

            // 实体范围 x∈[3,4]、y∈[0.2,4.2]：南端第 0 排已进半径、北端只到第 4 排
            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(3, 0, 0)), "长条南端格没被标（offset y=-0.3 没算进去）");
            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(3, 1, 0)), "长条中段格没被标");
            Assert.IsTrue(_grid.IsSolidCell(new Vector3Int(4, 4, 0)), "长条北端格没被标");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(1, 2, 0)), "距长条 2 列外被误标：半径算成了整格外扩");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(3, 5, 0)), "长条上方第 5 排被误标：offset 没算进去");
        }

        /// <summary>③ 排除口径：触发器、角色标记、动态体都不算障碍</summary>
        [Test]
        public void 触发器角色与动态体都不算障碍()
        {
            int baseline = CleanBaseline();

            NewBox("地图边界", new Vector2(TestOrigin.x + 3f, TestOrigin.y + 3f), new Vector2(60f, 30f), trigger: true);

            GameObject actor = NewBox("角色", new Vector2(TestOrigin.x + 1.5f, TestOrigin.y + 1.5f), new Vector2(1f, 1f));

            actor.AddComponent<ManagedActorStub>();

            GameObject crate = NewBox("木箱", new Vector2(TestOrigin.x + 4.5f, TestOrigin.y + 4.5f), new Vector2(1f, 1f));

            Rigidbody2D body = crate.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Dynamic;
            Sync();

            Assert.AreEqual(baseline, SceneObstacleBaker.Bake(_grid, 0.5f), "触发器/角色/动态体被当成了障碍");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(2, 2, 0)), "整张地图的边界触发器把格子烘成了墙");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(4, 4, 0)), "可推动的木箱把格子烘成了墙");
            Assert.IsFalse(_grid.IsSolidCell(new Vector3Int(1, 1, 0)), "挂了 IManagedActor 的角色把格子烘成了墙");
        }

        /// <summary>④ 幂等：重复烘焙（换场景重进）不能越积越多</summary>
        [Test]
        public void 重复烘焙结果一致()
        {
            int baseline = CleanBaseline();

            NewBox("薄墙", new Vector2(TestOrigin.x + 2f, TestOrigin.y + 1f), new Vector2(1.2f, 0.1f));
            Sync();

            int first = SceneObstacleBaker.Bake(_grid, 0.5f);
            int second = SceneObstacleBaker.Bake(_grid, 0.5f);

            Assert.AreEqual(first, second, "重复烘焙的占用格数变了：ClearSolidCells 没生效");
            Assert.Greater(first, baseline, "薄墙一格都没标出来");

            // 拆掉墙再烘焙：新增占用必须全部释放
            Object.DestroyImmediate(_spawned[0]);

            _spawned.Clear();
            Sync();

            Assert.AreEqual(baseline, SceneObstacleBaker.Bake(_grid, 0.5f), "障碍已拆，旧占用没清干净");
        }

        /// <summary>⑤ 脱困：速度被物理压到零而大脑仍在追，满帧后必须换一个背离阻挡的方向</summary>
        [Test]
        public void 卡死满帧后给出脱困方向()
        {
            // 障碍占 (1,1)(1,2)(2,1)(2,2)，敌人贴在其西南角 (0,0)：直扑方向被墙挡住
            NewBox("墙", new Vector2(TestOrigin.x + 2f, TestOrigin.y + 2f), new Vector2(2f, 2f));
            Sync();

            SceneObstacleBaker.Bake(_grid, 0.5f);

            var motor = new MotorProbe { Position = _grid.Geometry.CellCenter(Vector3Int.zero) };
            EnemyLogic enemy = NewEnemy(motor);

            Vector2 intoWall = new Vector2(1f, 1f);

            // 物理把速度压到零：引擎侧速度始终为 0，模拟顶墙
            float now = 0f;

            for (int i = 0; i < 40; i++)
            {
                now += 0.02f;
                motor.EngineVelocity = Vector2.zero;
                enemy.Tick(now, 0.02f);
            }

            Vector2 resolved = enemy.ResolveObstacle(intoWall);

            Assert.Less(resolved.y, 0f, "贴墙卡死后仍在朝墙施力：脱困没触发（或方向算反）");
        }

        /// <summary>⑥ 地图角落：格外空处不算墙，八邻域里只有一堵墙时脱困方向必须非零，否则永远解不开</summary>
        [Test]
        public void 地图角落脱困方向非零()
        {
            // 敌人落在已登记区最左下角 (0,0)，只有 (1,1) 挡着：其余邻格要么是空处、要么在图外
            NewBox("角墙", new Vector2(TestOrigin.x + 1.5f, TestOrigin.y + 1.5f), new Vector2(1f, 1f));
            Sync();

            SceneObstacleBaker.Bake(_grid, 0.5f);

            var motor = new MotorProbe { Position = _grid.Geometry.CellCenter(Vector3Int.zero) };
            EnemyLogic enemy = NewEnemy(motor);

            Vector2 intoCorner = new Vector2(1f, 1f);
            float now = 0f;

            for (int i = 0; i < 40; i++)
            {
                now += 0.02f;
                motor.EngineVelocity = Vector2.zero;
                enemy.Tick(now, 0.02f);
            }

            Vector2 resolved = enemy.ResolveObstacle(intoCorner);

            Assert.Greater(resolved.sqrMagnitude, 0f, "角落里脱困方向算成了零向量：账本会当停住，卡死再也解不开");
        }

        /// <summary>只挂标记接口，用来验证 IManagedActor 排除</summary>
        private sealed class ManagedActorStub : MonoBehaviour, IManagedActor
        {
        }
    }
}
