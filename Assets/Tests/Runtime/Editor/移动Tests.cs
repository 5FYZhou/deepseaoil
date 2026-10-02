// ---------------------------------------------------------------------------
// 俯视角移动 · 运行期测试
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 与 Data层Tests.cs 同机制：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【为什么是这些用例】俯视角移动的风险集中在"手感语义"，错了不报错、只是手感不对：
//   M1  零输入当帧停        —— 残留速度 = 滑行
//   M2  斜向不快 √2 倍      —— 俯视角最经典的 bug
//   M3  反向无过渡          —— 有加速度就是惯性，与"零惯性"直接冲突
//   M4  零输入保持朝向      —— 站住时精灵自己翻面
//   M5  朝向只翻水平符号    —— 上下移动不该把精灵颠倒
//   M6  冲刺沿朝向 8 向     —— 本轮改的语义本身（不再是固定 x 轴）
//   M7  冲刺走输入缓冲窗口  —— 框架件（InputBuffer）没被改坏
//   M8  状态流转站与走      —— 删掉空中态后转移是否仍自洽
//   M9  状态切换发事件      —— 框架件（EventBus）仍通
//   M10 边界真的钳住        —— 钳位失效 = 走出地图
//   M11 未接线不钳位        —— 无条件 Clamp 会把玩家钉死在地图原点
//   M12 刚体速度真的写入    —— 端口实现的唯一职责
//   M13 抢占失败不改状态    —— Configure 必须跑在消费成功之后
//   M14 首帧同样参与抢占    —— 首帧曾是"无抢占"特权帧，第一次按冲刺被吞
//   M15 限速整体钳制        —— ClampSpeed 按当帧速度收敛，未超限不得改动
//   M16 外力按 Δt 累进      —— ApplyExtraForce 的语义与"先外力后钳制"顺序
//
// 【与射线检测的关系】MovementMotor 已不含 groundCheck/wallCheck/groundMask：
//   俯视角的阻挡由刚体碰撞解算，逻辑层不需要"是否站地/是否贴墙"。
//   将来若有角色需要地形探测，按那个角色的需求单独实现，不预先把射线塞回移动执行器。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public class 移动Tests
    {
        /// <summary>逻辑层测试用的假执行器：只记录被写入的值，不碰物理。</summary>
        private sealed class RecordingMotor : IMovementMotor
        {
            public Vector2 Velocity { get; set; }
            public Vector2 Position { get; private set; }
            public Vector2 Facing { get; set; }
            public int MoveCallCount { get; private set; }

            public void Move(Vector2 velocity)
            {
                // 模拟"物理步已结算"：下一帧帧首读到的就是这个值。
                Velocity = velocity;
                MoveCallCount++;
            }

            public void SetPosition(Vector2 position)
            {
                Position = position;
            }
        }

        private PlayerConfig _config;
        private InputBuffer _buffer;
        private RecordingMotor _motor;
        private PlayerLogic _logic;

        private int _stateChangeCount;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.moveSpeed = 8f;
            _config.dashSpeed = 20f;
            _config.dashDuration = 0.2f;
            _config.dashCooldown = 1.5f;
            _config.dashBufferTime = 0.12f;
            _config.inputBufferTime = 0.12f;
            _config.snapToEightDirections = true;
            _config.extraForceScale = 0f;

            _buffer = new InputBuffer(
                Mathf.Max(_config.inputBufferTime, _config.dashBufferTime),
                Mathf.RoundToInt(1f / 0.02f)
                );

            _motor = new RecordingMotor();
            _logic = new PlayerLogic(_motor, _config, _buffer);

            _stateChangeCount = 0;
            EventBus<MovementStateChanged>.Subscribe(OnStateChanged);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<MovementStateChanged>.Unsubscribe(OnStateChanged);
            Object.DestroyImmediate(_config);
        }

        private void OnStateChanged(MovementStateChanged evt)
        {
            _stateChangeCount++;
        }

        /// <summary>推进一个逻辑帧：帧首把假执行器速度归零，模拟"每帧被物理重新结算"。</summary>
        /// <remarks>
        /// 真实链路里 FixedTick 帧首读的是引擎速度；假执行器若保留上一帧的值，
        /// 零提交帧（如 IdleState.StopMove 后）会读到旧速度，断言就失真了。
        /// </remarks>
        private void Tick(Vector2 move, float now, bool dashPressed = false, bool resetVelocity = true)
        {
            if (resetVelocity) _motor.Velocity = Vector2.zero;

            var snapshot = new InputSnapshot(move, false, dashPressed, false);
            var world = new WorldInfo(move, new BoundsArea(null));

            _buffer.Push(in snapshot, now);
            _logic.FixedTick(new LogicContext(now, 0.02f, in world, in snapshot));
        }

        // ================================================================
        // 移动语义
        // ================================================================

        [Test]
        public void M1_零输入当帧停()
        {
            Tick(Vector2.zero, 0f);

            Assert.AreEqual(Vector2.zero, _motor.Velocity, "零输入必须当帧停住，不得残留速度");
            Assert.AreEqual(MovementStateTag.Idle, _logic.CurrentState);
        }

        [Test]
        public void M2_斜向速度等于直向速度()
        {
            Tick(new Vector2(0.7071f, 0.7071f), 0f);

            float expected = _config.moveSpeed * _config.moveSpeed;
            float actual = _motor.Velocity.sqrMagnitude;

            // 容差 0.01：Vector2.normalized 与 magnitude 的浮点误差约 1.2e-3，
            // 而"未归一化"造成的偏差是 +64（(1,1) 会得到 2×speed²），量级差 4 个数量级，不会误判。
            Assert.AreEqual(expected, actual, 0.01f,
                $"斜向速度平方应为 {expected}（= moveSpeed²），实测 {actual}；接近 2×{expected} 即未归一化");
        }

        [Test]
        public void M3_方向切换无惯性()
        {
            Tick(Vector2.right, 0f);
            Assert.AreEqual(_config.moveSpeed, _motor.Velocity.x, 1e-3f, "向右一帧后速度应为 +moveSpeed");

            Tick(Vector2.left, 0.02f);
            Assert.AreEqual(-_config.moveSpeed, _motor.Velocity.x, 1e-3f,
                "反向输入必须当帧变为 -moveSpeed，出现中间值即存在加速度/衰减");
        }

        // ================================================================
        // 朝向
        // ================================================================

        [Test]
        public void M4_零输入保持朝向()
        {
            Tick(Vector2.left, 0f);
            Assert.Less(_motor.Facing.x, 0f, "向左移动后朝向应为左");

            Tick(Vector2.zero, 0.02f);
            Assert.Less(_motor.Facing.x, 0f, "站住后朝向不得被重置");
            Assert.AreEqual(MovementStateTag.Idle, _logic.CurrentState);
        }

        [Test]
        public void M5_朝向只翻水平符号()
        {
            var go = CreateMotorObject("移动测试_朝向", out MovementMotor motor);
            try
            {
                // 「朝左」与「纯竖直」的先后顺序是本用例的重点：先朝左再朝上，
                // 水平镜像必须保持朝左（曾因 value.x == 0 时取绝对值而翻回朝右）。
                motor.Facing = Vector2.left;
                Assert.Less(go.transform.localScale.x, 0f, "朝左应翻成负缩放");
                Assert.Greater(go.transform.localScale.y, 0f, "竖直缩放不得被翻转");

                motor.Facing = Vector2.up;
                Assert.Less(go.transform.localScale.x, 0f, "纯竖直朝向不得改动已有的水平镜像");
                Assert.AreEqual(Vector2.up, motor.Facing, "竖直朝向仍要记进 Facing（供 8 向动画用）");

                motor.Facing = Vector2.right;
                Assert.Greater(go.transform.localScale.x, 0f, "朝右应翻回正缩放");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // 冲刺（改造后语义：沿朝向 8 向 ／ 提交失败不得改动状态）
        // ================================================================

        [Test]
        public void M6_冲刺沿朝向八向()
        {
            // 先建立斜向朝向
            Tick(new Vector2(0.7071f, 0.7071f), 0f);
            _motor.Velocity = Vector2.zero;

            // 再触发冲刺（无输入 → 用最近朝向）
            Tick(Vector2.zero, 0.02f, dashPressed: true, resetVelocity: false);

            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState, "有缓冲按下且冷却已过，应抢占到 Dash");

            Vector2 v = _motor.Velocity;
            Assert.AreEqual(_config.dashSpeed, v.magnitude, 1e-3f, $"冲刺速率应为 dashSpeed={_config.dashSpeed}");

            Vector2 expected = new Vector2(0.7071f, 0.7071f);
            Assert.Greater(Vector2.Dot(v.normalized, expected), 0.999f,
                $"冲刺方向应为玩家朝向 {expected}，实测 {v.normalized}；若等于 (1,0) 说明仍是固定 x 轴冲刺");
        }

        [Test]
        public void M7_冲刺走输入缓冲窗口()
        {
            // 窗口内：可消费
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 0f);

            Assert.IsTrue(_logic.CanDash(0.01f), "窗口内的冲刺按下应判定为可冲刺");
            Assert.IsTrue(_logic.TryConsumeDash(0.01f), "首次消费应成功");
            Assert.IsFalse(_logic.TryConsumeDash(0.01f), "同一次按下只能消费一次");

            // 窗口外：不可消费
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 10f);

            Assert.IsFalse(_logic.CanDash(10f + _config.dashBufferTime + 0.5f), "超出缓冲窗口的按下必须失效");

            // 冷却内：即使缓冲有按下也不可冲
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 20f);
            Assert.IsTrue(_logic.TryConsumeDash(20f), "冷却已过应能消费");

            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 20.1f);
            Assert.IsFalse(_logic.CanDash(20.1f), "冷却未过时不得再冲");
        }

        /// <summary>
        /// 回归：第一个物理帧同样参与抢占，不是"无抢占"特权帧。
        /// </summary>
        /// <remarks>
        /// 曾有的缺陷是 <c>CheckTransitions</c> 在 <c>CurrentState == null</c> 时直接
        /// <c>return GetFallBackState()</c>，整条抢占链被跳过：玩家的第一次按冲刺
        /// （冷却与缓冲都成立）会被吞掉，到第二帧才生效。
        /// </remarks>
        [Test]
        public void M14_首帧同样参与抢占()
        {
            var snap = new InputSnapshot(Vector2.right, false, true, false);
            _buffer.Push(in snap, 0f);

            Assert.IsTrue(_logic.CanDash(0f), "前置条件：首帧冷却与缓冲都成立");

            Tick(Vector2.right, 0f, dashPressed: true);

            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState, "首帧应直接进入 Dash");
            Assert.IsFalse(_buffer.CanConsume(InputType.Dash, 0f, _config.dashBufferTime),
                "首帧既已提交，这次按下必须被消费掉（否则第二帧会再冲一次）");
            Assert.Less(Vector2.Distance(new Vector2(_config.dashSpeed, 0f), _motor.Velocity), 1e-3f,
                "首帧提交成功就该是冲刺速度，而不是基础态的 moveSpeed");
        }

        /// <summary>
        /// 抢占失败（未提交）时，状态、方向、速度都必须原样保留。
        /// </summary>
        /// <remarks>
        /// <b>另一条分支是死代码</b>：<c>TryDecidePreempt</c> 调 <c>CanDash</c>、
        /// <c>TryCommitPreempt</c> 调 <c>TryConsumeDash</c>，两者查的是同一个
        /// <c>dashBufferTime</c> 窗口、同一个 <c>now</c>，所以"判定成功但消费失败"
        /// 这一支在 Dash 上不可达。本用例走的是<b>冷却未过</b>那条真实路径：
        /// 抢占判定直接为假，什么都不该被改动。
        /// （<c>Configure</c> 放在消费之后仍是对的——它保证"状态对象只在提交成功时被改写"，
        /// 只是当前没有用例能触发那一支。）
        /// </remarks>
        [Test]
        public void M13_抢占失败不得改动状态()
        {
            Tick(Vector2.right, 0f, dashPressed: true);
            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState, "首帧应抢占到 Dash");
            Assert.AreEqual(Vector2.right, _logic.MoveGroup.Dash.Direction, "Configure 应把入场方向喂成输入方向");

            // t=0.15 时冷却（1.5s）远未过：抢占判定为假，本帧什么都不该发生
            _buffer.Push(new InputSnapshot(Vector2.up, false, true, false), 0.15f);
            Tick(Vector2.up, 0.15f, dashPressed: true, resetVelocity: false);

            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState,
                "冲刺时长 0.2s 未到，且抢占未成立：必须仍在 Dash，不得被基础态接管");
            Assert.AreEqual(Vector2.right, _logic.MoveGroup.Dash.Direction,
                "抢占未提交却改写了方向，说明 Configure 跑在消费成功之前");
            Assert.Less(Vector2.Distance(new Vector2(_config.dashSpeed, 0f), _motor.Velocity), 1e-3f,
                "抢占未提交不得让基础态接管速度：仍在 Dash 中应保持冲刺速度");

            // 按下还在缓冲里：这次按下从未被消费，只是随窗口自然老化
            Assert.IsTrue(_buffer.CanConsume(InputType.Dash, 0.15f, _config.dashBufferTime),
                "未提交的抢占不得消费缓冲 —— 按下应当原样留在里面");
        }

        // ================================================================
        // 状态机（框架件）
        // ================================================================

        [Test]
        public void M8_状态流转站与走()
        {
            Tick(Vector2.right, 0f);
            Assert.AreEqual(MovementStateTag.Move, _logic.CurrentState, "有输入应进入 Move");

            Tick(Vector2.zero, 0.02f);
            Assert.AreEqual(MovementStateTag.Idle, _logic.CurrentState, "输入归零应回到 Idle，不经任何空中态");

            Tick(Vector2.up, 0.04f);
            Assert.AreEqual(MovementStateTag.Move, _logic.CurrentState, "再次输入应回到 Move");
        }

        [Test]
        public void M9_状态切换发事件()
        {
            int before = _stateChangeCount;

            Tick(Vector2.right, 0f);      // 首次进入不发事件
            Assert.AreEqual(before, _stateChangeCount, "状态机首次进入不应发事件");

            Tick(Vector2.zero, 0.02f);    // Move → Idle
            Assert.Greater(_stateChangeCount, before, "状态切换必须广播 MovementStateChanged");
        }

        // ================================================================
        // 边界（BoundsArea）
        // ================================================================

        [Test]
        public void M10_边界真的钳住()
        {
            var boundsGo = new GameObject("移动测试_边界");
            try
            {
                var box = boundsGo.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                box.offset = Vector2.zero;
                box.size = new Vector2(4f, 4f);   // 世界范围 [-2, 2] × [-2, 2]
                var bounds = new BoundsArea(box);

                Assert.IsTrue(bounds.IsValid, "尺寸非零的矩形应判定为有效区域");
                Assert.IsTrue(bounds.TryClamp(new Vector2(99f, 0f), out Vector2 clamped), "越界位置必须报告已钳位");
                Assert.AreEqual(2f, clamped.x, 1e-3f, "应被钳到右边界");
                Assert.AreEqual(0f, clamped.y, 1e-3f, "未越界的分量不得被改动");
            }
            finally
            {
                Object.DestroyImmediate(boundsGo);
            }
        }

        [Test]
        public void M11_边界未接线不钳位()
        {
            // ① collider 缺失
            var empty = new BoundsArea(null);
            Assert.IsFalse(empty.IsValid, "空引用必须是无效区域");
            Assert.IsFalse(empty.TryClamp(new Vector2(99f, -99f), out Vector2 kept), "无效区域不得报告钳位");
            Assert.AreEqual(new Vector2(99f, -99f), kept, "无效区域必须原样返回位置");

            // ② 形状被停用
            var go = new GameObject("移动测试_停用边界");
            try
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(4f, 4f);
                box.enabled = false;
                var disabled = new BoundsArea(box);

                Assert.IsFalse(disabled.IsValid, "停用的碰撞体必须判无效——否则玩家会被钉死在地图原点");
                Assert.IsFalse(disabled.TryClamp(new Vector2(99f, -99f), out Vector2 keptDisabled));
                Assert.AreEqual(new Vector2(99f, -99f), keptDisabled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // 执行器端口
        // ================================================================

        [Test]
        public void M12_刚体速度真的被写入()
        {
            var go = CreateMotorObject("移动测试_速度", out MovementMotor motor);
            try
            {
                var body = go.GetComponent<Rigidbody2D>();

                // 首次访问触发惰性自取与初始化
                motor.Move(new Vector2(3f, -4f));

                Assert.IsTrue(motor.IsInitialized, "首次使用必须完成自取初始化（不依赖 Awake 时机）");
                Assert.AreEqual(0f, body.gravityScale, "俯视角：重力缩放应被初始化为 0");
                Assert.IsTrue(body.freezeRotation, "俯视角：旋转应被冻结");
                Assert.AreEqual(new Vector2(3f, -4f), body.velocity, "MovementMotor.Move 必须写进 Rigidbody2D.velocity");
                Assert.AreEqual(new Vector2(3f, -4f), motor.Velocity, "Velocity 必须回读同一份真值");

                motor.SetPosition(new Vector2(1.5f, 2.5f));
                Assert.AreEqual(new Vector2(1.5f, 2.5f), motor.Position, "SetPosition 必须落到物理体位置");

                // 初始化只生效一次：不能每次读速度都把物理参数重写回去
                body.gravityScale = 0.5f;
                _ = motor.Velocity;
                Assert.AreEqual(0.5f, body.gravityScale, "重复访问不得再次执行初始化");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // 外力与限速（ApplyExtraForce / ClampSpeed，本轮改造的核心语义）
        // ================================================================

        [Test]
        public void M15_限速按当帧速度整体钳制()
        {
            var motor = new RecordingMotor();
            var logic = new LimitProbe(motor);

            // 无提交 + 无上限：不该产生任何速度（零提交帧不写速度）
            logic.FixedTick(ProbeContext(0f));
            Assert.AreEqual(Vector2.zero, motor.Velocity, "零提交帧不得凭空产生速度");

            // 上限 ≤ 0 表示不限制
            logic.Limit = 0f;
            logic.Speed = new Vector2(8f, 0f);
            logic.FixedTick(ProbeContext(0.02f));
            Assert.Less(Vector2.Distance(new Vector2(8f, 0f), motor.Velocity), 1e-3f,
                "ClampSpeed(0) 应视为不限制：速度原样写出");

            // 超限时按模长整体缩回
            logic.Limit = 3f;
            logic.Speed = new Vector2(8f, 0f);
            logic.FixedTick(ProbeContext(0.04f));
            Assert.Less(Vector2.Distance(new Vector2(3f, 0f), motor.Velocity), 1e-3f,
                "超限速度应收敛到上限模长");

            // 未超限时不得被改动
            logic.Speed = new Vector2(1f, 1f);
            logic.FixedTick(ProbeContext(0.06f));
            Assert.Less(Vector2.Distance(new Vector2(1f, 1f), motor.Velocity), 1e-3f,
                "未超限的速度不得被钳制改动");
        }

        [Test]
        public void M16_外力按ΔT累进且受强度缩放()
        {
            var motor = new RecordingMotor();
            var logic = new LimitProbe(motor);

            // ① 力为零：当帧不产生提交
            logic.Scale = 1f;
            logic.Force = Vector2.zero;
            logic.FixedTick(ProbeContext(0f));
            Assert.AreEqual(Vector2.zero, motor.Velocity, "零外力不得产生速度");

            // ② 终速 = 外力 × Δt（每帧提交，不预先乘 Δt）
            logic.Force = new Vector2(0f, -100f);
            logic.FixedTick(ProbeContext(0.02f));
            Assert.Less(Vector2.Distance(new Vector2(0f, -2f), motor.Velocity), 1e-3f,
                "外力 100、Δt 0.02 时终速应为 -2");

            // ③ 强度缩放为 0：整段空转
            motor.Velocity = Vector2.zero;
            logic.Scale = 0f;
            logic.FixedTick(ProbeContext(0.04f));
            Assert.AreEqual(Vector2.zero, motor.Velocity, "extraForceScale = 0 时必须整段空转");

            // ④ 顺序约束：先外力、后钳制。上限 3 高于终速 2，故本帧外力应完整留下。
            //    注意探针的 SetVelocity 写的是 _delta，所以钳制看到的是 (0,-2)+(0,-2) = (0,-4)，
            //    上限必须高于 4 才谈得上"没被钳"。这里用 4.5 留出余量。
            motor.Velocity = Vector2.zero;
            logic.Scale = 1f;
            logic.Limit = 4.5f;
            logic.FixedTick(ProbeContext(0.06f));
            Assert.Less(Vector2.Distance(new Vector2(0f, -2f), motor.Velocity), 1e-3f,
                "先累加外力再钳制：上限未触及时应保持 -2");

            // ⑤ 同帧超限时被钳回上限
            motor.Velocity = Vector2.zero;
            logic.Limit = 1f;
            logic.FixedTick(ProbeContext(0.08f));
            Assert.Less(Vector2.Distance(new Vector2(0f, -1f), motor.Velocity), 1e-3f,
                "超上限的外力结果应收敛到上限");
        }

        /// <summary>把 <see cref="ActorLogic"/> 的两个新写入口暴露出来的探针。</summary>
        /// <remarks>
        /// 直接测 <c>ActorLogic</c> 而不是 <c>PlayerLogic</c>：这两个入口是"共用件"，
        /// 玩家不调用它们（零惯性 + 不施外力），消费者是未来的敌人、击退、水流等。
        /// 用探针在 <c>OnTick</c> 里按测试脚本调用，才能覆盖到真实调用次序。
        /// </remarks>
        private sealed class LimitProbe : ActorLogic
        {
            public Vector2 Force;
            public float Scale = 1f;
            public float Limit;
            public Vector2 Speed;

            public LimitProbe(IMovementMotor motor) : base(motor, ScriptableObject.CreateInstance<CharacterConfig>())
            {
            }

            protected override void OnTick(in LogicContext ctx)
            {
                SetVelocity(Speed);
                SetExtraForceScale(Scale);
                ApplyExtraForce(in ctx, Force);
                ClampSpeed(Limit);
            }
        }

        private static LogicContext ProbeContext(float now)
        {
            var snapshot = InputSnapshot.Empty;
            var world = new WorldInfo(Vector2.zero, new BoundsArea(null));

            return new LogicContext(now, 0.02f, in world, in snapshot);
        }

        // ================================================================
        // 辅助
        // ================================================================

        /// <summary>
        /// 建一个 MovementMotor 物体：Rigidbody2D ＋ 组件，接线与场景一致。
        /// </summary>
        /// <remarks>
        /// 顺序不能反：先加 Rigidbody2D 再加 MovementMotor，不依赖 <c>RequireComponent</c> 的自动补加顺序。
        /// <b>注意 EditMode 下 <c>AddComponent</c> 不会触发 <c>Awake</c></b>，所以本类不能依赖 <c>Awake</c>
        /// 里的自取与自检——<c>MovementMotor</c> 的物理体引用因此做成惰性兜底（见其 <c>Body</c> 属性）。
        /// 这也意味着 <c>gravityScale = 0</c> / <c>freezeRotation</c> 那两行在 EditMode 测试里不会执行，
        /// 它们由场景实际 Play 时生效。
        /// </remarks>
        private static GameObject CreateMotorObject(string name, out MovementMotor motor)
        {
            var go = new GameObject(name);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            motor = go.AddComponent<MovementMotor>();

            return go;
        }
    }
}