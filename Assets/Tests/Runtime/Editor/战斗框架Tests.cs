// ---------------------------------------------------------------------------
// 白模迁移 · 战斗框架 EditMode 测试
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 与 移动Tests.cs / Data层Tests.cs 同机制：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【覆盖边界，写在明处】只测**纯逻辑**：几何 / 吸附 / 状态机 / 队列 / 结算 / 波次 / 血量 / 随机。
//   不测：MonoBehaviour 生命周期、Tilemap、Physics2D、EffectModule、UIMgr、鼠标设备 ——
//   那些只能人工 Play 手测（见交付说明里的验收清单）。
//   写假测试会把真绿变成"全绿但缺陷仍在"，移动Tests.cs 头部已记过这个教训。
//
// 【为什么不用 ConfigModule】所有被测类型都收**结构体**而不是自己去读表，
//   所以这里不需要初始化配表，也就不受"表里改了数就红一片"的影响。
//   表值 → 结构体的折算由 SpecCatalog 负责，它需要真配表，属于运行期验收。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Logic.Random;
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
        // ================================================================
        // 夹具
        // ================================================================

        /// <summary>格边长 1、角点在原点的标准几何。</summary>
        private static GridGeometry Geometry()
        {
            return new GridGeometry(Vector2.zero, 1f);
        }

        /// <summary>一颗"水球"：落地把目标格切成泥浆。射程 5、时长 0.6、弧高 2、最小距离 0.4。</summary>
        private static BallSpec WaterBall()
        {
            return new BallSpec(
                BallType.Water,
                "水球",
                new ThrowSpec(0.6f, 2f, 5f, 0.4f),
                TileStateType.Mud);
        }

        /// <summary>一颗"土球"：落地不改格子。</summary>
        private static BallSpec EarthBall()
        {
            return new BallSpec(
                BallType.Earth,
                "土球",
                new ThrowSpec(0.6f, 2f, 5f, 0.4f),
                TileStateType.Normal);
        }

        private static TileStateSpec MudSpec(float slow = 0.45f, float duration = 8f, float damage = 1f, float knockback = 1.83f)
        {
            return new TileStateSpec(TileStateType.Mud, "泥浆", slow, duration, damage, knockback);
        }

        private static TileStateSpec NormalSpec()
        {
            return new TileStateSpec(TileStateType.Normal, "常规", 1f, 0f, 0f, 0f);
        }

        private static GridLogic NewGrid(EnemyCellRegistry registry = null)
        {
            var list = new List<TileStateSpec> { NormalSpec(), MudSpec() };

            return new GridLogic(
                Geometry(),
                list,
                id => id == TileStateType.Mud ? new MudTileState(MudSpec()) : null,
                registry);
        }

        /// <summary>被结算的假目标：只记事实，不碰引擎。</summary>
        private sealed class ProbeTarget : IDamageable
        {
            public int HitCount;
            public float LastAmount;
            public float LastImpulse;
            public Vector2 LastDirection;

            public bool IsDead { get; set; }

            public Vector2 Position { get; set; }

            public void TakeDamage(in Damage damage)
            {
                HitCount++;
                LastAmount = damage.Amount;
                LastImpulse = damage.Impulse;
                LastDirection = damage.Direction;
            }
        }

        /// <summary>
        /// 不碰引擎的移动执行器探针：速度只存在一个字段里。
        /// </summary>
        /// <remarks>
        /// 逻辑层只认识 <see cref="IMovementMotor"/>，所以这里能塞一个纯 C# 实现 ——
        /// 这正是"逻辑层零引擎类型"的收益。
        /// </remarks>
        private sealed class ProbeMotor : IMovementMotor
        {
            public Vector2 Velocity { get; set; }

            public Vector2 Position { get; set; }

            public Vector2 Facing { get; set; } = Vector2.right;

            public void Move(Vector2 velocity) => Velocity = velocity;

            public void SetPosition(Vector2 position) => Position = position;
        }

        // ================================================================
        // G1 · 格子几何
        // ================================================================

        [Test]
        public void G1_世界坐标与格子坐标互为可逆()
        {
            GridGeometry geo = Geometry();

            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    Vector2 center = geo.CellCenter(cell);

                    Assert.AreEqual(cell, geo.WorldToCell(center),
                        $"格 {cell} 的中心 {center} 换算回来应当是它自己");
                }
            }
        }

        [Test]
        public void G1_负坐标必须向下取整()
        {
            GridGeometry geo = Geometry();

            // -0.5 落在格 (-1) 里，而不是格 0。用截断取整会让 x=0 左侧那一格与 x=0 重叠。
            Assert.AreEqual(new Vector3Int(-1, -1, 0), geo.WorldToCell(new Vector2(-0.5f, -0.5f)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), geo.WorldToCell(new Vector2(0.0f, 0.0f)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), geo.WorldToCell(new Vector2(0.99f, 0.99f)));
        }

        [Test]
        public void G1_几何非法时必须安全降级()
        {
            var geo = new GridGeometry(Vector2.zero, 0f);

            Assert.IsFalse(geo.IsValid, "格边长为 0 时几何必须判为非法");
            Assert.AreEqual(Vector3Int.zero, geo.WorldToCell(new Vector2(5f, 5f)));
            Assert.AreEqual(Vector2.zero, geo.CellCenter(new Vector3Int(5, 5, 0)));

            var nan = new GridGeometry(Vector2.zero, float.NaN);
            Assert.IsFalse(nan.IsValid, "NaN 格边长必须判为非法（否则换算全是 NaN）");
        }

        // ================================================================
        // G2 · 瞄准吸附
        // ================================================================

        [Test]
        public void G2_射程内吸附到鼠标所在格()
        {
            GridGeometry geo = Geometry();
            var origin = new Vector2(0.5f, 0.5f);      // 格 (0,0) 的中心

            bool ok = TileAim.TryGetAimCell(in geo, origin, new Vector2(2.2f, 0.5f), 5f, out Vector3Int cell);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Vector3Int(2, 0, 0), cell, "射手在格 (0,0) 中心时，鼠标 x=2.2 应当落在格 (2,0)");
        }

        [Test]
        public void G2_超距时落点必须落在射程内()
        {
            GridGeometry geo = Geometry();
            var origin = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 72; i++)
            {
                float radians = i * 5f * Mathf.Deg2Rad;
                var mouse = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 500f;

                bool ok = TileAim.TryGetAimCell(in geo, origin, mouse, 5f, out Vector3Int cell);

                Assert.IsTrue(ok, $"角度 {i * 5}° 时应当能取到格子");

                float distance = Vector2.Distance(origin, geo.CellCenter(cell));

                // 允许半格误差：格子是离散的，"最近的可及格"中心未必精确落在射程边界上。
                Assert.LessOrEqual(distance, 5f + 0.75f,
                    $"角度 {i * 5}° 的落点离射手 {distance}，明显超出射程 5");
            }
        }

        [Test]
        public void G2_鼠标压在脚下时必须返回false()
        {
            GridGeometry geo = Geometry();
            var origin = new Vector2(0.5f, 0.5f);

            // 方向向量为零 ⇒ 归一化会产生 NaN ⇒ 必须返回 false 而不是硬塞一个方向。
            Assert.IsFalse(TileAim.TryGetAimCell(in geo, origin, origin, 5f, out _));

            var badGeo = new GridGeometry(Vector2.zero, 0f);
            Assert.IsFalse(TileAim.TryGetAimCell(in badGeo, origin, new Vector2(3f, 3f), 5f, out _),
                "几何非法时必须返回 false");
        }

        [Test]
        public void G2_非法射程不得传染非数()
        {
            GridGeometry geo = Geometry();
            var origin = new Vector2(0.5f, 0.5f);

            foreach (float reach in new[] { float.NaN, float.PositiveInfinity, 0f, -3f })
            {
                bool ok = TileAim.TryGetAimCell(in geo, origin, new Vector2(3f, 3f), reach, out Vector3Int cell);

                Assert.IsTrue(ok, $"射程 {reach} 应当被当成「不限」而不是拒绝");

                Vector2 center = geo.CellCenter(cell);

                Assert.IsFalse(float.IsNaN(center.x) || float.IsNaN(center.y), $"射程 {reach} 时格心是 NaN");
            }
        }

        // ================================================================
        // G3 · 空间查询
        // ================================================================

        [Test]
        public void G3_邻居查询不含自己()
        {
            var buffer = new List<Vector3Int>();
            var cell = new Vector3Int(2, 3, 0);

            GridQuery.GetNeighbors4(cell, buffer);

            Assert.AreEqual(4, buffer.Count);
            Assert.IsFalse(buffer.Contains(cell), "四邻不该包含自己");

            GridQuery.GetNeighbors8(cell, buffer);

            Assert.AreEqual(8, buffer.Count);
            Assert.IsFalse(buffer.Contains(cell), "八邻不该包含自己");
        }

        [Test]
        public void G3_范围查询按格心判距()
        {
            GridGeometry geo = Geometry();
            var buffer = new List<Vector3Int>();
            var center = new Vector3Int(0, 0, 0);

            // 半径 0：只有中心格。
            GridQuery.GetWithinRadius(in geo, center, 0f, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(center, buffer[0]);

            // 半径 1：中心 + 4 邻 = 5（对角格心距 √2 > 1，不该进来）。
            GridQuery.GetWithinRadius(in geo, center, 1f, buffer);

            Assert.AreEqual(5, buffer.Count, "半径 1 应当覆盖中心与四邻，不含对角");

            // 半径 1.5：四角进来了（√2 ≈ 1.414 ≤ 1.5）。
            GridQuery.GetWithinRadius(in geo, center, 1.5f, buffer);

            Assert.AreEqual(9, buffer.Count, "半径 1.5 应当覆盖 3×3 全部格子");
        }

        // ================================================================
        // G4 · Tick 队列
        // ================================================================

        [Test]
        public void G4_本帧提交的请求下一帧才处理()
        {
            var queue = new TileTickQueue();
            var cell = new Vector3Int(1, 1, 0);

            queue.Schedule(cell);

            Assert.AreEqual(0, queue.Count, "刚提交的请求不该在本帧就可见（否则会变成同帧递归）");

            queue.Swap();

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(cell, queue.Current[0]);
        }

        [Test]
        public void G4_一次性消费且同帧去重()
        {
            var queue = new TileTickQueue();
            var cell = new Vector3Int(1, 1, 0);

            queue.Schedule(cell);
            queue.Schedule(cell);       // 同帧重复提交
            queue.Swap();

            Assert.AreEqual(1, queue.Count, "同一格在一帧里提交多次只算一次");

            queue.Swap();

            Assert.AreEqual(0, queue.Count, "请求是一次性的：不重新提交就不会再被处理");
        }

        // ================================================================
        // G5 · 格子状态机与结算
        // ================================================================

        [Test]
        public void G5_水球落地把目标格切成泥浆并结算一次伤害()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(2, 2, 0);
            grid.RegisterCell(cell);

            var target = new ProbeTarget { Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            BallSpec ball = WaterBall();

            Assert.IsTrue(grid.OnBallHit(cell, in ball), "落在合法格上必须生效");

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell), "状态应当切成泥浆");
            Assert.AreEqual(1, target.HitCount, "状态转换应当结算一次伤害");
            Assert.AreEqual(1f, target.LastAmount, 1e-4f);
            Assert.Less(grid.GetSlowMultiplierAt(cell), 1f, "泥浆格必须减速");
        }

        [Test]
        public void G5_落在没有地板的格上不产生任何效果()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(9, 9, 0);   // 没有 RegisterCell

            var target = new ProbeTarget { Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            BallSpec ball = WaterBall();

            Assert.IsFalse(grid.OnBallHit(cell, in ball));
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell));
            Assert.AreEqual(0, target.HitCount, "没落到地板上就不该有人受伤");
        }

        [Test]
        public void G5_土球不改格子也不伤害()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(1, 1, 0);
            grid.RegisterCell(cell);

            var target = new ProbeTarget { Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            BallSpec ball = EarthBall();

            grid.OnBallHit(cell, in ball);

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell), "土球的 tile_state 是常规 ⇒ 不该改格子");
            Assert.AreEqual(0, target.HitCount, "状态没变就不该结算伤害");
        }

        [Test]
        public void G5_同一格重复落球伤害照给但计时不重置()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(1, 1, 0);
            grid.RegisterCell(cell);

            var target = new ProbeTarget { Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            BallSpec ball = WaterBall();

            grid.OnBallHit(cell, in ball);

            // 泥浆时长 8 秒：先推 4 秒。
            for (int i = 0; i < 4; i++) grid.Tick(i * 1f, 1f);

            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell), "4 秒时泥浆还在");

            // 再砸一颗：伤害照给（白模的节奏），但计时不该被刷新。
            grid.OnBallHit(cell, in ball);

            Assert.AreEqual(2, target.HitCount, "重复落球必须再结算一次伤害（白模的伤害节奏）");

            for (int i = 0; i < 4; i++) grid.Tick(4f + i * 1f, 1f);

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell),
                "总共 8 秒后必须落回常规 —— 若重复落球刷新了计时，这里还会是泥浆");
        }

        [Test]
        public void G5_泥浆到期后落回常规并恢复速度()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(0, 0, 0);
            grid.RegisterCell(cell);

            BallSpec ball = WaterBall();

            grid.OnBallHit(cell, in ball);

            Assert.AreEqual(1, grid.ActiveStateCount);

            for (int i = 0; i < 8; i++) grid.Tick(i, 1f);

            Assert.AreEqual(TileStateType.Normal, grid.StateOf(cell));
            Assert.AreEqual(1f, grid.GetSlowMultiplierAt(cell), 1e-4f, "状态结束后必须恢复 1");
            Assert.AreEqual(0, grid.ActiveStateCount, "落回常规的格不该继续占着状态机");
        }

        [Test]
        public void G5_伤害方向从格心指向受害者()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(0, 0, 0);
            grid.RegisterCell(cell);

            Vector2 center = grid.Geometry.CellCenter(cell);

            var target = new ProbeTarget { Position = center + new Vector2(0.3f, 0f) };
            registry.Register(cell, target);

            BallSpec ball = WaterBall();
            grid.OnBallHit(cell, in ball);

            Assert.AreEqual(1f, target.LastDirection.x, 1e-3f);
            Assert.AreEqual(0f, target.LastDirection.y, 1e-3f);
            Assert.AreEqual(1.83f, target.LastImpulse, 1e-3f, "击退冲量来自状态配置行");
        }

        [Test]
        public void G5_已死目标不再被结算()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(0, 0, 0);
            grid.RegisterCell(cell);

            var target = new ProbeTarget { IsDead = true, Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            BallSpec ball = WaterBall();
            grid.OnBallHit(cell, in ball);

            Assert.AreEqual(0, target.HitCount, "已死目标不该再吃一次伤害");
        }

        [Test]
        public void G5_初始状态不产生伤害()
        {
            var registry = new EnemyCellRegistry();
            GridLogic grid = NewGrid(registry);

            var cell = new Vector3Int(1, 0, 0);
            grid.RegisterCell(cell);

            var target = new ProbeTarget { Position = grid.Geometry.CellCenter(cell) };
            registry.Register(cell, target);

            var initials = new List<TileInitialSpec>
            {
                new TileInitialSpec(cell.x, cell.y, TileStateType.Mud),
                new TileInitialSpec(99, 99, TileStateType.Mud),   // 没有地板的格：忽略
            };

            int applied = grid.LoadInitialStates(initials);

            Assert.AreEqual(1, applied, "只有落在合法格上的初始状态会被应用");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(cell));
            Assert.AreEqual(0, target.HitCount,
                "开局就站在泥浆上的敌人不该凭空掉血 —— 伤害的语义是「发生了转换」");
        }

        // ================================================================
        // G6 · 归属表
        // ================================================================

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

            Assert.IsFalse(registry.TryGetIn(b, out _));
            Assert.AreEqual(0, registry.Count);
        }

        // ================================================================
        // G7 · 抛球数据
        // ================================================================

        [Test]
        public void G7_抛物线两端精确贴地且顶点等于弧高()
        {
            var spec = new ThrowSpec(0.6f, 2f, 5f, 0.4f);
            var origin = new Vector2(1f, 2f);
            var target = new Vector2(5f, 3.5f);

            var data = new BallData(BallType.Water, origin, target, Vector2.Distance(origin, target), in spec);

            Assert.AreEqual(0f, data.SampleVisual(0f).y - data.SampleGround(0f).y, 1e-4f, "出手瞬间视觉抬升必须是 0");
            Assert.AreEqual(0f, data.SampleVisual(1f).y - data.SampleGround(1f).y, 1e-4f, "落地瞬间视觉抬升必须是 0");

            float peak = 0f;

            for (int i = 0; i <= 1000; i++)
            {
                float t = i / 1000f;
                peak = Mathf.Max(peak, data.SampleVisual(t).y - data.SampleGround(t).y);
            }

            Assert.AreEqual(2f, peak, 1e-3f, "最高点应当恰好等于配置的弧高");
        }

        [Test]
        public void G7_时长按距离缩放且最远一投等于配置时长()
        {
            var spec = new ThrowSpec(0.6f, 2f, 5f, 0.4f);
            var origin = Vector2.zero;

            var near = new BallData(BallType.Water, origin, new Vector2(2.5f, 0f), 2.5f, in spec);
            var far = new BallData(BallType.Water, origin, new Vector2(5f, 0f), 5f, in spec);

            Assert.AreEqual(2f, far.Duration / near.Duration, 1e-3f, "距离翻倍 ⇒ 时长翻倍（同一飞行速度）");
            Assert.AreEqual(0.6f, far.Duration, 1e-3f, "最远一投的时长就是配置的飞行时长");
        }

        [Test]
        public void G7_鼠标压在脚下不得产生非数坐标()
        {
            var spec = new ThrowSpec(0.6f, 2f, 5f, 0.4f);
            var origin = new Vector2(-1.5f, 4f);

            for (int i = 0; i < 360; i++)
            {
                float radians = i * Mathf.Deg2Rad;
                var target = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 0.01f;

                var data = new BallData(BallType.Water, origin, target, 0.01f, in spec);

                float distance = Vector2.Distance(data.Start, data.End);

                Assert.GreaterOrEqual(distance, 0.4f - 1e-3f, $"角度 {i}° 的落点落到了脚下");
                Assert.IsFalse(float.IsNaN(data.End.x) || float.IsNaN(data.End.y), $"角度 {i}° 的落点是 NaN");
            }
        }

        [Test]
        public void G7_非法投掷参数必须被换成默认值()
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
            {
                var spec = new ThrowSpec(bad, bad, bad, bad);

                Assert.IsFalse(float.IsNaN(spec.FlightDuration) || spec.FlightDuration <= 0f,
                    $"非法时长 {bad} 之后仍然是合法值");
                Assert.IsFalse(float.IsNaN(spec.MaxThrowDistance) || spec.MaxThrowDistance <= 0f,
                    $"非法射程 {bad} 之后仍然是合法值");
                Assert.Less(spec.MinThrowDistance, spec.MaxThrowDistance,
                    "最小距离必须严格小于最大距离，否则夹取区间是空的");
            }
        }

        // ================================================================
        // G8 · 伤害结构
        // ================================================================

        [Test]
        public void G8_伤害方向由落点指向受害者且零方向兜底为上()
        {
            Damage normal = Damage.At(Vector2.zero, new Vector2(1f, 1f), 1f, DamageSource.Tile);

            Assert.AreEqual(1f, normal.Direction.magnitude, 1e-4f, "方向必须是单位向量");

            Damage centered = Damage.At(new Vector2(3f, 3f), new Vector2(3f, 3f), 1f, DamageSource.Tile);

            Assert.AreEqual(Vector2.up, centered.Direction, "落点压在受害者身上时必须兜底为 up（否则是 NaN）");
        }

        [Test]
        public void G8_零伤害零冲量不得被当成有效结算()
        {
            var empty = new Damage(Vector2.zero, 0f, DamageSource.Tile);

            Assert.IsFalse(empty.HasDamage);
            Assert.IsFalse(empty.HasKnockback);

            var knockOnly = new Damage(Vector2.zero, 0f, DamageSource.Tile, Vector2.right, 5f);

            Assert.IsTrue(knockOnly.HasKnockback, "0 伤害 + 有冲量 = 只推不扣（土球那条路）");
            Assert.IsFalse(knockOnly.HasDamage);
        }

        // ================================================================
        // G9 · 追击决策
        // ================================================================

        [Test]
        public void G9_停止距离内不给速度但保留方向()
        {
            Steering near = Steering.Resolve(Vector2.zero, new Vector2(0.3f, 0f), 0.6f, 60f, 3.6f, 1f);

            Assert.AreEqual(0f, near.Speed, 1e-4f, "进入停止距离后目标速度必须是 0");
            Assert.Greater(near.Direction.sqrMagnitude, 0f,
                "够近了也要给方向：丢掉它等于把「够近了但仍要面向目标」这件事提前抹掉");
        }

        [Test]
        public void G9_超出追击范围不给方向()
        {
            Steering far = Steering.Resolve(Vector2.zero, new Vector2(100f, 0f), 0.6f, 60f, 3.6f, 1f);

            Assert.IsTrue(far.IsIdle, "超出追击范围必须是「不动」，否则「跑得够远能脱离」永远不成立");
        }

        [Test]
        public void G9_站在目标点上不给方向()
        {
            Steering same = Steering.Resolve(new Vector2(2f, 2f), new Vector2(2f, 2f), 0.6f, 60f, 3.6f, 1f);

            Assert.IsTrue(same.IsIdle, "零向量归一化是 NaN：必须返回「不动」而不是硬塞方向");
        }

        [Test]
        public void G9_减速只做乘法不做钳制()
        {
            Steering slowed = Steering.Resolve(Vector2.zero, new Vector2(5f, 0f), 0.6f, 60f, 3.6f, 0.45f);

            Assert.AreEqual(3.6f * 0.45f, slowed.Speed, 1e-4f);

            Steering boosted = Steering.Resolve(Vector2.zero, new Vector2(5f, 0f), 0.6f, 60f, 3.6f, 5f);

            Assert.AreEqual(3.6f * 5f, boosted.Speed, 1e-4f,
                "本函数不做钳制：把系数夹到 [0,1] 是 EnemyLogic.SetSlowMultiplier 的职责" +
                "（判据收在一处，测试才好钉）");
        }

        // ================================================================
        // G10 · 敌人逻辑：受伤禁足与击退（白模 W19 / W20 的回归）
        // ================================================================

        private static EnemySpec EnemySpecFixture()
        {
            return new EnemySpec(1, "测试敌人", 0.45f, 3.6f, 14f, 10f, 0.6f, 60f, 0.24f, 3, 4f);
        }

        private static EnemyLogic NewEnemyLogic(ProbeMotor motor, in EnemySpec spec)
        {
            CharacterConfig config = EnemyCharacterFactory.Build(in spec);

            return new EnemyLogic(motor, in spec, config);
        }

        [Test]
        public void G10_禁足帧数必须字面成立()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            EnemyLogic logic = NewEnemyLogic(motor, in spec);

            // 不把"12"写死在断言里：秒 → 帧的换算是 CeilToInt，浮点除法在边界上会给出 12 或 13。
            // 真正要钉住的是**递减的位置**（帧末）—— 曾经在帧首递减，于是实际少禁足一帧，
            // 冲量在最后那一帧被转向覆盖（实测速度从 5.5 掉到 3.6）。
            int expected = Mathf.CeilToInt(0.24f / 0.02f);

            Assert.Greater(expected, 1, "前提：这次禁足至少两帧，否则测不出「少一帧」");

            logic.BeginStun(0.24f, 0.02f);

            int observed = 0;

            for (int i = 0; i < expected + 5; i++)
            {
                if (logic.IsStunned) observed++;

                logic.Tick(i * 0.02f, 0.02f);
            }

            Assert.AreEqual(expected, observed,
                $"禁足必须恰好持续 {expected} 帧（帧首递减会变成 {expected - 1} 帧）");
        }

        [Test]
        public void G10_禁足期间冲量不得被衰减()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            EnemyLogic logic = NewEnemyLogic(motor, in spec);

            logic.SetTarget(null);
            logic.ApplyKnockback(5.5f, Vector2.right);
            logic.BeginStun(0.24f, 0.02f);

            logic.Tick(0f, 0.02f);

            Assert.AreEqual(5.5f, motor.Velocity.magnitude, 1e-3f,
                "禁足第一帧必须原样写出冲量（写成纯提前返回会让冲量永远进不了引擎）");

            // 后续帧：零提交 ⇒ 引擎速度原样保留。
            for (int i = 1; i < 12; i++) logic.Tick(i * 0.02f, 0.02f);

            Assert.AreEqual(5.5f, motor.Velocity.magnitude, 1e-3f,
                "禁足期间速度必须保持不变（零提交，不是「衰减到 0」）");
        }

        [Test]
        public void G10_击退总位移必须看得出来()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            EnemyLogic logic = NewEnemyLogic(motor, in spec);

            logic.SetTarget(null);
            logic.ApplyKnockback(5.5f, Vector2.right);
            logic.BeginStun(0.24f, 0.02f);

            float dt = 0.02f;
            float now = 0f;
            Vector2 start = motor.Position;

            // 禁足 12 帧 + 之后 1 秒的滑停。
            for (int i = 0; i < 12 + 50; i++)
            {
                logic.Tick(now, dt);

                motor.Position += motor.Velocity * dt;
                now += dt;
            }

            float distance = Vector2.Distance(start, motor.Position);

            Assert.Greater(distance, 1.2f, $"击退总位移 {distance} 太小 —— 看起来会像「没打中」");
            Assert.Less(distance, 3.0f, $"击退总位移 {distance} 太大 —— 会被推出战场");
        }

        [Test]
        public void G10_禁足结束后必须还能重新贴上来()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            EnemyLogic logic = NewEnemyLogic(motor, in spec);

            // 玩家在 2 米外：追击范围内、停止距离外。
            var player = new Vector2(2f, 0f);

            logic.SetTarget(player);
            logic.ApplyKnockback(5.5f, Vector2.right);
            logic.BeginStun(0.24f, 0.02f);

            float dt = 0.02f;
            float now = 0f;

            for (int i = 0; i < 300; i++)
            {
                logic.Tick(now, dt);

                motor.Position += motor.Velocity * dt;
                now += dt;
            }

            float distance = Vector2.Distance(motor.Position, player);

            Assert.LessOrEqual(distance, spec.StopDistance + 0.2f,
                $"被击退之后敌人必须能重新压回来（当前距离 {distance}）—— 否则整局只掉一次血");
        }

        [Test]
        public void G10_减速系数必须被净化()
        {
            var motor = new ProbeMotor();
            EnemySpec spec = EnemySpecFixture();
            EnemyLogic logic = NewEnemyLogic(motor, in spec);

            logic.SetTarget(new Vector2(5f, 0f));

            logic.SetSlowMultiplier(float.NaN);
            logic.Tick(0f, 0.02f);

            Assert.IsFalse(float.IsNaN(motor.Velocity.x), "非数减速系数不得传染进速度（否则角色会消失）");

            motor.Velocity = Vector2.zero;
            logic.SetSlowMultiplier(-3f);
            logic.Tick(0.02f, 0.02f);

            Assert.AreEqual(0f, motor.Velocity.magnitude, 1e-6f,
                "负数系数必须被夹到 0（定住），而不是把速度反过来推");
        }

        // ================================================================
        // G11 · 敌人视效决策
        // ================================================================

        [Test]
        public void G11_身体颜色四态两两不同()
        {
            Color normal = EnemyVisual.BodyColor(1f, false);
            Color slowed = EnemyVisual.BodyColor(0.45f, false);
            Color flash = EnemyVisual.BodyColor(1f, true);
            Color flashSlowed = EnemyVisual.BodyColor(0.45f, true);

            Assert.AreNotEqual(normal, slowed);
            Assert.AreNotEqual(normal, flash);
            Assert.AreNotEqual(slowed, flashSlowed);
            Assert.AreNotEqual(flash, flashSlowed, "踩在减速格里被打中必须同时看得出「深」和「闪」");

            float normalLuma = normal.r + normal.g + normal.b;
            float slowedLuma = slowed.r + slowed.g + slowed.b;
            float flashLuma = flash.r + flash.g + flash.b;

            Assert.Less(slowedLuma, normalLuma, "减速色必须比正常色暗");
            Assert.Greater(flashLuma, normalLuma, "闪烁色必须比正常色亮");
        }

        [Test]
        public void G11_耐久数字与闪烁相位()
        {
            Assert.AreEqual("3", EnemyVisual.HpText(3));
            Assert.AreEqual(string.Empty, EnemyVisual.HpText(0), "耐久为 0 时写空串：写「0」会让人以为它还在场上");

            Assert.IsFalse(EnemyVisual.IsFlashOn(0f, 4f), "频率为 0 时恒不亮（不除零、不崩）");
            Assert.IsFalse(EnemyVisual.IsFlashOn(1f, float.NaN), "非数频率按不闪处理");

            // 4 Hz ⇒ 周期 0.25 秒 ⇒ [0, 0.125) 亮。
            Assert.IsTrue(EnemyVisual.IsFlashOn(0.05f, 4f), "周期的前半段应当是亮的");
            Assert.IsFalse(EnemyVisual.IsFlashOn(0.2f, 4f), "周期的后半段应当是不亮的");
        }

        // ================================================================
        // G12 · 玩家血量与资源
        // ================================================================

        private static PlayerSpec PlayerSpecFixture(float maxHp = 100f, float invulnerable = 0.8f)
        {
            return new PlayerSpec(1, "玩家", maxHp, 10f, invulnerable, 1.2f, 0.5f, 12f, 12f, 1f);
        }

        [Test]
        public void G12_无敌帧判据对非数必须落到可以受伤那一侧()
        {
            Assert.IsTrue(PlayerHealth.CanTakeDamage(1f, float.NaN),
                "NaN 时必须照常结算：写成 now >= until 会让玩家变成永久无敌，而屏幕上什么都不会显示");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(1f, float.NegativeInfinity));
            Assert.IsFalse(PlayerHealth.CanTakeDamage(1f, 2f), "无敌期未过时必须挡住");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(2f, 2f), "无敌到期那一帧必须可以受伤");
        }

        [Test]
        public void G12_无敌期内的伤害必须被整条挡掉()
        {
            PlayerSpec spec = PlayerSpecFixture();
            var health = new PlayerHealth(in spec);

            Assert.AreEqual(100f, health.Current, 1e-4f);

            Assert.IsTrue(health.ApplyDamage(10f, 0f), "第一次必须扣血");
            Assert.AreEqual(90f, health.Current, 1e-4f);

            Assert.IsFalse(health.ApplyDamage(10f, 0.1f), "无敌期内必须被挡住");
            Assert.AreEqual(90f, health.Current, 1e-4f, "被挡住时不该扣血");

            Assert.IsTrue(health.ApplyDamage(10f, 0.8f), "无敌到期后必须能再扣");
            Assert.AreEqual(80f, health.Current, 1e-4f);
        }

        [Test]
        public void G12_血量不会扣成负数且可以重置()
        {
            PlayerSpec spec = PlayerSpecFixture();
            var health = new PlayerHealth(in spec);

            health.ApplyDamage(1000f, 0f);

            Assert.AreEqual(0f, health.Current, 1e-4f, "血量不该是负数");
            Assert.IsFalse(health.IsAlive);

            health.ResetToFull();

            Assert.AreEqual(100f, health.Current, 1e-4f);
            Assert.IsTrue(health.IsAlive);
            Assert.IsFalse(health.IsInvulnerable(0f), "重置必须把无敌期一起清掉");
        }

        [Test]
        public void G12_资源不足时不得改动任何状态()
        {
            var resources = new PlayerResources();

            Assert.IsFalse(resources.TryConsume(1), "没有资源时消耗必须失败");
            Assert.AreEqual(0, resources.WaterBallCount);

            resources.Add(2);

            Assert.AreEqual(2, resources.WaterBallCount);
            Assert.IsFalse(resources.TryConsume(3), "不够就是不够，不允许扣成负数");
            Assert.AreEqual(2, resources.WaterBallCount, "失败时数量必须原样");
            Assert.IsTrue(resources.TryConsume(2));
            Assert.AreEqual(0, resources.WaterBallCount);
        }

        [Test]
        public void G12_攻击冷却()
        {
            var cooldown = new AttackCooldown();

            Assert.IsTrue(cooldown.CanAttack(0f), "开局即可攻击");

            cooldown.MarkUsed(0f, 0.5f);

            Assert.IsFalse(cooldown.CanAttack(0.49f));
            Assert.IsTrue(cooldown.CanAttack(0.5f), "间隔是闭区间：到点即可攻击");

            cooldown.Reset();

            Assert.IsTrue(cooldown.CanAttack(0.5f));
        }

        // ================================================================
        // G13 · 波次
        // ================================================================

        private static WaveSpec WaveSpecFixture()
        {
            return new WaveSpec(1, "默认", 4, 0.25f, 1.5f, 2.5f, 5f);
        }

        [Test]
        public void G13_开局延时后才出第一只且一只一只出()
        {
            WaveSpec spec = WaveSpecFixture();
            var logic = new WaveLogic(in spec);
            var output = new List<WaveLogic.SpawnRequest>();

            float now = 0f;
            float dt = 0.05f;

            // 【为什么是「帧数由配置算出 + 时间窗」而不是「第 30 帧恰好出第一只」】
            // 计时是 `_timer -= dt` 的浮点累减：1.5f 连减 30 次 0.05f 会剩下 +3.05e-7 的单精度残差，
            // 于是第 30 帧 `_timer > 0f` 仍然成立、第一只其实落在第 31 帧。
            // 这是浮点余量而不是逻辑缺陷 ——「延时 1.5s」本身就允许 ±1 帧，所以判据必须是时间窗。
            //
            // 强断言一条都没放松，只是把「恰好某一帧翻转」换成「配置算出的帧数 + 少数几帧的窗口」：
            //   beforeDueFrames = floor(InitialDelay/dt) - 1 = 29 帧（1.45s）
            //     严格早于延时到点：这 29 帧里**一只都不许出**。
            //   windowFrames = 3 帧（0.15s）明显小于 SpawnInterval = 0.25s，
            //     所以「一只一只出」不会被这条容差放过去。
            int beforeDueFrames = Mathf.FloorToInt(spec.InitialDelay / dt) - 1;
            int windowFrames = 3;

            Assert.Greater(beforeDueFrames, 0, "前提：延时要跨不止一帧，否则「延时之前」无从谈起");

            for (int i = 0; i < beforeDueFrames; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                Assert.AreEqual(0, output.Count,
                    $"第 {i + 1} 帧（{now:F2}s）不该有敌人出生：延时 {spec.InitialDelay}s 还没到");

                now += dt;
            }

            // 延时到点：允许 ±2 帧的浮点余量（窗口头尾各留 1 帧）。
            int firstSpawnAt = -1;

            for (int i = 0; i < windowFrames; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                if (output.Count > 0)
                {
                    // 一帧只许出一只：窗口比 SpawnInterval 还短，出两只就说明「一次刷一堆」。
                    Assert.AreEqual(1, output.Count, "一帧只该出一只");
                    Assert.AreEqual(1, output[0].WaveIndex, "第一波的序号是 1（不是 0）");

                    firstSpawnAt = i;
                    break;
                }

                now += dt;
            }

            Assert.GreaterOrEqual(firstSpawnAt, 0,
                $"延时 {spec.InitialDelay}s 到点后 {windowFrames} 帧（{windowFrames * dt}s）内必须出第一只"
                + $"（允许 ±{windowFrames} 帧的浮点余量：累减计时在单精度下会多留一帧）");

            // 同波内两只之间同理：先钉 floor(SpawnInterval/dt) - 1 = 4 帧（0.2s）不许出第二只，
            // 再在同样宽的窗口内必须出。实测第二只落在间隔的第 6 帧
            //（0.25f 连减 5 次 0.05f 还剩 +7.45e-9），窗口 [5,7] 把这一帧包住并留了 1 帧余量。
            int noSecondFrames = Mathf.FloorToInt(spec.SpawnInterval / dt) - 1;

            Assert.Greater(noSecondFrames, 0, "前提：间隔要跨不止一帧，否则「一只一只出」无从谈起");

            for (int i = 0; i < noSecondFrames; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                Assert.AreEqual(0, output.Count,
                    $"间隔 {spec.SpawnInterval}s 还没到（第 {i + 1} 帧）就出了第二只");

                now += dt;
            }

            int secondSpawnAt = -1;

            for (int i = 0; i < windowFrames; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                if (output.Count > 0)
                {
                    Assert.AreEqual(1, output.Count, "一帧只该出一只");
                    Assert.AreEqual(1, output[0].WaveIndex, "第二只仍然属于第一波");

                    secondSpawnAt = i;
                    break;
                }

                now += dt;
            }

            Assert.GreaterOrEqual(secondSpawnAt, 0,
                $"间隔 {spec.SpawnInterval}s 到点后 {windowFrames} 帧内必须出第二只"
                + $"（允许 ±{windowFrames} 帧的浮点余量：累减计时在单精度下会多留一帧）");
        }

        [Test]
        public void G13_每波只出配置的只数()
        {
            WaveSpec spec = WaveSpecFixture();
            var logic = new WaveLogic(in spec);
            var output = new List<WaveLogic.SpawnRequest>();

            float now = 0f;
            float dt = 0.05f;

            int spawned = 0;

            for (int i = 0; i < 4000; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                spawned += output.Count;

                // 第一波 4 只出完就停手（此时 logic 进入"等清场"）。
                if (spawned >= spec.EnemiesPerWave) break;

                now += dt;
            }

            Assert.AreEqual(spec.EnemiesPerWave, spawned, "一波必须恰好出配置的只数");

            // 场上还有敌人 ⇒ 不该开下一波。
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
            var logic = new WaveLogic(in spec);
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

                Assert.AreEqual(0, output.Count,
                    $"清场后第 {i + 1} 帧就开下一波，没等满 {spec.RespawnDelay}s");

                now += dt;
            }

            // 超过等待时间：下一波开始。
            for (int i = 0; i < 10; i++)
            {
                logic.Tick(now, dt, false, Vector2.zero, output);

                if (output.Count > 0) break;

                now += dt;
            }

            Assert.Greater(output.Count, 0, "等待时间到点后必须开下一波");
            Assert.AreEqual(2, logic.WaveIndex, "第二波的序号是 2");
            Assert.AreEqual(2, output[0].WaveIndex);
        }

        // ================================================================
        // G14 · 随机
        // ================================================================

        [Test]
        public void G14_固定种子必须可复现()
        {
            Rng.SetImpl(new DefaultRng(12345));

            float a = Rng.Value01();
            Vector2 b = Rng.InsideUnitCircle();

            Rng.SetImpl(new DefaultRng(12345));

            Assert.AreEqual(a, Rng.Value01(), 1e-6f, "同一个种子必须给出同一个序列");

            Vector2 again = Rng.InsideUnitCircle();

            Assert.AreEqual(b.x, again.x, 1e-6f, "同一个种子必须给出同一个序列");
            Assert.AreEqual(b.y, again.y, 1e-6f);

            Rng.Reset();
        }

        [Test]
        public void G14_单位圆内的点必须真的在圆内()
        {
            var rng = new DefaultRng(7);

            for (int i = 0; i < 1000; i++)
            {
                Vector2 p = rng.InsideUnitCircle();

                Assert.LessOrEqual(p.magnitude, 1f + 1e-5f, "单位圆内的点不得跑到圆外");
            }

            Assert.AreEqual(3, rng.Range(3, 3), "空区间必须安静地返回下界，不抛异常");
        }

        // ================================================================
        // G15 · 球效果
        // ================================================================

        [Test]
        public void G15_水球请求格子状态而土球什么都不做()
        {
            var context = new CountingEffectContext();

            BallSpec water = WaterBall();
            BallSpec earth = EarthBall();

            new TileStateBallEffect().Apply(new Vector3Int(1, 1, 0), in water, context);
            new NullBallEffect().Apply(new Vector3Int(1, 1, 0), in earth, context);

            Assert.AreEqual(1, context.RequestCount, "水球必须请求一次状态转换");
            Assert.AreEqual(TileStateType.Mud, context.LastBall.TileState);
        }

        private sealed class CountingEffectContext : IBallEffectContext
        {
            public int RequestCount;

            public BallSpec LastBall;

            public void RequestTileState(Vector3Int cell, in BallSpec ball)
            {
                RequestCount++;
                LastBall = ball;
            }
        }
    }
}
