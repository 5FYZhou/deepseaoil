// ---------------------------------------------------------------------------
// 白模迁移 · 战斗框架 EditMode 测试
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 与 移动Tests.cs / Data层Tests.cs 同机制：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【留什么 · 砍什么】判据只有一条：这条用例守的是不是「改错了不报错、只表现为手感/观感不对」。
//   留：非法输入退化与非数兜底、格子状态机的一次性结算与到期、归属表生命周期、
//       血量/无敌帧的退化判据、接触判定、投掷裁决、波次计时、泥浆减速的稳态。
//   砍：守注释或转发的、同一语义的第 2、3 条、常量对常量，以及断言「不存在的契约」的
//       （原 G7_非法投掷参数：循环变量 bad 从未被使用 —— 四次构造同一个 spec，是空断言）。
//   另删：原 G11 颜色四态（观感常量不等断言）、原 G14 随机种子（守的是调试体验，不是运行时静默失效）。
//   同一主题的用例已并成一条，测试名即清单：删掉的编号不再出现。
//
// 【覆盖边界，写在明处】只测**纯逻辑**：几何 / 吸附 / 状态机 / 队列 / 结算 / 波次 / 血量。
//   不测：MonoBehaviour 生命周期、Tilemap、Physics2D、EffectModule、UIMgr、鼠标设备。
//   被测类型都收**结构体**而不是自己去读表，所以这里不需要初始化配表，
//   也就不受"表里改了数就红一片"的影响（表值 → 结构体的折算由 SpecCatalog 负责，属于运行期验收）。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Logic.Wave;
using NUnit.Framework;
using UnityEngine;
using cfg.demo;
namespace DeepseaOil.Tests
{
    /// <summary>白模迁移后的战斗框架测试（纯逻辑）。</summary>
    [Category("Combat")]
    public class 战斗框架Tests
    {
        // 夹具

        /// <summary>格边长 1、角点在原点的标准几何。</summary>
        private static GridGeometry Geometry() => new GridGeometry(Vector2.zero, 1f);

        /// <summary>球侧的观感参数（半径 / 出手抬高量）与逻辑无关，用 SO 的字段默认值。</summary>
        private static ThrowTuning Tuning() => ScriptableObject.CreateInstance<ThrowTuning>();

        /// <summary>一颗"水球"：落地把目标格切成泥浆（射程 5 / 时长 0.6 / 弧高 2 / 最近 0.4）。</summary>
        private static ProjectileSpec WaterBall() => new ProjectileSpec(RowFactory.WaterRow(), Tuning());

        /// <summary>只注册了"常规 ＋ 泥浆"两种状态的格子门面（效果执行者就是 GridLogic 自己）。</summary>
        private static GridLogic NewGrid(EnemyCellRegistry registry = null)
            => new GridLogic(
                Geometry(),
                new List<TileStateSpec> { new TileStateSpec(RowFactory.NormalRow()), new TileStateSpec(RowFactory.MudRow()) },
                id => id == TileStateType.Mud ? new MudTileState(new TileStateSpec(RowFactory.MudRow())) : null,
                registry);

        /// <summary>一格已注册的地板 ＋ 一个站在格心上的探针目标。</summary>
        private static GridLogic GridWithTarget(Vector3Int cell, ProbeTarget target, EnemyCellRegistry registry)
        {
            GridLogic grid = NewGrid(registry);
            grid.RegisterCell(cell);
            target.Position = grid.Geometry.CellCenter(cell);
            registry.Register(cell, target);
            return grid;
        }

        /// <summary>被结算的假目标：三个接口正是真实敌人的形状（IDamageable / ISlowEffectTarget / IAlivable）。</summary>
        private sealed class ProbeTarget : IDamageable, ISlowEffectTarget, IAlivable
        {
            public int HitCount;
            public float LastAmount;
            public float LastImpulse;
            public Vector2 LastDirection;
            public int SlowSubmitCount;
            public float LastSpeedScale;
            public float LastSlowSeconds;
            public bool IsAlive { get; set; } = true;
            public Vector2 Position { get; set; }

            public void TakeDamage(in Damage damage)
            {
                HitCount++;
                LastAmount = damage.Amount;
                LastImpulse = damage.Impulse;
                LastDirection = damage.Direction;
            }

            public void ApplySlow(float speedScale, float seconds)
            {
                SlowSubmitCount++;
                LastSpeedScale = speedScale;
                LastSlowSeconds = seconds;
            }
        }

        /// <summary>不碰引擎的移动执行器探针：底座是共享的 <see cref="MotorProbe"/>（账本与控制律复用生产实现）。</summary>
        private sealed class ProbeMotor : MotorProbe
        {
        }

        private static EnemySpec EnemySpecFixture() => new EnemySpec(RowFactory.EnemyRow());

        /// <summary>玩家取值边界（表行 ＋ 移动 SO ＋ 水球那一行）。</summary>
        private static PlayerSpec PlayerSpecFixture()
            => new PlayerSpec(RowFactory.PlayerRow(), ScriptableObject.CreateInstance<PlayerConfig>(),
                new ProjectileSpec(RowFactory.WaterRow(), Tuning()));
        private static WaveSpec WaveSpecFixture() => new WaveSpec(RowFactory.WaveRow());

        /// <summary>推进一个玩家逻辑帧（帧首把执行器速度归零模拟物理结算）。</summary>
        private static void TickPlayer(PlayerLogic logic, ProbeMotor motor, Vector2 move, float now, bool resetVelocity = true)
        {
            if (resetVelocity) motor.EngineVelocity = Vector2.zero;
            var snapshot = new InputSnapshot(move, false, false);
            var world = new WorldInfo(move, default(BoundsArea));
            logic.FixedTick(new LogicContext(now, 0.02f, in world, in snapshot));
        }

        /// <summary>记下每一次投掷请求的假裁决口（Accept 由用例控制，模拟"世界侧不接"）。</summary>
        private sealed class ProbeThrowSink : IThrowSink
        {
            public bool Accept = true;
            public int RequestCount;
            public ThrowIntent LastIntent;
            public bool RequestThrow(in ThrowIntent intent)
            {
                RequestCount++;
                LastIntent = intent;
                return Accept;
            }
        }

        /// <summary>收集 AimChanged 的订阅者（发布是去重的，所以条数本身就是断言对象）。</summary>
        private sealed class AimRecorder
        {
            public int Count;
            public AimChanged Last;
            public void Handle(AimChanged evt)
            {
                Count++;
                Last = evt;
            }
        }

        /// <summary>装一个"能瞄准、能投掷"的玩家：几何 1 米格、射程 5、裁决口由测试给。</summary>
        private static PlayerLogic NewAimingPlayer(ProbeMotor motor, InputBuffer buffer, PlayerSpec spec, IThrowSink sink, out AimRecorder recorder)
        {
            GridGeometry geometry = Geometry();
            var logic = new PlayerLogic(motor, spec, buffer);
            logic.ConfigureAim(in geometry, 5f, sink);
            recorder = new AimRecorder();
            EventBus<AimChanged>.Subscribe(recorder.Handle);
            return logic;
        }

        // G1 · 格子几何（可逆 / 负坐标向下取整 / 非法退化）

        [Test]
        public void G1_世界格换算可逆_负坐标向下取整_非法几何安全降级()
        {
            GridGeometry geo = Geometry();
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    Assert.AreEqual(cell, geo.WorldToCell(geo.CellCenter(cell)), $"格 {cell} 的中心换算回来应当是它自己");
                }
            }

            // -0.5 落在格 (-1) 里，而不是格 0：用截断取整会让 x=0 左侧那一格与 x=0 重叠。
            Assert.AreEqual(new Vector3Int(-1, -1, 0), geo.WorldToCell(new Vector2(-0.5f, -0.5f)));
            Assert.AreEqual(Vector3Int.zero, geo.WorldToCell(new Vector2(0.99f, 0.99f)));
            var degenerate = new GridGeometry(Vector2.zero, 0f);
            Assert.IsFalse(degenerate.IsValid, "格边长为 0 时几何必须判为非法");
            Assert.AreEqual(Vector3Int.zero, degenerate.WorldToCell(new Vector2(5f, 5f)));
            Assert.AreEqual(Vector2.zero, degenerate.CellCenter(new Vector3Int(5, 5, 0)));
            Assert.IsFalse(new GridGeometry(Vector2.zero, float.NaN).IsValid, "NaN 格边长必须判为非法（否则换算全是 NaN）");
        }

        // G2 · 瞄准吸附与退化输入

        [Test]
        public void G2_吸附到鼠标所在格_超距落点在射程内_退化输入不得产出非数()
        {
            GridGeometry geo = Geometry();
            var origin = new Vector2(0.5f, 0.5f);      // 格 (0,0) 的中心

            Assert.IsTrue(TileAim.TryGetAimCell(in geo, origin, new Vector2(2.2f, 0.5f), 5f, out Vector3Int cell));
            Assert.AreEqual(new Vector3Int(2, 0, 0), cell, "鼠标 x=2.2 应当落在格 (2,0)");
            for (int i = 0; i < 72; i++)
            {
                float radians = i * 5f * Mathf.Deg2Rad;
                var mouse = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 500f;
                Assert.IsTrue(TileAim.TryGetAimCell(in geo, origin, mouse, 5f, out Vector3Int far), $"角度 {i * 5}° 取不到格子");

                // 允许半格误差：格子是离散的，"最近的可及格"中心未必精确落在射程边界上。
                float distance = Vector2.Distance(origin, geo.CellCenter(far));
                Assert.LessOrEqual(distance, 5f + 0.75f, $"角度 {i * 5}° 的落点离射手 {distance}，明显超出射程 5");
            }

            // 方向向量为零 ⇒ 归一化会产生 NaN ⇒ 必须返回 false 而不是硬塞一个方向。
            Assert.IsFalse(TileAim.TryGetAimCell(in geo, origin, origin, 5f, out _));
            var badGeo = new GridGeometry(Vector2.zero, 0f);
            Assert.IsFalse(TileAim.TryGetAimCell(in badGeo, origin, new Vector2(3f, 3f), 5f, out _), "几何非法时必须返回 false");
            foreach (float reach in new[] { float.NaN, float.PositiveInfinity, 0f, -3f })
            {
                Assert.IsTrue(TileAim.TryGetAimCell(in geo, origin, new Vector2(3f, 3f), reach, out Vector3Int any),
                    $"射程 {reach} 应当被当成「不限」而不是拒绝");
                Vector2 center = geo.CellCenter(any);
                Assert.IsFalse(float.IsNaN(center.x) || float.IsNaN(center.y), $"射程 {reach} 时格心是 NaN");
            }
        }

        // G3 · 空间查询与 Tick 队列

        [Test]
        public void G3_邻居不含自己_范围查询按格心判距_队列当帧不可见且同帧去重()
        {
            var buffer = new List<Vector3Int>();
            var cell = new Vector3Int(2, 3, 0);
            GridQuery.GetNeighbors4(cell, buffer);
            Assert.AreEqual(4, buffer.Count);
            Assert.IsFalse(buffer.Contains(cell), "四邻不该包含自己（否则接触判定会把自己算成攻击者）");
            GridQuery.GetNeighbors8(cell, buffer);
            Assert.AreEqual(8, buffer.Count);
            Assert.IsFalse(buffer.Contains(cell), "八邻不该包含自己");
            GridGeometry geo = Geometry();
            var center = new Vector3Int(0, 0, 0);
            GridQuery.GetWithinRadius(in geo, center, 0f, buffer);
            Assert.AreEqual(1, buffer.Count, "半径 0 只该有中心格");

            // 半径 1：中心 + 4 邻 = 5（对角格心距 √2 > 1，不该进来）。
            GridQuery.GetWithinRadius(in geo, center, 1f, buffer);
            Assert.AreEqual(5, buffer.Count, "半径 1 应当覆盖中心与四邻，不含对角");
            GridQuery.GetWithinRadius(in geo, center, 1.5f, buffer);
            Assert.AreEqual(9, buffer.Count, "半径 1.5 应当覆盖 3×3 全部格子");
            var queue = new TileTickQueue();
            queue.Schedule(cell);
            Assert.AreEqual(0, queue.Count, "刚提交的请求不该在本帧就可见（否则会变成同帧递归）");
            queue.Schedule(cell);      // 同帧重复提交
            queue.Swap();
            Assert.AreEqual(1, queue.Count, "同一格在一帧里提交多次只算一次");
            Assert.AreEqual(cell, queue.Current[0]);
            queue.Swap();
            Assert.AreEqual(0, queue.Count, "请求是一次性的：不重新提交就不会再被处理");
        }

        // G5 · 格子状态机与结算

        [Test]
        public void G5_水球落地一次结算_伤害方向_没地板与初始状态都不生效()
        {
            var registry = new EnemyCellRegistry();
            var target = new ProbeTarget();
            var cell = new Vector3Int(2, 2, 0);
            GridLogic grid = GridWithTarget(cell, target, registry);
            target.Position = grid.Geometry.CellCenter(cell) + new Vector2(0.3f, 0f);   // 方向断言要一点偏移

            Assert.IsTrue(grid.OnBallHit(cell, WaterBall().TileState), "落在合法格上必须生效");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell), "状态应当切成泥浆");
            Assert.AreEqual(1, target.HitCount, "状态转换应当结算一次伤害");
            Assert.AreEqual(1f, target.LastAmount, 1e-4f);
            Assert.AreEqual(1f, target.LastDirection.x, 1e-3f, "伤害方向从格心指向受害者");
            Assert.AreEqual(1.83f, target.LastImpulse, 1e-3f, "击退冲量来自状态配置行");
            Assert.AreEqual(0.45f, target.LastSpeedScale, 1e-4f, "减速乘数来自状态配置行");
            Assert.Greater(target.LastSlowSeconds, 0f, "续命时长必须为正，否则修饰立刻过期");

            // 落在没有地板的格上：状态与伤害都不该动。
            var voidCell = new Vector3Int(9, 9, 0);
            var bystander = new ProbeTarget { Position = grid.Geometry.CellCenter(voidCell) };
            registry.Register(voidCell, bystander);
            Assert.IsFalse(grid.OnBallHit(voidCell, WaterBall().TileState));
            Assert.AreEqual(0, bystander.HitCount, "没落到地板上就不该有人受伤");

            // 开局初始状态：伤害的语义是「发生了转换」，站在泥浆上不该凭空掉血。
            var startCell = new Vector3Int(1, 0, 0);
            var starter = new ProbeTarget();
            grid.RegisterCell(startCell);
            starter.Position = grid.Geometry.CellCenter(startCell);
            registry.Register(startCell, starter);
            int applied = grid.LoadInitialStates(new List<TileInitial>
            {
                RowFactory.TileInitialRow(startCell.x, startCell.y, (int)TileStateType.Mud),
                RowFactory.TileInitialRow(99, 99, (int)TileStateType.Mud),   // 没有地板的格：忽略
            });
            Assert.AreEqual(1, applied, "只有落在合法格上的初始状态会被应用");
            Assert.AreEqual(0, starter.HitCount, "开局就站在泥浆上的敌人不该凭空掉血");
        }

        [Test]
        public void G5_重复落球不重入_到期落回常规_未注册的状态id不算转换()
        {
            var registry = new EnemyCellRegistry();
            var target = new ProbeTarget();
            var cell = new Vector3Int(1, 1, 0);
            GridLogic grid = GridWithTarget(cell, target, registry);
            ProjectileSpec ball = WaterBall();
            grid.OnBallHit(cell, ball.TileState);
            for (int i = 0; i < 4; i++) grid.Tick(i * 1f, 1f);      // 泥浆时长 8 秒：先推 4 秒

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell), "4 秒时泥浆还在");

            // 再砸一颗：状态没变 ⇒ 不重入、不刷新计时、也不再结算伤害。
            Assert.IsFalse(grid.OnBallHit(cell, ball.TileState), "已经是泥浆 ⇒ 第二次落地不算一次转换");
            Assert.AreEqual(1, target.HitCount, "伤害绑定在“状态真的变了”上：同一格连投不再重复结算");
            int submitsAtExpiry = target.SlowSubmitCount;
            for (int i = 4; i < 8; i++)
            {
                grid.Tick(i * 1f, 1f);
                submitsAtExpiry = target.SlowSubmitCount;
            }

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell), "总共 8 秒后必须落回常规 —— 重复落球若刷新了计时，这里还会是泥浆");
            Assert.AreEqual(0, grid.ActiveStateCount, "落回常规的格不该继续占着状态机");

            // 减速是"推"：不再续命就等于离开泥浆（修饰由目标自己过期，格子这边没有"摘"的动作）。
            for (int i = 8; i < 12; i++) grid.Tick(i, 1f);
            Assert.AreEqual(submitsAtExpiry, target.SlowSubmitCount, "落回常规之后不得再提交减速修饰");

            // 配置里没有这一行 ⇒ 不算一次转换（否则会凭空发一条事件 ＋ 一次冲击）。
            var unknownCell = new Vector3Int(0, 0, 0);
            var unknownTarget = new ProbeTarget();
            grid.RegisterCell(unknownCell);
            unknownTarget.Position = grid.Geometry.CellCenter(unknownCell);
            registry.Register(unknownCell, unknownTarget);
            Assert.IsFalse(grid.SwitchState(unknownCell, (TileStateType)99, applyEnterImpact: true));
            Assert.AreEqual(0, unknownTarget.HitCount, "没有转换就没有伤害");
            Assert.AreEqual(0, grid.ActiveStateCount, "也不该为它留下一个状态机");
        }

        // G6 · 归属表生命周期

        [Test]
        public void G6_归属表登记_移动_注销()
        {
            var registry = new EnemyCellRegistry();
            var target = new ProbeTarget();
            var a = new Vector3Int(0, 0, 0);
            var b = new Vector3Int(1, 0, 0);
            registry.Register(a, target);
            Assert.IsTrue(registry.TryGetIn(a, out List<IDamageable> list));
            Assert.AreEqual(1, list.Count);
            registry.Register(a, target);      // 幂等：同一格重复登记不该产生第二份
            Assert.AreEqual(1, list.Count, "同一格重复登记必须幂等");
            registry.Move(target, b);
            Assert.IsFalse(registry.TryGetIn(a, out _), "旧格清空后应当整条移除，而不是留一个空列表");
            Assert.IsTrue(registry.TryGetIn(b, out List<IDamageable> moved));
            Assert.AreEqual(1, moved.Count);
            registry.Unregister(target);
            Assert.AreEqual(0, registry.Count, "注销之后归属表里不该还留着它");
        }

        // G7 · 抛球轨迹与伤害结构（非数兜底）

        [Test]
        public void G7_抛物线两端贴地且顶点等于弧高_脚下目标与零伤害方向都不得是NaN()
        {
            ProjectileSpec spec = WaterBall();
            var origin = new Vector2(1f, 2f);
            var target = new Vector2(5f, 3.5f);
            var data = new ProjectileTrajectory(BallType.Water, origin, target, Vector2.Distance(origin, target), spec);
            Assert.AreEqual(0f, data.SampleVisual(0f).y - data.SampleGround(0f).y, 1e-4f, "出手瞬间视觉抬升必须是 0");
            Assert.AreEqual(0f, data.SampleVisual(1f).y - data.SampleGround(1f).y, 1e-4f, "落地瞬间视觉抬升必须是 0");
            float peak = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float t = i / 1000f;
                peak = Mathf.Max(peak, data.SampleVisual(t).y - data.SampleGround(t).y);
            }

            Assert.AreEqual(2f, peak, 1e-3f, "最高点应当恰好等于配置的弧高（错位不会报错，只会看着不对）");

            // 鼠标压在脚下：方向向量退化，落点必须被推到最小距离且不得是 NaN。
            var feet = new Vector2(-1.5f, 4f);
            for (int i = 0; i < 360; i++)
            {
                float radians = i * Mathf.Deg2Rad;
                var onFeet = feet + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 0.01f;
                var degenerate = new ProjectileTrajectory(BallType.Water, feet, onFeet, 0.01f, spec);
                float distance = Vector2.Distance(degenerate.Start, degenerate.End);
                Assert.GreaterOrEqual(distance, 0.4f - 1e-3f, $"角度 {i}° 的落点落到了脚下");
                Assert.IsFalse(float.IsNaN(degenerate.End.x) || float.IsNaN(degenerate.End.y), $"角度 {i}° 的落点是 NaN");
            }

            // 落点压在受害者身上：方向向量为零，必须兜底为 up（NaN 会让击退方向全乱）。
            Damage centered = Damage.At(new Vector2(3f, 3f), new Vector2(3f, 3f), 1f, DamageSource.Tile);
            Assert.AreEqual(1f, Damage.At(Vector2.zero, new Vector2(1f, 1f), 1f, DamageSource.Tile).Direction.magnitude, 1e-4f,
                "方向必须是单位向量");
            Assert.AreEqual(Vector2.up, centered.Direction, "零方向必须兜底为 up");
        }

        // G9/G10 · 敌人骨架：追击决策 ＋ 受击滑停（白模 W19 / W20 的回归）

        [Test]
        public void G9_追击决策的退化输入_骨架推进与无目标滑停()
        {
            Steering same = Steering.Resolve(new Vector2(2f, 2f), new Vector2(2f, 2f), 0.6f, 60f, 3.6f);
            Assert.IsTrue(same.IsIdle, "零向量归一化是 NaN：必须返回「不动」而不是硬塞方向");

            EnemySpec spec = EnemySpecFixture();
            var brain = new EnemyBrain(spec);
            EnemyIntent intent = brain.Decide(new EnemyBrain.Context(Vector2.zero, new Vector2(spec.StopDistance * 0.5f, 0f), true));
            Assert.AreEqual(0f, intent.Speed, 1e-4f, "进了停止距离就不该再给速度（否则会贴着玩家抖）");
            Assert.Greater(intent.Direction.sqrMagnitude, 0f, "方向不能丢：将来要「够近了也面向玩家」");

            var motor = new ProbeMotor();
            var logic = new EnemyLogic(motor, spec);
            logic.SetTarget(new Vector2(5f, 0f));
            logic.Tick(0f, 0.02f);
            Assert.AreEqual(MovementStateTag.Move, logic.MoveGroup.Current, "有目标且在追击范围内 ⇒ 移动层进追击态");
            Assert.Greater(logic.Brain.Intent.Direction.x, 0f, "意图方向应当指向玩家（＋x）");
            Assert.Greater(motor.EngineVelocity.x, 0f, "账本必须把意图落到执行器上");
            logic.SetTarget(null);
            for (int i = 0; i < 200; i++) logic.Tick(0.02f + i * 0.02f, 0.02f);
            Assert.AreEqual(0f, motor.EngineVelocity.magnitude, 1e-4f, "没有目标必须滑停到零");
            Assert.AreEqual(MovementStateTag.Idle, logic.MoveGroup.Current, "停住之后应当落在基础态（站立）");
        }

        [Test]
        public void G10_受击第一帧原样写出冲量之后按hurtDecay滑停()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            var logic = new EnemyLogic(motor, spec);
            logic.SetTarget(null);
            logic.ApplyKnockback(5.5f, Vector2.right);
            logic.Tick(0f, 0.02f);
            Assert.AreEqual(StatusStateTag.Hurt, logic.Status.Current, "击退必须由状态效果层的受击状态承载");
            Assert.AreEqual(5.5f, motor.EngineVelocity.magnitude, 1e-3f,
                "受击第一帧必须原样写出冲量（写成纯提前返回会让冲量永远进不了引擎）");
            Assert.Greater(spec.Config.hurtDecay, 0f, "前提：敌人配置给了受击减速度（否则这条测不出衰减）");
            for (int i = 0; i < 5; i++) logic.Tick(0.02f + i * 0.02f, 0.02f);
            Assert.Less(motor.EngineVelocity.magnitude, 5.5f, "受击期间速度必须按 hurtDecay 衰减（旧口径是「零提交、保持不变」）");
            Assert.Greater(motor.EngineVelocity.magnitude, 0f, "还没到零：不该一帧就停下");
        }

        [Test]
        public void G10_减速修饰的净化与稳态()
        {
            EnemySpec spec = EnemySpecFixture();

            // ① 系数净化：非数按"不起作用"，负数夹到 0（定住），都不许传染进速度。
            var motor = new ProbeMotor();
            var logic = new EnemyLogic(motor, spec);
            logic.SetTarget(new Vector2(5f, 0f));
            logic.Status.ApplySlow(float.NaN, 1f);
            logic.Tick(0f, 0.02f);
            Assert.IsFalse(float.IsNaN(motor.EngineVelocity.x), "非数减速系数不得传染进速度（否则角色会消失）");
            Assert.AreEqual(1f, logic.Status.SlowScale, 1e-4f, "非数按「不起作用」处理");
            motor.EngineVelocity = Vector2.zero;
            logic.Status.ApplySlow(-3f, 1f);
            logic.Tick(0.02f, 0.02f);
            Assert.AreEqual(0f, motor.EngineVelocity.magnitude, 1e-6f, "负数系数夹到 0（定住），而不是把速度反过来推");

            // ② 稳态：目标放在远处（全程追击），一直续命到跑稳 ⇒ 速度必须等于「配置速度 × 乘数」。
            var steadyMotor = new ProbeMotor();
            var steady = new EnemyLogic(steadyMotor, spec);
            steady.SetTarget(new Vector2(20f, 0f));
            steady.Status.ApplySlow(0.45f, 5f);
            float dt = 0.02f;
            for (int i = 0; i < 200; i++) steady.Tick(i * dt, dt);
            float expected = spec.MaxSpeed * 0.45f;
            Assert.AreEqual(expected, steadyMotor.EngineVelocity.magnitude, 1e-2f,
                $"减速稳态必须是「配置速度 × 乘数」= {expected}；每帧把整体速度乘一次会与加速度拉锯，稳态会远低于它");
            for (int i = 0; i < 400; i++) steady.Tick(5f + i * dt, dt);   // 不再续命 ⇒ 修饰过期

            Assert.AreEqual(spec.MaxSpeed, steadyMotor.EngineVelocity.magnitude, 1e-2f,
                "修饰过期后必须回到配置速度（「不再续命」就等于离开泥浆）");
        }

        // G12 · 玩家血量、资源与冷却

        [Test]
        public void G12_无敌帧NaN判据_血量钳零与重置_资源不扣负_冷却闭区间()
        {
            Assert.IsTrue(PlayerStats.CanTakeDamage(1f, float.NaN),
                "NaN 时必须照常结算：写成 now >= until 会让玩家永久无敌，而屏幕上什么都不会显示");
            Assert.IsFalse(PlayerStats.CanTakeDamage(1f, 2f), "无敌期未过时必须挡住");
            Assert.IsTrue(PlayerStats.CanTakeDamage(2f, 2f), "无敌到期那一帧必须可以受伤");
            var health = new PlayerStats(PlayerSpecFixture());
            health.ApplyDamage(1000f, 0f);
            Assert.AreEqual(0f, health.Current, 1e-4f, "血量不该是负数");
            health.ResetToFull();
            Assert.AreEqual(100f, health.Current, 1e-4f);
            Assert.IsFalse(health.IsInvulnerable(0f), "重置必须把无敌期一起清掉（否则重生后打不动）");
            Assert.IsFalse(health.TryConsumeWater(1), "没有资源时消耗必须失败");
            health.AddWaterBall(2);
            Assert.AreEqual(2, health.WaterBallCount);
            Assert.IsFalse(health.TryConsumeWater(3), "不够就是不够，不允许扣成负数");
            Assert.AreEqual(2, health.WaterBallCount, "失败时数量必须原样");
            var cooldown = new Cooldown();
            Assert.IsTrue(cooldown.CanUse(0f), "开局即可用");
            cooldown.MarkUsed(0f, 0.5f);
            Assert.IsFalse(cooldown.CanUse(0.49f));
            Assert.IsTrue(cooldown.CanUse(0.5f), "间隔是闭区间：到点即可用（差一帧就变成『按了没反应』）");
            cooldown.Reset();
            Assert.IsTrue(cooldown.CanUse(0.5f));
        }

        // G13 · 波次计时

        [Test]
        public void G13_开局延时后一只一只出且每波只出配置的只数()
        {
            WaveSpec spec = WaveSpecFixture();
            var logic = new WaveLogic(spec);
            var output = new List<WaveLogic.SpawnRequest>();
            float now = 0f;
            float dt = 0.05f;
            int spawned = 0;

            // 【为什么判据是时间窗而不是"恰好第 30 帧"】计时是 `_timer -= dt` 的浮点累减，
            // 1.5f 连减 30 次 0.05f 会剩下 +3e-7 ⇒ 第一只落在第 31 帧。强断言一条都没放松：
            // "一帧只出一只"与"恰好出配置的只数"都在下面，另有"不得早于开局延时"兜底。
            for (int i = 0; i < 2000 && spawned < spec.EnemiesPerWave; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);
                Assert.LessOrEqual(output.Count, 1, $"一次刷一堆：第 {i} 帧出了 {output.Count} 只");
                if (output.Count > 0) Assert.AreEqual(1, output[0].WaveIndex, "第一波的序号是 1（不是 0）");
                spawned += output.Count;
                now += dt;
            }

            Assert.AreEqual(spec.EnemiesPerWave, spawned, "一波必须恰好出配置的只数");
            Assert.GreaterOrEqual(now, spec.InitialDelay, "第一只不得早于开局延时");

            // 场上还有敌人 ⇒ 不该开下一波（10 秒 > 清场等待 2.5s，足以暴露"不看场上就开下一波"）。
            for (int i = 0; i < 200; i++)
            {
                logic.Tick(now, dt, true, Vector2.zero, output);
                Assert.AreEqual(0, output.Count, "场上还有敌人时不该开下一波");
                now += dt;
            }

            Assert.AreEqual(1, logic.WaveIndex, "波次序号不该在清场前推进");
        }

        [Test]
        public void G13_清场后隔一段再开下一波()
        {
            WaveSpec spec = WaveSpecFixture();
            var logic = new WaveLogic(spec);
            var output = new List<WaveLogic.SpawnRequest>();
            float now = 0f;
            float dt = 0.05f;
            int spawned = 0;
            while (spawned < spec.EnemiesPerWave)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);
                spawned += output.Count;
                now += dt;
            }

            // 清场：等待期内的每一帧都不该有敌人。用**固定次数**循环，不用浮点累加当条件 ——
            // "还差一帧"这种边界由计数决定，不由 2.45 与 2.4500001 谁大决定。
            int waitingTicks = Mathf.RoundToInt((spec.RespawnDelay - dt) / dt);
            for (int i = 0; i < waitingTicks; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);
                Assert.AreEqual(0, output.Count, $"清场后第 {i + 1} 帧就开下一波，没等满 {spec.RespawnDelay}s");
                now += dt;
            }

            for (int i = 0; i < 10; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);
                if (output.Count > 0) break;
                now += dt;
            }

            Assert.Greater(output.Count, 0, "等待时间到点后必须开下一波");
            Assert.AreEqual(2, logic.WaveIndex, "第二波的序号是 2");
        }

        // G14 · 随机（留着它还有一个硬理由：`Rng.SetImpl` / `Rng.Reset` 是给测试与回放用的
        // 门面钩子，本用例是它们**唯一**的消费者 —— 删了就会造出两个零消费者公开成员）

        [Test]
        public void G14_固定种子必须可复现()
        {
            Rng.SetImpl(new DefaultRng(12345));
            float a = Rng.Value01();
            Vector2 b = Rng.InsideUnitCircle();
            Rng.SetImpl(new DefaultRng(12345));
            Assert.AreEqual(a, Rng.Value01(), 1e-6f, "同一个种子必须给出同一个序列（否则缺陷无法复现）");

            Vector2 again = Rng.InsideUnitCircle();
            Assert.AreEqual(b, again, "同一个种子必须给出同一个序列");

            Rng.Reset();
        }

        // G16 · 接触判定与玩家受击

        [Test]
        public void G16_接触判定扫描邻格_取最近_半径外与已死不算接触()
        {
            var registry = new EnemyCellRegistry();
            var buffer = new List<Vector3Int>();
            var playerPosition = new Vector2(0.5f, 0.5f);

            // ① 只有邻格里有目标（距离 0.9）：只看玩家自己那一格的实现会漏掉它
            var neighbour = new ProbeTarget { Position = new Vector2(1.4f, 0.5f) };
            registry.Register(new Vector3Int(1, 0, 0), neighbour);
            Assert.IsTrue(
                ContactProbe.TryFindAttacker(Vector3Int.zero, playerPosition, 1f, registry, buffer, out Vector2 first, out float firstDistance),
                "接触判定必须扫描邻格：玩家站在格里哪个位置都有可能");
            Assert.AreEqual(0.9f, firstDistance, 1e-3f);

            // ② 本格里再放一个更近的：必须取最近的那个
            var inCell = new ProbeTarget { Position = new Vector2(0.8f, 0.5f) };
            registry.Register(new Vector3Int(0, 0, 0), inCell);
            Assert.IsTrue(
                ContactProbe.TryFindAttacker(Vector3Int.zero, playerPosition, 1f, registry, buffer, out Vector2 second, out _),
                "两格都有目标时照样能判定");
            Assert.AreEqual(inCell.Position, second, "有多个接触者时必须取最近的那个");

            // ③ 半径之外（邻格、会被扫到，但距离 1.1 > 半径 1）不算接触
            registry.Unregister(neighbour);
            registry.Unregister(inCell);
            var tooFar = new ProbeTarget { Position = new Vector2(1.6f, 0.5f) };
            registry.Register(new Vector3Int(1, 0, 0), tooFar);
            Assert.IsFalse(ContactProbe.TryFindAttacker(Vector3Int.zero, playerPosition, 1f, registry, buffer, out _, out _),
                "半径之外不算接触");

            // ④ 已死目标不算接触（表里可能还留着尸体）；没有归属表时必须安静地返回 false
            registry.Unregister(tooFar);
            var dead = new ProbeTarget { Position = new Vector2(0.6f, 0.5f), IsAlive = false };
            registry.Register(new Vector3Int(0, 0, 0), dead);
            Assert.IsFalse(ContactProbe.TryFindAttacker(Vector3Int.zero, playerPosition, 1f, registry, buffer, out _, out _),
                "已死目标不算接触（表里可能还留着尸体）");
            Assert.IsFalse(ContactProbe.TryFindAttacker(Vector3Int.zero, playerPosition, 1f, null, buffer, out _, out _),
                "没有归属表时必须安静地返回 false，不抛");
        }

        [Test]
        public void G16_受击扣血并落地击退_无敌期内整条挡掉_滑停后交还控制()
        {
            // CharacterConfig.moveAcceleration 的声明默认值，也是 PlayerConfig.asset 里的值。
            // 只用于算断言里的期望值；真值仍由 PlayerLogic 从取值边界折成快照后使用。
            const float PlayerAcceleration = 60f;

            PlayerSpec spec = PlayerSpecFixture();
            var motor = new ProbeMotor { Position = Vector2.zero };
            var buffer = new InputBuffer(0.12f, 50);
            var logic = new PlayerLogic(motor, spec, buffer);

            // 世界侧在玩家那一帧之后递交（与 CombatRoot 的实际顺序一致）
            var damage = new Damage(Vector2.zero, spec.ContactDamage, DamageSource.Contact, Vector2.right, spec.KnockbackImpulse);
            Assert.IsTrue(logic.TakeDamage(in damage, 0f), "第一次接触必须生效");
            Assert.AreEqual(90f, logic.Stats.Current, 1e-4f);
            TickPlayer(logic, motor, Vector2.zero, 0.02f);
            Assert.AreEqual(spec.KnockbackImpulse, motor.EngineVelocity.x, 1e-3f,
                "击退必须在下一帧被写进执行器（旧实现把它在帧首清掉了，表现是'被撞了纹丝不动'）");
            Assert.AreEqual(StatusStateTag.Hurt, logic.Status.Current, "击退由状态效果层的受击状态承载");

            // 受击期间按 moveAcceleration 衰减（不是"只接管一帧"，也不是当帧归零）。
            // 帧数预算见下面第二次受击处的注释：12 ÷ 60 = 0.2 秒，而进入受击的那一帧不衰减
            // （HurtState._justEntered）⇒ 1 + 10 = 11 帧到零，这里留到 14 帧做余量。
            TickPlayer(logic, motor, Vector2.zero, 0.04f);
            Assert.AreEqual(spec.KnockbackImpulse - PlayerAcceleration * 0.02f, motor.EngineVelocity.x, 1e-3f,
                $"受击期间速度每帧按 moveAcceleration 衰减（12 − {PlayerAcceleration}×0.02 = 10.8）");
            for (int i = 0; i < 12; i++) TickPlayer(logic, motor, Vector2.zero, 0.06f + i * 0.02f);
            Assert.AreEqual(StatusStateTag.Normal, logic.Status.Current, "滑停到零之后必须交还控制权");

            // 无敌期内（0.8s 内）再来一次：不扣血、也不留下任何新的击退
            Assert.IsFalse(logic.TakeDamage(in damage, 0.5f), "无敌期内必须整条挡掉");
            Assert.AreEqual(90f, logic.Stats.Current, 1e-4f, "被挡住时不该扣血");
            TickPlayer(logic, motor, Vector2.zero, 0.52f);
            Assert.AreEqual(0f, motor.EngineVelocity.x, 1e-3f, "被挡住的那一次不许留下击退");
            Assert.AreEqual(StatusStateTag.Normal, logic.Status.Current, "被挡住时也不该进入受击状态");

            // 无敌到期后再挨一次：**这一次真的会击退**，所以必须等它滑停完再测"交还控制"。
            // （这里曾直接断言"输入立刻生效"，而那一帧玩家正处在受击状态里 —— 门禁用强制速度覆盖输入，
            //   于是断言恒假。不是实现的问题：受击期间本来就该由外力接管。）
            Assert.IsTrue(logic.TakeDamage(in damage, 0.8f), "无敌到期后必须能再扣");
            Assert.AreEqual(80f, logic.Stats.Current, 1e-4f);

            // 帧数预算：进入受击的那一帧不衰减（HurtState._justEntered），之后每帧只掉
            // moveAcceleration×Δt = 60×0.02 = 1.2 ⇒ 12 需要 1 + 10 = 11 帧才到零。
            // 这里给 14 帧，**与上面第一次受击的预算一致**（那里是 1 + 1 + 12 = 14 帧才过）：
            // 两处预算取同一个数，就不必再猜"边界是不是差一帧"；多出来的几帧顺手钉住
            // "到零之后不会自己再动"。**改小之前先确认第一次受击那段也一起改。**
            for (int i = 0; i < 14; i++) TickPlayer(logic, motor, Vector2.zero, 0.82f + i * 0.02f);
            Assert.AreEqual(StatusStateTag.Normal, logic.Status.Current, "第二次受击也必须滑停到零");

            // 交还之后输入立刻生效（本用例的加速度是 60，一帧足够走 1.2，故断言"在往左加速"）
            TickPlayer(logic, motor, Vector2.left, 1.2f);
            Assert.Less(motor.EngineVelocity.x, 0f, "受击结束后玩家必须能重新控制移动（否则被打一次就废了）");
        }

        [Test]
        public void G16_重生把血量恢复满并清掉残留速度()
        {
            PlayerSpec spec = PlayerSpecFixture();
            var motor = new ProbeMotor { Position = Vector2.zero };
            var buffer = new InputBuffer(0.12f, 50);
            var logic = new PlayerLogic(motor, spec, buffer);
            var lethal = new Damage(Vector2.zero, 1000f, DamageSource.Contact, Vector2.right, spec.KnockbackImpulse);
            Assert.IsTrue(logic.TakeDamage(in lethal, 0f));
            Assert.IsFalse(logic.IsAlive);

            // 模拟"打空那一帧已经被撞飞的引擎速度"：重生不能带着它继续滑（实测能滑出一点几个单位）。
            motor.EngineVelocity = new Vector2(spec.KnockbackImpulse, 0f);
            logic.RespawnTo(new Vector2(3f, 4f));
            Assert.IsTrue(logic.IsAlive, "重生必须满血复活");
            Assert.AreEqual(100f, logic.Stats.Current, 1e-4f);
            Assert.AreEqual(Vector2.zero, motor.EngineVelocity, "重生必须当场清掉残留在物理体上的速度");
        }

        // G17 · 瞄准事实 / 投掷裁决

        [Test]
        public void G17_瞄准事实只在真的变了时发布_没瞄到格就不提交投掷意图()
        {
            var motor = new ProbeMotor { Position = new Vector2(0.5f, 0.5f) };
            var buffer = new InputBuffer(0.12f, 50);
            var sink = new ProbeThrowSink();
            PlayerLogic logic = NewAimingPlayer(motor, buffer, PlayerSpecFixture(), sink, out AimRecorder recorder);
            logic.Stats.AddWaterBall(1);
            logic.UpdateAim(new Vector2(2.5f, 0.5f), 0f);
            Assert.AreEqual(1, recorder.Count, "第一次拿到瞄准必须发布一条事实");
            Assert.AreEqual(new Vector3Int(2, 0, 0), recorder.Last.Cell);
            Assert.IsTrue(recorder.Last.Available, "射程内 ＋ 冷却就绪 ＋ 有水球");

            // 同一个格再算一次：不发（瞄准是每帧算的，事实只在变化时发，否则每帧一条事件）
            logic.UpdateAim(new Vector2(2.6f, 0.5f), 0.02f);
            Assert.AreEqual(1, recorder.Count, "同一格不得重复发布");
            logic.ClearAim();
            Assert.AreEqual(2, recorder.Count);
            Assert.IsFalse(recorder.Last.HasAim, "暂停 / 没鼠标时必须发布'没有瞄准'，高亮才会收起来");

            // 鼠标压在脚下（零方向）：TileAim 的契约是"拿不到格" ⇒ 不受理投掷，也不扣弹药
            logic.UpdateAim(motor.Position, 0.04f);
            Assert.IsFalse(logic.Combat.HasAim);
            Assert.IsFalse(logic.RequestThrow(BallType.Water, 0.04f), "没瞄到格就什么都不做");
            Assert.AreEqual(0, sink.RequestCount, "不该把无效意图递给世界侧");
            Assert.AreEqual(1, logic.Stats.WaterBallCount, "更不该扣弹药");
            EventBus<AimChanged>.Unsubscribe(recorder.Handle);
        }

        [Test]
        public void G17_裁决在世界侧_拒绝不扣弹药不吃冷却_采纳才扣并进冷却()
        {
            var motor = new ProbeMotor { Position = new Vector2(0.5f, 0.5f) };
            var buffer = new InputBuffer(0.12f, 50);
            PlayerSpec spec = PlayerSpecFixture();
            var sink = new ProbeThrowSink { Accept = false };
            PlayerLogic logic = NewAimingPlayer(motor, buffer, spec, sink, out AimRecorder recorder);
            logic.Stats.AddWaterBall(2);
            logic.UpdateAim(new Vector2(2.5f, 0.5f), 0f);
            Assert.IsFalse(logic.RequestThrow(BallType.Water, 0f), "被拒绝时返回 false");
            Assert.AreEqual(2, logic.Stats.WaterBallCount, "没被采纳就不该扣弹药");
            Assert.AreEqual(1, sink.RequestCount, "但意图确实递过去了（裁决在世界侧）");
            sink.Accept = true;
            Assert.IsTrue(logic.RequestThrow(BallType.Water, 0.01f), "被拒绝不进冷却：下一帧就能再投");
            Assert.AreEqual(1, logic.Stats.WaterBallCount, "采纳后才扣");
            Assert.IsFalse(logic.RequestThrow(BallType.Water, 0.1f), "冷却内不得再投");
            Assert.AreEqual(2, sink.RequestCount, "被冷却挡下时不该去打扰世界侧");
            Assert.IsTrue(logic.RequestThrow(BallType.Water, spec.AttackInterval + 0.01f), "冷却到点即可再投");
            Assert.AreEqual(0, logic.Stats.WaterBallCount);

            // 没水球了：水球投不出去，土球照样能投（副攻击不吃弹药）
            Assert.IsFalse(logic.RequestThrow(BallType.Water, spec.AttackInterval * 3f), "没弹药投不出去");
            Assert.IsTrue(logic.RequestThrow(BallType.Earth, spec.AttackInterval * 3f), "土球不吃弹药");

            // 意图里的落点必须是瞄准格的几何中心（与吸附共用同一份几何）
            Assert.AreEqual(new Vector3Int(2, 0, 0), sink.LastIntent.Cell);
            Assert.AreEqual(new Vector2(2.5f, 0.5f), sink.LastIntent.Target);
            EventBus<AimChanged>.Unsubscribe(recorder.Handle);
        }
    }
}

