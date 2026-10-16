// ---------------------------------------------------------------------------
// 敌人寻路 · 运行期测试（纯逻辑：不碰物理、不读场景）
//
// ASCII 地形：'#' 墙、'.' 空地、'=' 窄口，首行是**最上面**那排；净空逐采样点铺开（见 NewMaze 与 WallDistance）。
//   距离一律以**采样点**为单位：一格 = 4 个采样点。
//
// 跑法：Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Movement;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 寻路Tests
    {
        private const float Radius = 0.3f;
        private const float Skin = 0.05f;

        /// <summary>窄口净空：半径 0.3 挤得过、半径 0.5 挤不过</summary>
        private const float Narrow = 0.5f;

        private const float Cap = 1f;

        private const float Acceleration = 5f;

        /// <summary>测试格域几何；与 NavMap 共用同一套角点口径</summary>
        private static readonly GridGeometry Geometry = new(Vector2.zero, 1f);

        private static float MaxSpeed => RowFactory.EnemyRow().MaxSpeed;

        /// <summary>手写地形 → 已登记格的 GridLogic ＋ 净空图；墙格也登记</summary>
        /// <remarks>净空按采样点铺成"到最近墙格矩形的距离"并封顶 Cap：整格填常数会让贴墙那半格假装站得住。</remarks>
        private static GridLogic NewMaze(string[] rows, out NavMap map)
        {
            int height = rows.Length;
            int width = rows[0].Length;

            var grid = new GridLogic(
                Geometry,
                ConfigModule.GetAllTileStates(),
                CreateState,
                ConfigModule.GetDuoReactions());

            map = new NavMap(Vector2Int.zero, new Vector2Int(width, height), in Geometry);

            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];

                for (int x = 0; x < width; x++)
                {
                    grid.RegisterCell(new Vector3Int(x, y, 0));

                    for (int j = 0; j < NavMap.Subdivision; j++)
                    {
                        for (int i = 0; i < NavMap.Subdivision; i++)
                        {
                            float cap = row[x] == '=' ? Narrow : Cap;

                            map.SetSampleClearance(Node(x, y, width, i, j), row[x] == '#' ? 0f : Mathf.Min(cap, WallDistance(rows, x, y, i, j)));
                        }
                    }
                }
            }

            grid.SetNavMap(map);

            return grid;
        }

        /// <summary>采样点到最近墙格矩形的距离；封顶 Cap，图外不算墙</summary>
        private static float WallDistance(string[] rows, int x, int y, int i, int j)
        {
            float best = Cap;
            float px = x + (i + 0.5f) / NavMap.Subdivision;
            float py = y + (j + 0.5f) / NavMap.Subdivision;

            for (int wy = 0; wy < rows.Length; wy++)
            {
                string row = rows[rows.Length - 1 - wy];

                for (int wx = 0; wx < row.Length; wx++)
                {
                    if (row[wx] != '#') continue;

                    float dx = Mathf.Max(Mathf.Max(wx - px, px - (wx + 1)), 0f);
                    float dy = Mathf.Max(Mathf.Max(wy - py, py - (wy + 1)), 0f);

                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }

            return best;
        }

        private static int CellIndex(int x, int y, int width)
        {
            return x + y * width;
        }

        /// <remarks>必须是**采样坐标的行主序**：流场按 ±1 / ±SampleWidth 走四邻，格内分块布局会让"相邻"落到别的格上。</remarks>
        private static int Node(int x, int y, int width, int i, int j)
        {
            int sub = NavMap.Subdivision;

            return (x * sub + i) + (y * sub + j) * width * sub;
        }

        /// <summary>格心附近那个采样点：种子与"从这里出发"都用它</summary>
        private static int CenterNode(int x, int y, int width)
        {
            return Node(x, y, width, 1, 1);
        }

        private static string[] OpenField(int width, int height)
        {
            var rows = new string[height];

            for (int i = 0; i < height; i++) rows[i] = new string('.', width);

            return rows;
        }

        private static ITileState CreateState(TileStateType id)
        {
            TileStateSpec spec = ConfigModule.TryGetTileState(id);

            return spec != null ? new TableTileState(spec) : null;
        }

        /// <summary>造一只只认网格与执行的敌人</summary>
        private static EnemyLogic NewEnemy(GridLogic grid, MotorProbe motor, float radius = Radius, float acceleration = Acceleration)
        {
            var entry = new EnemyTuningEntry { id = 1, radius = radius, acceleration = acceleration };
            var spec = new EnemySpec(RowFactory.EnemyRow(), entry);

            var logic = new EnemyLogic(motor, spec);

            logic.AttachGrid(grid);

            return logic;
        }

        /// <summary>沿场走到底，返回步数；走不到终点返回 -1</summary>
        private static int WalkTo(NavField field, int start, int goal, int limit = 256)
        {
            int current = start;

            for (int i = 0; i <= limit; i++)
            {
                if (current == goal) return i;

                if (!field.TryGetStep(current, out int next)) return -1;

                current = next;
            }

            return -1;
        }

        /// <summary>沿场走到走不动为止，返回停下的采样点</summary>
        private static int WalkUntilStuck(NavField field, int start, int limit = 256)
        {
            int current = start;

            for (int i = 0; i < limit; i++)
            {
                if (!field.TryGetStep(current, out int next)) break;

                current = next;
            }

            return current;
        }

        /// <summary>从出发节点沿链走到第一个拐点，返回拐点的世界坐标；链上没拐点就返回链尾</summary>
        /// <remarks>与转向器同一个判据：相邻两步方向点积 &lt; 0.9 即拐。leg 必须归一，否则"同向"会被判成"拐弯"。</remarks>
        private static Vector2 CornerPoint(NavMap map, NavField field, Vector2 position)
        {
            Assert.IsTrue(field.TryFindStart(position, out int node), "找不到出发节点");

            Vector2 leg = Vector2.zero;
            Vector2 point = map.SampleCenter(node);

            for (int i = 0; i < 64; i++)
            {
                point = map.SampleCenter(node);

                if (!field.TryGetStep(node, out int next)) break;

                Vector2 step = map.SampleCenter(next) - point;

                if (step.sqrMagnitude <= 0f) break;

                if (i > 0 && Vector2.Dot(step.normalized, leg) < 0.9f) break;

                leg = step.normalized;
                node = next;
            }

            return point;
        }

        private static int Manhattan(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        [Test]
        public void 空地上每个采样点都能一步不绕地走到目标()
        {
            GridLogic grid = NewMaze(OpenField(10, 5), out NavMap map);

            var goal = new Vector3Int(7, 2, 0);

            grid.RefreshNavFields(goal, Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            Assert.AreEqual(10 * 5 * NavMap.SamplesPerCell, field.ReachableCount, "空地上整片采样点都该可达");
            Assert.IsTrue(grid.Nav.IsWalkable(goal, Radius, Skin), "空地的目标格自己站不住");

            int seed = CenterNode(7, 2, 10);

            Assert.AreEqual(0, field.DistOf(seed), "种子不是目标格里最靠格心的采样点");

            int sub = NavMap.Subdivision;

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    for (int j = 0; j < sub; j++)
                    {
                        for (int i = 0; i < sub; i++)
                        {
                            int node = Node(x, y, 10, i, j);
                            int expected = Mathf.Abs(x * sub + i - (7 * sub + 1)) + Mathf.Abs(y * sub + j - (2 * sub + 1));

                            Assert.AreEqual(expected, field.DistOf(node), $"({x},{y}) 采样点 ({i},{j}) 的场距不是采样单位下的曼哈顿距离");
                            Assert.AreEqual(expected, WalkTo(field, node, seed), $"({x},{y}) 采样点 ({i},{j}) 沿场走不到目标");
                        }
                    }
                }
            }
        }

        [Test]
        public void 迷宫里的下一步不朝墙()
        {
            // 一道 x=4 的竖墙从 y=1 到 y=5：只剩 y=0 与 y=6 两排
            string[] rows =
            {
                ".........",
                "....#....",
                "....#....",
                "....#....",
                "....#....",
                "....#....",
                ".........",
            };

            GridLogic grid = NewMaze(rows, out NavMap map);

            var goal = new Vector3Int(8, 3, 0);

            grid.RefreshNavFields(goal, Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            int start = CenterNode(0, 3, 9);

            // 54 = 净下 11 ＋ 横移 32 ＋ 上来 11；y=0 那排只到 j=2
            Assert.AreEqual(NavMap.Subdivision * 13 + 2, field.DistOf(start), $"绕行距离不对：场没绕墙（直走 8 格）可达={field.ReachableCount}");
            Assert.AreEqual(NavMap.Subdivision * 13 + 2, WalkTo(field, start, CenterNode(8, 3, 9)), "沿场走不到玩家");

            // 贴墙那一格（x=3）的链必须沿格内往下/往右走，不能一步跨进 x=4 的墙格
            int current = CenterNode(3, 3, 9);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(field.TryGetStep(current, out current), $"贴墙那条链第 {i} 步断了");
                Assert.AreNotEqual(new Vector3Int(4, 3, 0), map.CellOfSample(current), "贴墙格的链走进了墙里");
            }
        }

        [Test]
        public void 目标格站不住时流场照样建成()
        {
            // 玩家贴着栅栏站：他所在格净空不足，种子必须外搜到紧邻的可站格
            string[] rows = OpenField(8, 5);

            rows[2] = ".......#";

            GridLogic grid = NewMaze(rows, out NavMap map);

            var goal = new Vector3Int(7, 2, 0);

            grid.RefreshNavFields(goal, Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            Assert.Greater(field.ReachableCount, 0, "目标格站不住时场整个没建：绕障全部失效（旧实现就是这样）");

            int stop = WalkUntilStuck(field, CenterNode(0, 2, 8));

            Assert.AreEqual(0, field.DistOf(stop), "停下的节点不是种子");
            Assert.AreEqual(1, Manhattan(map.CellOfSample(stop), goal), $"没走到玩家旁边就停了：停在 {map.CellOfSample(stop)}");
        }

        [Test]
        public void 窄口只放行挤得过的体半径()
        {
            // x=4 一列：y=0/1 是墙，y=2/3 是窄口（净空 0.5），y=4 是空地
            string[] rows =
            {
                ".........",
                "....=....",
                "....=....",
                "....#....",
                "....#....",
            };

            GridLogic grid = NewMaze(rows, out NavMap map);

            grid.RefreshNavFields(new Vector3Int(8, 2, 0), Skin);

            NavField small = grid.NavFieldFor(0.3f, Skin);
            NavField big = grid.NavFieldFor(0.5f, Skin);

            int start = CenterNode(0, 2, 9);
            int sub = NavMap.Subdivision;

            Assert.AreEqual(sub * 8, small.DistOf(start), "半径 0.3 该穿窄口（直走 8 格）");
            Assert.AreEqual(sub * 8 + 14, big.DistOf(start), "半径 0.5 该绕上方：横移 32 ＋ 上下各让 7 个采样点。共用一张图就会让它去挤窄口");

            Assert.GreaterOrEqual(small.DistOf(CenterNode(4, 2, 9)), 0, "半径 0.3 的场里窄口不可达");
            Assert.AreEqual(-1, big.DistOf(CenterNode(4, 2, 9)), "半径 0.5 的场把挤不过去的窄口也算成了可走");
        }

        [Test]
        public void 薄墙横插在两排采样点之间时不可通行()
        {
            GridLogic grid = NewMaze(OpenField(5, 5), out NavMap map);

            var goal = new Vector3Int(4, 2, 0);

            grid.RefreshNavFields(goal, Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            int start = CenterNode(0, 2, 5);

            Assert.AreEqual(NavMap.Subdivision * 4, field.DistOf(start), "空地直走该是 4 格");

            // 一条竖墙压在 (2,2) 与 (3,2) 的格界上：界两侧最靠界的那列采样点都站不住
            for (int j = 0; j < NavMap.Subdivision; j++)
            {
                map.SetSampleClearance(Node(2, 2, 5, NavMap.Subdivision - 1, j), 0f);
                map.SetSampleClearance(Node(3, 2, 5, 0, j), 0f);
            }

            grid.SetNavMap(map);

            field = grid.NavFieldFor(Radius, Skin);

            Assert.AreEqual(-1, field.DistOf(Node(2, 2, 5, NavMap.Subdivision - 1, 1)), "墙上的采样点被判成可走");

            // 绕行代价 = 从本排下到邻排再上来，共 4 个采样点（横向上不多走）
            Assert.AreEqual(NavMap.Subdivision * 4 + 4, field.DistOf(start), "界过不去却没绕路：薄墙没进判据");
        }

        [Test]
        public void 栅栏压在格心上时格子仍要连得通()
        {
            GridLogic grid = NewMaze(OpenField(5, 3), out NavMap map);

            // 一条横栅栏正压在 y=1 那排的格心线上：j=1、j=2 两排采样点全站不住，通道只剩格的上下两条边
            for (int x = 0; x < 5; x++)
            {
                for (int j = 1; j < 3; j++)
                {
                    for (int i = 0; i < NavMap.Subdivision; i++)
                    {
                        map.SetSampleClearance(Node(x, 1, 5, i, j), 0f);
                    }
                }
            }

            grid.SetNavMap(map);
            grid.RefreshNavFields(new Vector3Int(4, 1, 0), Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            Assert.AreEqual(-1, field.DistOf(CenterNode(2, 1, 5)), "格心采样点没被挡住：用例没造出栅栏压格心的地形");
            Assert.IsTrue(map.IsWalkable(new Vector3Int(2, 1, 0), Radius, Skin), "格内还有站得住的地方，整格却被判成不可走");
            Assert.GreaterOrEqual(field.CellDistOf(new Vector3Int(2, 1, 0)), 0, "整格被判成连不上目标：通道从格边走时场必须仍铺得开");
            Assert.AreEqual(NavMap.Subdivision * 4, field.DistOf(Node(0, 1, 5, 1, 0)), "沿格下边的那条通道没连到目标");
        }

        [Test]
        public void 场只在目标格或阻挡变化时重建()
        {
            GridLogic grid = NewMaze(OpenField(6, 4), out NavMap map);

            var goal = new Vector3Int(5, 1, 0);

            grid.RefreshNavFields(goal, Skin);
            grid.NavFieldFor(Radius, Skin);

            int builds = grid.NavFields.TotalRebuilds;

            Assert.Greater(builds, 0, "第一张场没建");

            for (int i = 0; i < 20; i++) grid.RefreshNavFields(goal, Skin);

            Assert.AreEqual(builds, grid.NavFields.TotalRebuilds, "目标格没变却重建了：站着不动的玩家会每帧重算全场");

            grid.RefreshNavFields(new Vector3Int(4, 1, 0), Skin);

            Assert.AreEqual(builds + 1, grid.NavFields.TotalRebuilds, "目标格变了没重建");

            builds = grid.NavFields.TotalRebuilds;

            Assert.IsTrue(grid.SwitchTileState(new Vector3Int(1, 1, 0), ObstacleState()), "is_obstacle 状态没切成功");

            grid.RefreshNavFields(new Vector3Int(4, 1, 0), Skin);

            Assert.AreEqual(builds + 1, grid.NavFields.TotalRebuilds, "阻挡地貌翻转没作废流场");

            NavField field = grid.NavFieldFor(Radius, Skin);

            Assert.IsTrue(grid.Nav.IsWalkable(new Vector3Int(1, 1, 0), Radius, Skin), "净空本身没变（地貌阻挡与净空是两套判据）");
            Assert.AreEqual(-1, field.DistOf(CenterNode(1, 1, 6)), "地貌阻挡起来了，那个采样点仍在场里");
        }

        /// <summary>表里第一个 is_obstacle = true 的状态</summary>
        private static TileStateType ObstacleState()
        {
            IReadOnlyList<TileStateSpec> specs = ConfigModule.GetAllTileStates();

            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].IsObstacle) return specs[i].Id;
            }

            Assert.Fail("tile_state 表里没有 is_obstacle = true 的行：导航的动态阻挡没有数据源");

            return TileStateType.Normal;
        }

        /// <summary>L 形走廊：底排 x=0..2 接 (2,0)→(2,5) 竖排，唯一拐角在 (2,0) 那格</summary>
        /// <remarks>竖排往上多留两格：滑行段必须落在可走格里</remarks>
        private static string[] CornerMaze()
        {
            return new[]
            {
                "#####",
                "##.##",
                "##.##",
                "##.##",
                "##.##",
                "##.##",
                "...##",
            };
        }

        [Test]
        public void 拐角前按刹车距离降速()
        {
            GridLogic grid = NewMaze(CornerMaze(), out NavMap map);

            var goal = new Vector3Int(2, 3, 0);

            grid.RefreshNavFields(goal, Skin);

            NavField field = grid.NavFieldFor(Radius, Skin);

            var motor = new MotorProbe();
            EnemyLogic enemy = NewEnemy(grid, motor);

            enemy.SetTarget(new Vector2(2.5f, 3.5f));

            // 远离拐角：刹车速度还高于满速，不该降速（取走廊中间那排：0.875 那排离上侧墙格只剩 0.125）
            motor.Position = new Vector2(0.5f, 0.625f);
            motor.EngineVelocity = new Vector2(MaxSpeed, 0f);
            enemy.Tick(0.02f, 0.02f);

            Assert.AreEqual(MaxSpeed, enemy.ResolveSteering().Speed, 0.05f, "拐角还没进入刹车区就降速了");

            // 贴到拐点西侧：入弯速度 = √(v_c² + 2a·d)，d 是到链上拐点的距离
            var position = new Vector2(1.6f, 0.625f);
            float distance = (CornerPoint(map, field, position) - position).magnitude;

            Assert.Greater(distance, 0.2f, "没量到拐点：用例地形没造出拐角");

            motor.Position = position;
            motor.EngineVelocity = new Vector2(MaxSpeed, 0f);
            enemy.Tick(0.04f, 0.02f);

            float cornerSpeed = ConfigModule.CornerSpeedFactor * MaxSpeed;
            float expected = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * Acceleration * distance);

            Assert.Less(expected, MaxSpeed, "这个位置量出的刹车速度高于满速，验不出降速");
            Assert.AreEqual(expected, enemy.ResolveSteering().Speed, 0.15f, "入弯降速没按刹车距离算");
        }

        [Test]
        public void 满速进弯不会飘出走廊()
        {
            GridLogic grid = NewMaze(CornerMaze(), out NavMap map);

            var goal = new Vector3Int(2, 3, 0);

            grid.RefreshNavFields(goal, Skin);

            var motor = new MotorProbe { Position = new Vector2(0.5f, 0.5f) };
            EnemyLogic enemy = NewEnemy(grid, motor);

            enemy.SetTarget(new Vector2(2.5f, 3.5f));

            // 满速朝东起步：假物理只把本帧速度积到位置上（转弯半径全靠转向器自己收）
            motor.EngineVelocity = new Vector2(MaxSpeed, 0f);

            float now = 0f;
            float maxDrift = 0f;

            for (int i = 0; i < 150; i++)
            {
                now += 0.02f;
                enemy.Tick(now, 0.02f);
                motor.Position += motor.EngineVelocity * 0.02f;

                Assert.IsTrue(map.TrySampleAt(motor.Position, out int node), $"第 {i} 帧跑出了图（位置 {motor.Position}）");

                // 假物理没有碰撞体：逐帧只判"还在不在有地方站的格"（真身靠刚体贴墙滑）
                Assert.IsTrue(map.IsWalkable(map.CellOfSample(node), Radius, Skin), $"第 {i} 帧进了实心墙格（位置 {motor.Position}）：入弯降速没生效");

                if (motor.Position.y > 0.55f) maxDrift = Mathf.Max(maxDrift, motor.Position.x - 2f);
            }

            Assert.Less(maxDrift, 1f, $"转弯飘出走廊 {maxDrift:F2} 格：满速 90° 的转弯半径是 v²/a");

            var final = new Vector3Int(Mathf.FloorToInt(motor.Position.x), Mathf.FloorToInt(motor.Position.y), 0);

            Assert.AreEqual(2, final.x, $"拐完之后没待在竖排里（停在 {motor.Position}）");
            Assert.GreaterOrEqual(final.y, 3, $"没顺着竖排走到目标格那一排（停在 {motor.Position}）");
        }

        /// <summary>同伴桩：只实现站得住与半径</summary>
        private sealed class BodyStub : IEffectTarget, IEnemyBody
        {
            public Vector2 Position { get; set; }

            public float Radius { get; set; }

            public bool IsAlive => true;
        }

        [Test]
        public void 同伴挤过来时让开且不许让进墙里()
        {
            GridLogic grid = NewMaze(OpenField(6, 3), out NavMap map);

            grid.RefreshNavFields(new Vector3Int(5, 1, 0), Skin);

            // 站在种子那一排采样点上（j=5）：流场方向正好正东，横向分量只剩软分离
            var motor = new MotorProbe { Position = new Vector2(1.5f, 1.375f) };
            EnemyLogic enemy = NewEnemy(grid, motor);

            enemy.SetTarget(new Vector2(5.5f, 1.5f));

            grid.Registry.Register(new Vector3Int(1, 1, 0), new BodyStub { Position = new Vector2(1.6f, 1.475f), Radius = Radius });

            motor.EngineVelocity = new Vector2(MaxSpeed, 0f);
            enemy.Tick(0.02f, 0.02f);

            Steering steered = enemy.ResolveSteering();

            Assert.Greater(steered.Direction.x, 0f, "流场方向被挤没了：软分离不该盖过路线");
            Assert.Less(steered.Direction.y, -0.1f, "同伴在东北侧，方向却没有任何让开的横向分量");

            // 让开的方向是西南，而西南那一格是墙：横向分量必须被丢掉，不许把同伴推进墙里
            map.SetCellClearance(CellIndex(0, 0, 6), 0f);
            grid.SetNavMap(map);

            motor.EngineVelocity = new Vector2(MaxSpeed, 0f);
            enemy.Tick(0.04f, 0.02f);

            Steering againstWall = enemy.ResolveSteering();

            Assert.AreEqual(0f, againstWall.Direction.y, 0.01f, "让开方向那一侧是墙，横向分量仍被加进了方向");
        }

        [Test]
        public void 卡死满帧后换到次优邻格()
        {
            GridLogic grid = NewMaze(OpenField(8, 3), out NavMap map);

            grid.RefreshNavFields(new Vector3Int(7, 1, 0), Skin);

            // 站在目标格心那一排采样点上：上下邻点的场距都更远
            var motor = new MotorProbe { Position = new Vector2(2.375f, 1.375f) };
            EnemyLogic enemy = NewEnemy(grid, motor);

            enemy.SetTarget(new Vector2(7.5f, 1.5f));

            NavField field = grid.NavFieldFor(Radius, Skin);

            Assert.IsTrue(field.TryFindStart(motor.Position, out int node), "找不到出发节点");
            Assert.IsTrue(field.TryGetStep(node, out int primary), "首选步没取到");

            // 速度必须在 Tick 之后清零（写在前面会被当帧覆盖）
            float now = 0f;

            for (int i = 0; i < ConfigModule.StuckFrames + 2; i++)
            {
                now += 0.02f;
                enemy.Tick(now, 0.02f);
                motor.EngineVelocity = Vector2.zero;
            }

            Assert.AreEqual(1, enemy.Navigator.JamCount, "速度被压到零满帧后没触发换步");

            Assert.IsTrue(field.TryGetStepOption(node, true, out int detour), "换步取不到次优邻节点：开阔地上唯一的改善方向就是首选，只认更近等于换不出去");
            Assert.AreNotEqual(primary, detour, "换步又选回了卡住的那条边");
            Assert.GreaterOrEqual(field.DistOf(detour), 0, "换步走到了场外");

            // 换步方向按 TryGetStepOption(node, true) 量：同距的南北邻节点先到先得，故是南侧
            Steering steered = enemy.ResolveSteering();

            Assert.Greater(steered.Direction.sqrMagnitude, 0f, "换步方向算成了零向量：账本会当停住，卡死再也解不开");
            Assert.Less(steered.Direction.y, -0.9f, "换步该横向滑开（次优邻节点在南侧），却仍在朝东顶");
        }

        /// <summary>验收：玩家在敌人正前方 8 单位/秒横向折返 3 次，再顶住它的脸 —— 命令方向不许翻面、单帧 |Δv| 不许超配置、贴脸不许算卡死</summary>
        /// <remarks>夹具自己积分位置，并做引擎会做的那件事：身体贴住时把速度压到零（下一帧帧首读到的就是它）。|Δv| 读账本本帧的提交量，不含物理压速。
        /// 折返段全程保持 1.8 格间距：要让怪在满速上撞见"原始方向掉头"，贴住身体会把速度压没、把这一条盖掉。
        /// 改前实测：命令方向单帧转过 60° 有 9 次、单帧 |Δv| 4.88（配置加速度 5 的 49 倍）、贴住满帧即换步。</remarks>
        [Test]
        public void 玩家折返与贴脸顶住都不该抖()
        {
            const float Dt = 0.02f;
            const float PlayerSpeed = 8f;
            const float Offset = 1.8f;

            // 身体接触距离：怪 0.3 ＋ 玩家碰撞体 0.4，大于停靠环 0.6 ⇒ 贴住那一线"想走却走不动"
            const float Contact = 0.7f;
            const float PressOffset = 0.65f;
            const float TurnLimit = 60f;

            // 8 单位/秒 × 2.1 秒 ÷（2 × 1.8 格）= 折返 3 次；顶住段要满 stuckFrames 帧才判得出假卡死
            const float WeaveSeconds = 2.1f;
            const float PressSeconds = 0.6f;

            GridLogic grid = NewMaze(OpenField(24, 8), out _);

            var motor = new MotorProbe { Position = new Vector2(8f, 4f) };
            EnemyLogic enemy = NewEnemy(grid, motor);

            float omega = PlayerSpeed / Offset;
            float time = 0f;
            int frames = 0;
            int turns = 0;
            float peak = 0f;
            Vector2 previous = Vector2.zero;
            var witness = new List<string>();

            int weaveSteps = Mathf.RoundToInt(WeaveSeconds / Dt);
            int steps = weaveSteps + Mathf.RoundToInt(PressSeconds / Dt);

            for (int i = 0; i < steps; i++)
            {
                time += Dt;
                frames++;

                // 折返段：钉在敌人正前方 Offset 格的相对位置上做横向正弦（相对速度恒为 8 单位/秒）
                // 顶住段：退到贴住它的脸（Contact 内），敌人想走却走不动
                Vector2 player = i < weaveSteps
                    ? motor.Position + new Vector2(Mathf.Sin(time * omega) * Offset, Offset)
                    : motor.Position + new Vector2(PressOffset, 0f);

                // 组合根每物理帧喂目标格，这里照做：流场只认目标格，不认目标身体
                enemy.SetTarget(player);

                grid.RefreshNavFields(
                    new Vector3Int(Mathf.FloorToInt(player.x), Mathf.FloorToInt(player.y), 0),
                    Skin);

                enemy.Tick(time, Dt);

                Steering steered = enemy.ResolveSteering();

                // 只在真的在走的那一帧量方向：停靠环内 speed = 0，此时 direction 仍是"目标−自己"，玩家穿过身边会翻面
                if (steered.Speed > 0f && steered.Direction.sqrMagnitude > 0f)
                {
                    Vector2 unit = steered.Direction.normalized;
                    float turn = previous.sqrMagnitude > 0f ? Vector2.Angle(previous, unit) : 0f;

                    if (turn > TurnLimit)
                    {
                        turns++;

                        if (witness.Count < 6)
                        {
                            witness.Add($"第 {frames} 帧：转过 {turn:F0}°，本帧速度 {motor.EngineVelocity.magnitude:F2}");
                        }
                    }

                    previous = unit;
                }
                else
                {
                    previous = Vector2.zero;
                }

                peak = Mathf.Max(peak, motor.SubmittedDelta.magnitude);

                motor.Position += motor.EngineVelocity * Dt;

                if ((motor.Position - player).sqrMagnitude < Contact * Contact) motor.EngineVelocity = Vector2.zero;
            }

            float bound = 5f * Acceleration * Dt;
            string report =
                $"{frames} 帧：命令方向单帧转过 {TurnLimit:F0}° 共 {turns} 次；" +
                $"峰值单帧 |Δv| {peak:F2}（{peak / Dt:F0} u/s²，上限 {bound:F2}）；" +
                $"JamCount {enemy.Navigator.JamCount}；原始方向切换 {enemy.Navigator.ModeSwitches} 次" +
                (witness.Count > 0 ? "；翻面帧 " + string.Join(" / ", witness) : "");

            TestContext.WriteLine(report);

            Assert.LessOrEqual(turns, 2, $"命令方向还在逐帧翻面：{report}");
            Assert.LessOrEqual(peak, bound, $"单帧 |Δv| 没被压回配置加速度量级：{report}");
            Assert.AreEqual(0, enemy.Navigator.JamCount, $"顶住目标身体被当成了卡死：{report}");
        }

        [Test]
        public void 没场可读时退回直线意图()
        {
            GridLogic grid = NewMaze(OpenField(4, 3), out NavMap map);

            var motor = new MotorProbe { Position = new Vector2(0.5f, 0.5f) };
            EnemyLogic enemy = NewEnemy(grid, motor);

            enemy.SetTarget(new Vector2(3.5f, 2.5f));

            // 从没喂过目标格：一张场都没有
            enemy.Tick(0.02f, 0.02f);

            Steering steered = enemy.ResolveSteering();

            Assert.Greater(steered.Direction.sqrMagnitude, 0f, "退回直线意图时给了零向量");
            Assert.Greater(Vector2.Dot(steered.Direction.normalized, new Vector2(1f, 1f).normalized), 0.9f, "退回的不是直线意图方向");

            // 图外同理：场给不出一步，也不能给零向量；方向仍是直线意图（目标 − 自己）
            motor.Position = new Vector2(20f, 20f);
            enemy.Tick(0.04f, 0.02f);

            Steering outside = enemy.ResolveSteering();
            Vector2 intent = new Vector2(3.5f, 2.5f) - motor.Position;

            Assert.Greater(outside.Direction.sqrMagnitude, 0f, "图外给了零向量");
            Assert.Greater(Vector2.Dot(outside.Direction.normalized, intent.normalized), 0.99f, "图外退回的不是直线意图方向");
        }
    }
}
