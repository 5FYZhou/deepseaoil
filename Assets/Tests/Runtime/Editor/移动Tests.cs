// ---------------------------------------------------------------------------
// 俯视角移动 · 运行期测试
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 与 Data层Tests.cs 同机制：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【为什么是这些用例】俯视角移动的风险集中在"手感语义"，错了不报错、只是手感不对：
//   M1  零输入当帧停        —— 残留速度 = 滑行
//   M2  斜向不快 √2 倍      —— 俯视角最经典的 bug（喂**未归一化**的 (1,1)，让实现自己去归一化）
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
//   M16 外力按 Δt 累进      —— ApplyExtraForce 的语义与强度缩放
//   M17 8 向吸附是纯函数    —— **唯一**覆盖 PlayerController 那条吸附/归一化通路的用例
//   M18 外力必须早于钳制    —— 顺序反了本帧外力被整个吃掉，且只看帧末输出验不出来
//
// 【覆盖边界，写在明处】除 M12 / M17 外，本文件测的都是 Logic 层：
//   它直接构造 PlayerLogic ＋ 假执行器，**不经过 PlayerController**。
//   于是"宿主把输入装配错了"这一类缺陷（未归一化、缓冲推了原始快照、边界没接线）
//   在 M17 之前是完全无覆盖的 —— 那正是"测试全绿但缺陷仍在"的成因。
//   **场景搭建与接线检验没有自动化**：按流程由人工完成（建场景、挂组件、连引用、Play 手测）。
//   本工程曾有一个 `MovementSetupCheck` 菜单做这件事，已删除 —— 它报过 31 项**全部误报**
//   （把 Unity 内置的 m_Material / m_ProbeAnchor 之类当成"未接线"），而假红会训练人忽略它。
//   宁可不查，也不要报一堆假红。所以：本文件绿了只代表 Logic 层对，不代表场景接对了。
//
// 【与射线检测的关系】PlayerMotor 已不含 groundCheck/wallCheck/groundMask：
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
        private PlayerSpec _spec;
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

            // 零惯性基准：本文件里绝大多数用例钉的是"当帧到位 / 松手当帧停"那套语义，
            // 而加速度现在是配置项（玩家资产填 60 = 有惯性）。
            // 要测惯性本身请在自己的用例里显式改这两个值。
            _config.moveAcceleration = 0f;
            _config.turnDecayRate = 0f;

            // 玩家表值：血量 / 无敌帧 / 接触伤害 / 击退。移动测试用不到它们，
            // 但 PlayerLogic 的构造要吃它（账本 PlayerStats 由它初始化）
            _spec = new PlayerSpec(1, "玩家", 100f, 10f, 0.8f, 1.2f, 0.5f, 12f, 12f, 1f);

            _buffer = new InputBuffer(
                Mathf.Max(_config.inputBufferTime, _config.dashBufferTime),
                Mathf.RoundToInt(1f / 0.02f)
                );

            _motor = new RecordingMotor();
            _logic = new PlayerLogic(_motor, _config, _buffer, in _spec);

            _stateChangeCount = 0;
            EventBus<MovementStateChanged>.Subscribe(OnStateChanged);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<MovementStateChanged>.Unsubscribe(OnStateChanged);
            UnityEngine.Object.DestroyImmediate(_config);
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
            var world = new WorldInfo(move, default(BoundsArea));

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
            // 喂**未归一化**的 (1,1)：键盘同时按右与上就是这个值。
            // 归一化是实现的义务，不是测试的前提 —— 喂 0.7071 只能证明"已经归一化的输入能过"，
            // 证明不了"实现会归一化"。曾经这里喂的就是 0.7071，于是斜向快 √2 倍也照样绿。
            Tick(new Vector2(1f, 1f), 0f);

            float expected = _config.moveSpeed * _config.moveSpeed;
            float actual = _motor.Velocity.sqrMagnitude;

            // 容差 0.01：Vector2.normalized 与 magnitude 的浮点误差约 1.2e-3，
            // 而"未归一化"造成的偏差是 +64（(1,1) 会得到 2×speed²），量级差 4 个数量级，不会误判。
            Assert.AreEqual(expected, actual, 0.01f,
                $"斜向速度平方应为 {expected}（= moveSpeed²），实测 {actual}；接近 2×{expected} 即未归一化");
        }

        /// <summary>
        /// 回归：8 向吸附是<b>静态纯函数</b>，喂未归一化输入也必须吐单位向量。
        /// </summary>
        /// <remarks>
        /// 这是本文件里<b>唯一</b>覆盖 <c>PlayerController</c> 那条通路的用例。
        /// 之前那条通路零覆盖，于是"吸附函数恒返回单位向量、注释却说摇杆轻推保持模拟量"
        /// 这种注释与实现相反的问题，测试与校验工具都发现不了。
        /// </remarks>
        [Test]
        public void M17_八向吸附输出单位向量()
        {
            // ① 键盘斜向 (1,1) → 45° 档的单位向量
            Vector2 diagonal = PlayerController.SnapMoveToEightDirections(new Vector2(1f, 1f), true);
            Assert.AreEqual(1f, diagonal.magnitude, 1e-3f, $"键盘斜向应被归一化，实测模长 {diagonal.magnitude}");
            Assert.Greater(Vector2.Dot(diagonal, new Vector2(0.7071f, 0.7071f)), 0.999f,
                $"应吸附到 45°，实测 {diagonal}");

            // ② 直向与摇杆"轻推"都必须得到同一个单位向量：
            //    吸附的语义是"取方向"，代价是丢掉模拟幅度 —— 这条断言把这笔代价钉在代码上。
            Vector2 fullPush = PlayerController.SnapMoveToEightDirections(Vector2.right, true);
            Vector2 lightPush = PlayerController.SnapMoveToEightDirections(new Vector2(0.2f, 0f), true);
            Assert.AreEqual(fullPush, lightPush,
                "摇杆轻推与推满必须是同一个速度：8 向吸附会抹掉模拟幅度，这是已知取舍，不是缺陷");

            // ③ 关掉吸附时也不能原样放行：斜向 (1,1) 的模长是 √2，会当帧写出快 41% 的速度
            Vector2 unsnapped = PlayerController.SnapMoveToEightDirections(new Vector2(1f, 1f), false);
            Assert.AreEqual(1f, unsnapped.magnitude, 1e-3f,
                "关掉吸附只应关掉「吸到 45°」，不该顺带关掉归一化");

            // ④ 零输入恒为零向量：不得因归一化而放大成 NaN
            Assert.AreEqual(Vector2.zero, PlayerController.SnapMoveToEightDirections(Vector2.zero, true));
            Assert.AreEqual(Vector2.zero, PlayerController.SnapMoveToEightDirections(Vector2.zero, false));

            // ⑤ 吸附后的方向必须是"逐位相同"的常量：同一档位换一次输入，值不该抖。
            //    抖动会让 MoveDirection 每帧写出不同的速度，表现为角色在直线上"抖"。
            Assert.AreEqual(fullPush, PlayerController.SnapMoveToEightDirections(new Vector2(5f, 0f), true),
                "同一档位的不同输入必须得到同一个结果：否则直线行走会逐帧抖动");
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
            var go = CreateMotorObject("移动测试_朝向", out PlayerMotor motor, out _);
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
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // 冲刺（改造后语义：沿朝向 8 向 ／ 提交失败不得改动状态）
        // ================================================================

        [Test]
        public void M6_冲刺沿朝向八向()
        {
            // 先建立斜向朝向：喂未归一化的 (1,1)，由实现自己归一到 45°
            Tick(new Vector2(1f, 1f), 0f);
            _motor.Velocity = Vector2.zero;

            // 再触发冲刺（无输入 → 用最近朝向）
            Tick(Vector2.zero, 0.02f, dashPressed: true, resetVelocity: false);

            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState, "有缓冲按下且冷却已过，应抢占到 Dash");

            Vector2 v = _motor.Velocity;
            Assert.AreEqual(_config.dashSpeed, v.magnitude, 1e-3f,
                $"冲刺速率应为 dashSpeed={_config.dashSpeed}；"
                + $"若为 {_config.dashSpeed * Mathf.Sqrt(2f):F2} 量级，说明方向没归一化就乘了速度");

            Vector2 expected = new Vector2(0.7071f, 0.7071f);
            Assert.Greater(Vector2.Dot(v.normalized, expected), 0.999f,
                $"冲刺方向应为玩家朝向 {expected}，实测 {v.normalized}；若等于 (1,0) 说明仍是固定 x 轴冲刺");
        }

        [Test]
        public void M7_冲刺走输入缓冲窗口()
        {
            // 窗口内：可消费
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 0f);

            Assert.IsTrue(_logic.MoveGroup.CanDash(0.01f), "窗口内的冲刺按下应判定为可冲刺");
            Assert.IsTrue(_logic.MoveGroup.TryConsumeDash(0.01f), "首次消费应成功");
            Assert.IsFalse(_logic.MoveGroup.TryConsumeDash(0.01f), "同一次按下只能消费一次");

            // 窗口外：不可消费
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 10f);

            Assert.IsFalse(_logic.MoveGroup.CanDash(10f + _config.dashBufferTime + 0.5f), "超出缓冲窗口的按下必须失效");

            // 冷却内：即使缓冲有按下也不可冲
            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 20f);
            Assert.IsTrue(_logic.MoveGroup.TryConsumeDash(20f), "冷却已过应能消费");

            _buffer.Push(new InputSnapshot(Vector2.zero, false, true, false), 20.1f);
            Assert.IsFalse(_logic.MoveGroup.CanDash(20.1f), "冷却未过时不得再冲");
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

            Assert.IsTrue(_logic.MoveGroup.CanDash(0f), "前置条件：首帧冷却与缓冲都成立");

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
            // 世界范围 [-2, 2] × [-2, 2]：BoundsArea 现在是纯数据，不需要建物体或碰撞体。
            var bounds = new BoundsArea(new Vector2(-2f, -2f), new Vector2(2f, 2f));

            Assert.IsTrue(bounds.IsValid, "尺寸非零的矩形应判定为有效区域");
            Assert.IsTrue(bounds.TryClamp(new Vector2(99f, 0f), out Vector2 clamped), "越界位置必须报告已钳位");
            Assert.AreEqual(2f, clamped.x, 1e-3f, "应被钳到右边界");
            Assert.AreEqual(0f, clamped.y, 1e-3f, "未越界的分量不得被改动");
        }

        [Test]
        public void M11_边界未接线不钳位()
        {
            // ① 默认值（min == max == 零）：组合根在 boundsArea 未接线时传的就是它
            var unset = default(BoundsArea);
            Assert.IsFalse(unset.IsValid, "默认值必须判为无效区域");
            Assert.IsFalse(unset.TryClamp(new Vector2(99f, -99f), out Vector2 kept), "无效区域不得报告钳位");
            Assert.AreEqual(new Vector2(99f, -99f), kept, "无效区域必须原样返回位置");

            // ② 只有一个轴有尺寸（例：BoxCollider2D 的 Size 有一轴是 0）——
            //    只看"有没有传值"会把它当成有效区域，玩家会被钳到一条线上。
            var flat = new BoundsArea(new Vector2(-2f, 0f), new Vector2(2f, 0f));
            Assert.IsFalse(flat.IsValid, "单轴尺寸为 0 必须判无效——否则玩家会被钳到一条线上");
            Assert.IsFalse(flat.TryClamp(new Vector2(99f, -99f), out Vector2 keptFlat));
            Assert.AreEqual(new Vector2(99f, -99f), keptFlat, "无效区域必须原样返回位置");

            // ③ 边界含内、位置恰好在边上：不算钳位（避免每帧写一次位置、打断刚体插值）
            var bounds = new BoundsArea(new Vector2(-2f, -2f), new Vector2(2f, 2f));
            Assert.IsFalse(bounds.TryClamp(new Vector2(2f, -2f), out _), "位置已在边界上时不得报告钳位");
        }

        // ================================================================
        // 执行器端口
        // ================================================================

        [Test]
        public void M12_刚体速度真的被写入()
        {
            var go = CreateMotorObject("移动测试_速度", out PlayerMotor motor, out Rigidbody2D body);
            try
            {
                // 首次访问触发惰性自取与初始化
                motor.Move(new Vector2(3f, -4f));

                Assert.IsTrue(motor.IsInitialized, "首次使用必须完成自取初始化（不依赖 Awake 时机）");
                // 这两条断言只有在创建时**故意写成非默认值**时才有意义：
                // 若创建时就把 gravityScale 设成 0，无论 Initialize 跑没跑断言都成立 —— 那是假绿。
                Assert.AreEqual(0f, body.gravityScale,
                    "俯视角：Initialize 必须把重力缩放写成 0（创建时故意留成 1，才能区分初始化跑没跑）");
                Assert.IsTrue(body.freezeRotation,
                    "俯视角：Initialize 必须冻结旋转（创建时故意留成不冻结）");
                Assert.AreEqual(new Vector2(3f, -4f), body.velocity, "PlayerMotor.Move 必须写进 Rigidbody2D.velocity");
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
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 敌人的物理参数归敌人执行器：连续碰撞检测由 <see cref="EnemyMotor"/> 固化，
        /// 不再写在"造一只敌人"的地方。
        /// </summary>
        /// <remarks>
        /// 与 M12 同一套写法（创建时<b>故意</b>留成非默认值，否则断言恒真 = 假绿）。
        /// 收口前这三条参数写在 <c>EnemyActor.BuildBody</c> 里，而执行器自己一份都不设 ——
        /// "敌人的物理长什么样"因此有两个可能的答案。
        /// </remarks>
        [Test]
        public void M22_敌人执行器固化敌人侧的物理参数()
        {
            var go = new GameObject("移动测试_敌人执行器");

            try
            {
                var body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 1f;
                body.freezeRotation = false;
                body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

                var motor = go.AddComponent<EnemyMotor>();

                motor.EnsureInitialized();

                Assert.IsTrue(motor.IsInitialized);
                Assert.AreEqual(0f, body.gravityScale, "共同的物理参数（重力缩放 0）");
                Assert.IsTrue(body.freezeRotation, "共同的物理参数（冻结旋转）");
                Assert.AreEqual(CollisionDetectionMode2D.Continuous, body.collisionDetectionMode,
                    "敌人侧独有的物理参数必须由敌人的执行器固化");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
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

            // 负上限同样视为不限制（不许把速度翻成反方向 —— Mathf.ClampMagnitude 的负数入参会）
            logic.Limit = -5f;
            logic.Speed = new Vector2(8f, 0f);
            logic.FixedTick(ProbeContext(0.03f));
            Assert.Less(Vector2.Distance(new Vector2(8f, 0f), motor.Velocity), 1e-3f,
                "ClampSpeed(负数) 必须视为不限制，不得把速度反向");

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

            // ④ 上限高于终速 2 → 本帧外力应完整留下。
            //    注意探针的 SetVelocity 走的是 _delta，所以钳制看到的是 (0,-2)+(0,-2) = (0,-4)；
            //    上限用 4.5 留出余量，本段只为验"未触及时不被动过"。
            motor.Velocity = Vector2.zero;
            logic.Scale = 1f;
            logic.Limit = 4.5f;
            logic.FixedTick(ProbeContext(0.06f));
            Assert.Less(Vector2.Distance(new Vector2(0f, -2f), motor.Velocity), 1e-3f,
                "未触及上限的速度不得被钳制改动");

            // ⑤ 同帧超限时被钳回上限
            motor.Velocity = Vector2.zero;
            logic.Limit = 1f;
            logic.FixedTick(ProbeContext(0.08f));
            Assert.Less(Vector2.Distance(new Vector2(0f, -1f), motor.Velocity), 1e-3f,
                "超上限的外力结果应收敛到上限");
        }

        /// <summary>
        /// 回归：<b>先累加外力、后钳制</b> —— 顺序反了本帧外力会被钳制整个吃掉。
        /// </summary>
        /// <remarks>
        /// 为什么必须单独一条：钳制是"接管"而不是"追加"（<c>ClampSpeed</c> 按当帧
        /// <c>Velocity</c> 整体覆盖），所以"顺序"这件事不看中间值就验不出来 ——
        /// 只看帧末输出的话，"先钳后加外力"与"先加外力后钳"在多数参数下给出同一个数。
        /// <para>断言的判据是<b>钳制那一刻看到的速度</b>（探针在 <c>ClampSpeed</c> 之前记一次快照）：
        /// 顺序正确时它必须已经含上外力（<c>0 + 50×0.02 = 1</c>）；
        /// 顺序反了它只会是 <c>0</c>。上限设 1，于是外力被钳掉后帧末只剩 −3，
        /// 与"顺序正确"的 −1 差得足够远，不会误判。</para>
        /// </remarks>
        [Test]
        public void M18_外力必须早于钳制()
        {
            var motor = new RecordingMotor();
            var logic = new LimitProbe(motor);

            logic.Speed = Vector2.zero;
            logic.Scale = 1f;
            logic.Force = new Vector2(0f, -50f);
            logic.Limit = 1f;

            logic.FixedTick(ProbeContext(0f));

            Assert.Less(Vector2.Distance(new Vector2(0f, -1f), logic.VelocityBeforeClamp), 1e-3f,
                $"钳制必须看到已累加的外力（期望 50×0.02=1），实测 {logic.VelocityBeforeClamp}；"
                + "若为 (0,0) 说明钳制跑在 ApplyExtraForce 之前 —— 本帧外力会被整个吃掉且不报错");

            Assert.Less(Vector2.Distance(new Vector2(0f, -1f), motor.Velocity), 1e-3f,
                "钳制看到含外力的速度后，帧末应收敛到上限 1");
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

            /// <summary>钳制那一刻看到的速度（在 <c>ClampSpeed</c> 之前记一次），用来验调用顺序。</summary>
            public Vector2 VelocityBeforeClamp { get; private set; }

            public LimitProbe(IMovementMotor motor) : base(motor, ScriptableObject.CreateInstance<CharacterConfig>())
            {
            }

            protected override void OnTick(in LogicContext ctx)
            {
                SetVelocity(Speed);
                SetExtraForceScale(Scale);
                ApplyExtraForce(in ctx, Force);

                VelocityBeforeClamp = Velocity;

                ClampSpeed(Limit);
            }
        }

        private static LogicContext ProbeContext(float now)
        {
            var snapshot = InputSnapshot.Empty;
            var world = new WorldInfo(Vector2.zero, default(BoundsArea));

            return new LogicContext(now, 0.02f, in world, in snapshot);
        }

        // ================================================================
        // 辅助
        // ================================================================

        /// <summary>
        /// 建一个 PlayerMotor 物体：Rigidbody2D ＋ 组件，接线与场景一致。
        /// </summary>
        /// <remarks>
        /// 顺序不能反：先加 Rigidbody2D 再加 PlayerMotor，不依赖 <c>RequireComponent</c> 的自动补加顺序。
        /// <para><b>物理参数故意留成非默认值</b>（<c>gravityScale = 1</c> / 不冻旋转）：
        /// 只有这样才能区分"<c>Initialize()</c> 真的跑了"与"值恰好就是默认的 0/false"。
        /// 早先这里写的是 <c>body.gravityScale = 0f</c>，于是 M12 的两条断言恒真 —— 假绿。</para>
        /// <para><b>注意 EditMode 下 <c>AddComponent</c> 不会触发 <c>Awake</c></b>，所以本类不能依赖
        /// <c>Awake</c> 里的自取与自检——<c>PlayerMotor</c> 的物理体引用因此做成惰性兜底（见其 <c>Body</c> 属性）。
        /// 这也意味着 <c>gravityScale = 0</c> / <c>freezeRotation</c> 那两行在 EditMode 测试里
        /// 只有通过"首次访问属性"这条惰性路径才会执行 —— M12 走的正是那条路径。</para>
        /// </remarks>
        private static GameObject CreateMotorObject(string name, out PlayerMotor motor, out Rigidbody2D body)
        {
            var go = new GameObject(name);

            body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 1f;          // 非默认：断言"被 Initialize 改成 0"才有意义
            body.freezeRotation = false;     // 非默认：同上

            motor = go.AddComponent<PlayerMotor>();

            return go;
        }

        // ================================================================
        // M19~M21 · 惯性（配置驱动）与门禁
        // ================================================================

        /// <summary>
        /// 有惯性时：加速受 <c>moveAcceleration</c> 限制，松手按同一个加速度滑停。
        /// </summary>
        /// <remarks>
        /// 这两条合起来才是"惯性"的完整定义 —— 只测加速会让"松手当帧停"这种半吊子实现蒙混过关。
        /// 数值取 1 帧（0.02s）× 加速度，所以断言是解析解而不是"看起来变了"。
        /// </remarks>
        [Test]
        public void M19_有惯性时加速与滑停都受加速度限制()
        {
            _config.moveAcceleration = 20f;   // 20 单位/秒² × 0.02s = 每帧 0.4
            _config.turnDecayRate = 0f;

            // 有惯性时"帧首速度"就是积分状态，所以这里**不能**每帧把执行器速度清零
            // （清零等于每帧都从零开始，永远加不上去）
            Tick(Vector2.right, 0f, resetVelocity: false);

            Assert.AreEqual(0.4f, _motor.Velocity.x, 1e-3f,
                "第一帧只能加到 加速度 × Δt；直接等于 moveSpeed 说明状态还在当帧接管速度");

            // 再跑 19 帧：0.4 × 20 = 8 = moveSpeed，之后不再涨
            for (int i = 1; i <= 19; i++) Tick(Vector2.right, i * 0.02f, resetVelocity: false);

            Assert.AreEqual(_config.moveSpeed, _motor.Velocity.x, 1e-3f, "持续按住应收敛到 moveSpeed");

            // 松手：按同一个加速度滑停（8 / 20 = 0.4 秒 = 20 帧），不是当帧归零
            Tick(Vector2.zero, 0.4f, resetVelocity: false);

            Assert.AreEqual(_config.moveSpeed - 0.4f, _motor.Velocity.x, 1e-3f,
                "松手第一帧应当只掉 加速度 × Δt；直接归零说明走的是当帧急停");

            for (int i = 1; i <= 25; i++) Tick(Vector2.zero, 0.4f + i * 0.02f, resetVelocity: false);

            Assert.AreEqual(0f, _motor.Velocity.x, 1e-3f, "滑够时间必须真的停下");
        }

        /// <summary>
        /// 惯性不改变冲刺的语义：冲刺进入时仍然当帧接管速度。
        /// </summary>
        /// <remarks>冲刺是"定时恒速"，与走路的加减速是两套语义；有惯性时它不该被"先加速再到达"。
        /// 这条是给"把 <c>SnapVelocity</c> 也改成渐进逼近"这种改法准备的钉子。</remarks>
        [Test]
        public void M20_惯性不改变冲刺的当帧接管()
        {
            _config.moveAcceleration = 20f;
            _config.turnDecayRate = 0f;

            var snap = new InputSnapshot(Vector2.right, false, true, false);
            _buffer.Push(in snap, 0f);

            Tick(Vector2.right, 0f, dashPressed: true);

            Assert.AreEqual(MovementStateTag.Dash, _logic.CurrentState);
            Assert.AreEqual(_config.dashSpeed, _motor.Velocity.magnitude, 1e-3f,
                "冲刺必须当帧到达 dashSpeed，而不是按走路加速度爬上去");
        }

        /// <summary>
        /// 受击期间：门禁接管速度，输入完全不生效。
        /// </summary>
        /// <remarks>
        /// 门禁由状态效果层产出（受击状态），在移动层的状态跑完之后统一施加 ——
        /// 写在状态之前会被 <c>SnapVelocity</c> 覆盖掉，而那正是旧实现"挨打了却纹丝不动"的成因之一。
        /// </remarks>
        [Test]
        public void M21_受击期间输入不接管速度()
        {
            _config.moveAcceleration = 0f;    // 让击退一帧到位、便于断言

            var damage = new DeepseaOil.Logic.Combat.Damage(
                Vector2.zero,
                0f,                                             // 只推不扣血
                DeepseaOil.Logic.Combat.DamageSource.Contact,
                Vector2.right,
                12f);

            Assert.IsTrue(_logic.TakeDamage(in damage, 0f), "纯击退也该生效");

            // 这一帧玩家正按着"上"
            Tick(Vector2.up, 0.02f);

            Assert.AreEqual(12f, _motor.Velocity.x, 1e-3f, "受击帧的速度由门禁决定");
            Assert.AreEqual(0f, _motor.Velocity.y, 1e-3f, "输入不该在受击帧生效");
        }
    }
}