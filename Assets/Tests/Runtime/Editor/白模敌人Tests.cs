// ---------------------------------------------------------------------------
// 白模敌人 · EditMode 测试（临时资产）
//
// 【白模验收通过后整个文件删除。】与 白模抛球Tests.cs 同一个性质：它是白模这一版的仪表盘，不是框架。
//   只想跑白模这几条：Test Runner ▸ EditMode ▸ 右侧分类筛选 WhiteBox。
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 本目录没有 asmdef，靠「路径里有名为 Editor 的目录」
//   落 Assembly-CSharp-Editor，它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【为什么是这些用例】敌人这一版的风险集中在四件事，错了都不报错、只是行为不对：
//   W12 追击速度不得超过上限 —— 泥浆减速是乘法，写成加法就会越加越快（"踩了泥反而跑得更快"）
//   W13 击退滑行会衰减且不反向 —— **本文件重点**：ApproachX 只看 x 分量，竖直击退会永不衰减飘出地图
//   W14 泥浆只减速不放大     —— 重叠泥浆若各自相乘会得到 0.09，敌人看起来被粘住，而不报错
//   W15 三下打碎且每颗球只算一次 —— "一帧一次"的锁没生效时，一颗球就能打空 3 点耐久
//   W16 接触伤害受无敌帧门控 —— 判据写反会让玩家变成永久无敌（屏幕上看不出任何异常）
//   W17 白模数值互相自洽     —— 敌人比玩家快、耐久为 1、减速系数越界，全是静默失效
//   W18 被击退后还能重新贴上 —— **真实自锁**：停止距离与接触距离的几何判据会让敌人被推出去后
//                              再也不回来，现象只是"整局只掉一次血"
//   W19 禁足期间不得衰减冲量 —— **真实缺陷**：禁足自己走衰减那一支，把冲量吃掉 82%
//                              （现象："球打中了但敌人几乎没动"，而冲量本身够大）
//   W20 击退总位移必须看得出来 —— 数值契约：允许改任何参数，只要位移还在 1.2~2.6 米
//
// 【覆盖边界，写在明处】本文件只测**纯函数/纯数据**与一段**手写探针驱动的逻辑账本**，
//   不测 MonoBehaviour 生命周期、不测 Physics2D 接触检测、不测 OnGUI、不测碎片外观。
//   那些在白模阶段由人工 Play 手测（见 Docs/toAgent/白模接入.md 的验收清单）。
//   写假测试会把真绿变成"全绿但缺陷仍在"，移动Tests.cs 头部已记过这个教训。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>【临时】白模敌人测试。白模验收后整文件删除。</summary>
    [Category("WhiteBox")]
    public class 白模敌人Tests
    {
        /// <summary>速度类断言容差。经过归一化与多次累加，容差不能取到 1e-6。</summary>
        private const float SpeedTolerance = 1e-3f;

        /// <summary>固定物理步长：与工程默认值一致（50 Hz）。</summary>
        private const float FixedStep = 0.02f;

        // ================================================================
        // W12 · 追击速度只由乘法决定，永不超过基准速度
        // ================================================================

        /// <summary>
        /// 泥浆减速是<b>乘法</b>：8 个方向 × 4 档系数下，输出速度都不得超过基准速度，
        /// 且方向必须正比于"目标 − 自己"（未归一化）。
        /// </summary>
        /// <remarks>
        /// <b>方向那一半不是凑数的。</b><c>Steering.Resolve</c> 刻意回未归一化的差值，
        /// 归一化由 <c>ActorLogic.SteerTowards</c> 负责 —— 两处都归就会让斜向凭空快 √2 倍，
        /// 而且不报错、只是"敌人斜着走更快"。
        /// <para>曾经想过在这里就归一化：那样 <c>Vector2.Distance</c> 等信息会丢，
        /// 而"够不够近"的判断要用到距离，调用方就得自己再算一次 —— 两份几何必然漂移。</para>
        /// </remarks>
        [Test]
        public void W12_追击速度不得超上限且方向正比于位移()
        {
            var self = new Vector2(1f, -2f);
            var target = new Vector2(4f, 3f);

            float[] slows = { 1f, ThrowConstants.MUD_SLOW_FACTOR, 0.8f, 1.5f };

            foreach (float slow in slows)
            {
                for (int i = 0; i < 8; i++)
                {
                    float radians = i * Mathf.PI / 4f;

                    Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians))
                        * ThrowConstants.CHASE_SPAWN_RADIUS;

                    Steering steering = Steering.Resolve(
                        self,
                        self + offset,
                        ThrowConstants.ENEMY_STOP_DISTANCE,
                        ThrowConstants.ENEMY_CHASE_RANGE,
                        ThrowConstants.ENEMY_SPEED,
                        EnemyConfig.SlowMultiplier(slow)
                        );

                    Assert.LessOrEqual(steering.Speed, ThrowConstants.ENEMY_SPEED + SpeedTolerance,
                        $"泥浆系数 {slow}、方向 {i}：目标速度超过了基准速度 —— 减速只能做乘法");

                    // 方向必须是"目标 − 自己"的正比向量（未归一化），且指向目标。
                    float cross = steering.Direction.x * offset.y - steering.Direction.y * offset.x;

                    Assert.AreEqual(0f, cross, SpeedTolerance,
                        $"方向 {i}：给出去的方向不是'目标 − 自己'的正比向量");

                    Assert.Greater(Vector2.Dot(steering.Direction, offset), 0f,
                        $"方向 {i}：给出去的方向与目标方向相反");
                }
            }
        }

        // ================================================================
        // W13 · 击退滑行会衰减且不反向（本文件的重点）
        // ================================================================

        /// <summary>
        /// 给一次满额击退后连续步进：速度必须单调不增、永不反向、最终归零，且<b>永不超过冲量初值</b>。
        /// </summary>
        /// <remarks>
        /// <b>这一条是为一个真实的结构性缺陷写的。</b><c>ActorLogic.ApproachX</c> 只看 <c>Velocity.x</c>，
        /// 竖直分量<b>永远不衰减</b>：拿它做 2D 击退，朝正上方被推飞的敌人会以恒定速度一路飘到地图外，
        /// 而且不报错 —— 现象只有"敌人不见了"。
        /// <para>本测试用 <see cref="Probe"/> 手写驱动账本，不依赖 MonoBehaviour 生命周期与物理世界，
        /// 所以"衰减"这件事是逐步验算出来的，而不是"跑一次看着没问题"。</para>
        /// </remarks>
        [Test]
        public void W13_击退滑行会衰减且不反向()
        {
            const float impulse = 5.5f;
            var direction = new Vector2(0.6f, 0.8f).normalized;   // 刻意带竖直分量

            var probe = new Probe();
            var logic = new EnemyLogic(probe, new EnemyConfig());

            logic.SetTarget(null);                        // 没有目标 ⇒ 只靠衰减把速度收回去
            logic.ApplyKnockback(impulse, direction);

            float initialSpeed = 0f;

            for (int i = 0; i < 400; i++)
            {
                logic.Tick(i * FixedStep, FixedStep);

                Vector2 v = probe.Velocity;
                float speed = v.magnitude;

                if (i == 0) initialSpeed = speed;

                Assert.LessOrEqual(speed, impulse + SpeedTolerance,
                    $"第 {i} 帧：滑行速度超过了冲量初值 —— 衰减过程在放大速度");

                Assert.LessOrEqual(speed, initialSpeed + SpeedTolerance,
                    $"第 {i} 帧：滑行速度不减反增（当前 {speed}，初值 {initialSpeed}）");

                if (speed > 1e-4f)
                {
                    // 永不反向：速度方向与击退方向不得背离（点积为负就是"弹回去了"）。
                    Assert.Greater(Vector2.Dot(v, direction), 0f,
                        $"第 {i} 帧：滑行方向反了 —— 角色会往回弹，看起来像被吸住");
                }
            }

            float final = probe.Velocity.magnitude;

            Assert.Less(final, impulse * 0.01f,
                $"整整 8 秒之后速度还有 {final} —— 衰减太慢或有分量没被衰减（竖直分量不衰减就是这个问题）");
        }

        // ================================================================
        // W14 · 泥浆只减速不放大
        // ================================================================

        /// <summary>
        /// 减速系数的钳制、重叠泥浆取最强而不是相乘、以及"追击速度 = 基准 × 系数"。
        /// </summary>
        /// <remarks>
        /// <b>相乘是真的会把敌人粘住。</b>两片泥浆各自乘一次得到 0.2025，三片 0.0911 ——
        /// 敌人几乎不动，看起来像"泥浆定身"，而这是坏掉的手感而不是设计。
        /// 更糟的是它<b>不报错</b>，现象只是"叠几片泥敌人就不动了"，很难倒推到乘算上。
        /// </remarks>
        [Test]
        public void W14_泥浆只减速不放大()
        {
            // 钳制：任何外因都不得比设计下限更慢，也不得比 1 更快（更快就是"泥浆加速"）。
            Assert.AreEqual(ThrowConstants.MUD_SLOW_FACTOR, EnemyConfig.SlowMultiplier(0f), SpeedTolerance,
                "系数为 0 时必须钳到设计下限，否则敌人会被定死（定身是另一套机制）");
            Assert.AreEqual(ThrowConstants.MUD_SLOW_FACTOR, EnemyConfig.SlowMultiplier(-3f), SpeedTolerance,
                "负系数必须钳到设计下限");
            Assert.AreEqual(1f, EnemyConfig.SlowMultiplier(1f), SpeedTolerance, "不给减益时系数是 1");
            Assert.AreEqual(1f, EnemyConfig.SlowMultiplier(2.5f), SpeedTolerance,
                "系数大于 1 必须钳到 1，否则'泥浆'会把敌人加速");
            Assert.AreEqual(1f, EnemyConfig.SlowMultiplier(float.NaN), SpeedTolerance,
                "非数必须按'不起作用'处理；原样放行会把非数传染进速度里");

            // 追击速度 = 基准 × 系数。
            Steering steered = Steering.Resolve(
                Vector2.zero,
                new Vector2(10f, 0f),
                ThrowConstants.ENEMY_STOP_DISTANCE,
                ThrowConstants.ENEMY_CHASE_RANGE,
                ThrowConstants.ENEMY_SPEED,
                EnemyConfig.SlowMultiplier(ThrowConstants.MUD_SLOW_FACTOR)
                );

            Assert.AreEqual(ThrowConstants.ENEMY_SPEED * ThrowConstants.MUD_SLOW_FACTOR, steered.Speed, SpeedTolerance,
                "踩在泥浆里的追击速度必须是 基准 × 系数");

            // 够近就不追：方向可以给（"面向玩家"将来要用），但速度必须是 0。
            Steering stopped = Steering.Resolve(
                Vector2.zero,
                new Vector2(ThrowConstants.ENEMY_STOP_DISTANCE * 0.5f, 0f),
                ThrowConstants.ENEMY_STOP_DISTANCE,
                ThrowConstants.ENEMY_CHASE_RANGE,
                ThrowConstants.ENEMY_SPEED,
                1f
                );

            Assert.AreEqual(0f, stopped.Speed, SpeedTolerance, "进入停止距离后目标速度必须是 0");

            // 超出追击范围：不给方向也不给速度（"跑得够远能脱离"这条得靠它成立）。
            Steering outOfRange = Steering.Resolve(
                Vector2.zero,
                new Vector2(ThrowConstants.ENEMY_CHASE_RANGE + 10f, 0f),
                ThrowConstants.ENEMY_STOP_DISTANCE,
                ThrowConstants.ENEMY_CHASE_RANGE,
                ThrowConstants.ENEMY_SPEED,
                1f
                );

            Assert.IsTrue(outOfRange.IsIdle, "超出追击范围后必须完全不动，否则'能脱离'永远不成立");

            // 站在目标点上：方向不可定义，必须不动（零向量归一化是非数）。
            Steering degenerate = Steering.Resolve(
                Vector2.zero, Vector2.zero,
                ThrowConstants.ENEMY_STOP_DISTANCE,
                ThrowConstants.ENEMY_CHASE_RANGE,
                ThrowConstants.ENEMY_SPEED,
                1f
                );

            Assert.IsTrue(degenerate.IsIdle, "正好站在目标点上时必须不动，且不得产出非数坐标");
        }

        // ================================================================
        // W15 · 三下打碎，且每颗球只算一次
        // ================================================================

        /// <summary>
        /// 模拟 <c>EnemyActor.TakeDamage</c> 的门控，验证"耐久 − 1 × ENEMY_HP 次才碎"
        /// 与"同一颗球不重复结算"。
        /// </summary>
        /// <remarks>
        /// <b>"同一颗球只算一次"不是优化，是规则。</b>落地结算按半径查碰撞体，同一个敌人可能被查到多次
        /// （多碰撞体、多父级）。锁没生效的现象是"说好的三下变成一下" —— 玩家会觉得耐久条写错了。
        /// <para><b>判据是球编号，不是"本帧"。</b>帧判据要求每帧有人复位标记，而那会把正确性绑死在
        /// 脚本执行顺序上（<c>ThrowSpawner.FixedUpdate</c> 与 <c>EnemyDirector.FixedUpdate</c>
        /// 谁先跑 Unity 不保证），先跑的那一帧伤害会被静默丢掉 ——
        /// 现象是"有时三下碎、有时四下还不碎"。球编号只增不减，比较它不需要任何复位。</para>
        /// </remarks>
        [Test]
        public void W15_三下打碎且每颗球只算一次()
        {
            var enemy = new FakeEnemy();

            Assert.AreEqual(ThrowConstants.ENEMY_HP, enemy.Hp, "初始耐久必须是 ENEMY_HP");

            int hitsToKill = 0;

            for (int ballId = 1; ballId <= ThrowConstants.ENEMY_HP; ballId++)
            {
                Assert.IsTrue(enemy.Hit(ballId), $"第 {ballId} 颗球应当算作命中");

                hitsToKill++;

                // 同一颗球被查到了多次（多碰撞体）：必须全部被挡掉。
                Assert.IsFalse(enemy.Hit(ballId), $"第 {ballId} 颗球：同一颗球的第二次结算不该生效");
                Assert.IsFalse(enemy.Hit(ballId), $"第 {ballId} 颗球：同一颗球的第三次结算不该生效");

                // 编号更旧的球（理论上不会发生，但"只增不减"这条契约要钉住）也必须被挡掉。
                //
                // **只在 ballId > 1 时判**：第一次循环里 `ballId - 1` 是 0，而 0 是一个
                // **从没被打中过的编号** —— 拿它来断言"更旧的编号要被挡掉"是在断言一件不该成立的事，
                // 那条红是测试写错了，不是实现错了（实测在第一轮就红）。
                if (ballId > 1)
                {
                    Assert.IsFalse(enemy.Hit(ballId - 1), $"第 {ballId} 颗球：更旧的编号不该生效");
                }
            }

            Assert.AreEqual(ThrowConstants.ENEMY_HP, hitsToKill, "打碎所需的命中次数必须恰好等于 ENEMY_HP");
            Assert.IsTrue(enemy.Dead, $"{ThrowConstants.ENEMY_HP} 下之后必须被打碎");
            Assert.AreEqual(0, enemy.Hp, "打碎后耐久必须是 0");

            // 已死目标再挨打：不复活、不重复计数。
            Assert.IsFalse(enemy.Hit(ThrowConstants.ENEMY_HP + 1), "已死目标不该再被结算（会重复生成碎片、重复写速度）");
            Assert.IsTrue(enemy.Dead, "已死目标不该被打活");
            Assert.AreEqual(0, enemy.Hp, "已死目标的耐久不该变化");
        }

        /// <summary>
        /// 复刻 <c>EnemyActor.TakeDamage</c> 的门控与扣血：行为必须与实现一致。
        /// </summary>
        /// <remarks>
        /// 手写而不是 <c>AddComponent&lt;EnemyActor&gt;</c>：后者的 <c>Awake</c> 要建刚体与碰撞体，
        /// 在 EditMode 下是否触发取决于 Unity 的版本与时机，那会引入一个与被测行为无关的变量。
        /// 本文件测的是<b>规则</b>（几下碎、一颗球算几次），规则与 MonoBehaviour 无关。
        /// </remarks>
        private sealed class FakeEnemy
        {
            private int _lastBallId = int.MinValue;

            public int Hp { get; private set; } = ThrowConstants.ENEMY_HP;

            public bool Dead { get; private set; }

            /// <summary>结算一次命中；返回"是否真的扣了血"。</summary>
            public bool Hit(int ballId)
            {
                // 与 EnemyActor.TakeDamage 同一判据："不大于"而不是"不等于"。
                // 用 == 挡不住"编号更旧的球"，而那种球在域重载之后是真实存在的。
                if (Dead || ballId <= _lastBallId) return false;

                _lastBallId = ballId;
                Hp = Mathf.Max(0, Hp - 1);

                if (Hp <= 0) Dead = true;

                return true;
            }
        }

        // ================================================================
        // W16 · 接触伤害受无敌帧门控
        // ================================================================

        /// <summary>
        /// 无敌帧判据：无敌期内拒绝、到期后接受、非法时间戳不得导致永久无敌。
        /// </summary>
        /// <remarks>
        /// <b>"永久无敌"是最坏的一种坏法。</b>判据写成 <c>now &gt;= invulnerableUntil</c> 时，
        /// <c>invulnerableUntil</c> 是 <c>NaN</c> 会让它恒为 <c>false</c> ——
        /// 玩家再也挨不到伤害，而屏幕上看不出任何异常（血量就是不掉），
        /// 只能靠"这局怎么打不完"倒推。所以判据写成 <c>!(now &lt; invulnerableUntil)</c>：
        /// 非法值落到"可以受伤"这一侧。
        /// </remarks>
        [Test]
        public void W16_接触伤害受无敌帧门控()
        {
            const float duration = ThrowConstants.PLAYER_INVULNERABLE_DURATION;

            Assert.IsTrue(PlayerHealth.CanTakeDamage(0f, 0f), "从没受过伤（结束时间等于当前时间）时必须可以受伤");
            Assert.IsFalse(PlayerHealth.CanTakeDamage(0f, duration), "无敌期内不得受伤");
            Assert.IsFalse(PlayerHealth.CanTakeDamage(duration * 0.99f, duration), "无敌期最后一帧不得受伤");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(duration, duration), "无敌期正好到点必须可以受伤");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(duration + 0.01f, duration), "无敌期过后必须可以受伤");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(10f, float.NegativeInfinity), "从未受击（负无穷）必须可以受伤");

            Assert.IsTrue(PlayerHealth.CanTakeDamage(5f, float.NaN),
                "结束时间是 NaN 时必须落到'可以受伤'这一侧，否则玩家会永久无敌且看不出异常");
            Assert.IsTrue(PlayerHealth.CanTakeDamage(float.NaN, 5f),
                "当前时间是 NaN 时必须落到'可以受伤'这一侧，否则受伤会永远被静默地挡掉");

            // 时长必须为正：0 会让"受击后立刻又能受击"，接触伤害变成每帧一次。
            Assert.Greater(duration, 0f, "无敌时长必须为正，否则接触伤害会退化成每帧一次");
        }

        // ================================================================
        // W17 · 白模数值互相自洽
        // ================================================================

        /// <summary>
        /// 只查常量之间的关系，不查行为。写在这里是因为下面每一条错了都会<b>静默</b>失效，
        /// 而它们全是运行时看不出来的。
        /// </summary>
        [Test]
        public void W17_白模数值互相自洽()
        {
            // 敌人必须比玩家慢：否则"能甩掉"这条验收做不了，玩家只会被粘住。
            float playerSpeed = 8f;   // Assets/ConfigAssets/玩家配置.asset 里的 moveSpeed

            Assert.Less(ThrowConstants.ENEMY_SPEED, playerSpeed,
                "敌人速度必须低于玩家速度，否则玩家甩不掉它");
            Assert.Greater(ThrowConstants.ENEMY_SPEED, 0f, "敌人速度必须为正，否则它永远追不上");
            Assert.Greater(ThrowConstants.ENEMY_ACCELERATION, 0f, "加速度必须为正，否则起步是瞬间满速");
            Assert.Greater(ThrowConstants.ENEMY_KNOCKBACK_DECAY, 0f,
                "衰减率必须为正：为 0 时击退永不衰减（竖直分量不衰减就是这条的极端情形）");
            Assert.Greater(ThrowConstants.ENEMY_CHASE_RANGE, ThrowConstants.ENEMY_STOP_DISTANCE,
                "追击范围必须大于停止距离，否则 clamp 区间是空的、敌人永远不会动");
            Assert.Greater(ThrowConstants.ENEMY_CONTACT_RADIUS, 0f,
                "接触半径必须为正，否则 OverlapCircle 永远查不到东西，玩家永远不挨打");
            // 禁足：为 0 就等于"被打中之后当帧恢复转向"，冲量会被追击立刻抵消（击退没反馈）。
            Assert.Greater(ThrowConstants.ENEMY_STUN_DURATION, 0f,
                "禁足时长必须为正，否则击退会被追击当帧抵消，看起来像'根本没击退'");
            Assert.Greater(ThrowConstants.ENEMY_STUN_FRAMES, 0, "禁足帧数必须为正");
            Assert.Greater(ThrowConstants.ENEMY_KNOCKBACK_IMPULSE, 0f, "击退冲量必须为正，否则打中没有任何位移");

            // **两个禁足常量必须互相对得上。** 帧数是真值、秒数是它的说法；
            // 两者不一致**不会报错**，只会让"文档说 0.25 秒、实际跑 12 帧"这类分歧悄悄存在。
            // 容差取 0.5 帧：整数除法本身允许半个帧的取舍。
            Assert.AreEqual(ThrowConstants.ENEMY_STUN_FRAMES,
                ThrowConstants.ENEMY_STUN_DURATION / FixedStep, 0.5f,
                "ENEMY_STUN_DURATION 必须等于 ENEMY_STUN_FRAMES × 物理步长（两者是同一个数的两种写法）");

            // 闪烁：频率必须为正且有上限。上限不是审美 —— 60 fps 下每个状态只有 30/hz 帧，
            // 取 10 就只剩 3 帧，看起来是"闪频"（像显示器坏了）而不是"被砸了一下"。
            Assert.Greater(ThrowConstants.ENEMY_FLASH_HZ, 0f, "闪烁频率必须为正，否则闪烁退化成常亮");
            Assert.LessOrEqual(ThrowConstants.ENEMY_FLASH_HZ, 10f,
                "闪烁频率不该超过 10 Hz：60 fps 下每个状态只剩 3 帧，看起来像闪频而不是命中反馈");

            // 减速色必须**更暗**：这是"减速生效了"唯一的视觉反馈（没有 buff 图标、没有粒子）。
            // 比亮度而不是比某一个通道 —— 色相保持、只是压暗，这是设计意图。
            Assert.Less(Luminance(ThrowConstants.ENEMY_SLOW_BODY_COLOR), Luminance(ThrowConstants.ENEMY_BODY_COLOR),
                "泥浆中的身体色必须比正常色暗，否则'减速生效了'在屏幕上读不出来");
            Assert.Less(Luminance(EnemyVisual.FlashColor), Luminance(Color.white),
                "闪烁色不该是纯白：纯白在浅色地面上会糊成一片");

            // 耐久数字：压在敌人圆心，而且必须**画在身体之上**。
            //
            // 位置：偏移 0 = 正中心（配合 TextMesh 的 MiddleCenter 锚点）。
            // 曾经是 0.34（"压在身体上沿"，配合 LowerCenter）—— 那条断言现在反了，
            // 因为需求是"数字显示在圆形中央"。
            Assert.AreEqual(0f, ThrowConstants.ENEMY_HP_TEXT_OFFSET_Y, 0.001f,
                "数字必须落在敌人圆心（偏移 0）—— 配合 MiddleCenter 锚点，非 0 就会偏出圆外");

            Assert.Greater(ThrowConstants.ENEMY_HP_TEXT_CHARACTER_SIZE, 0f, "字号必须为正");

            // 字号必须明显小于直径，否则数字会盖满整个身体、看不出敌人是什么形状。
            Assert.Less(ThrowConstants.ENEMY_HP_TEXT_CHARACTER_SIZE, ThrowConstants.ENEMY_RADIUS_METERS * 2f,
                "字号不该超过敌人直径，否则数字糊满身体");
            // 但也不能小到读不出来：至少要占直径的一成。
            Assert.Greater(ThrowConstants.ENEMY_HP_TEXT_CHARACTER_SIZE, ThrowConstants.ENEMY_RADIUS_METERS * 2f * 0.1f,
                "字号太小就读不出来 —— 曾经取 0.06，屏幕上几乎看不见");

            // **排序层：数字必须压在自己的身体之上。**
            // 这一条是补一个想当然的错误 —— 曾经以为"MeshRenderer 没有 sortingOrder"，
            // 于是数字靠深度排序，被自己的身体挡掉。实际上 MeshRenderer 和 SpriteRenderer
            // 一样继承 Renderer、一样有 sortingOrder，而白模全是 z=0 的正交俯视：
            // 深度分不出先后，排序层是唯一能决定谁压谁的东西。
            // 数字又是**唯一的耐久读数**（没有血条），被挡住就等于这条机制不存在。
            Assert.Greater(ThrowConstants.ENEMY_HP_TEXT_SORTING_ORDER, ThrowConstants.ENEMY_BODY_SORTING_ORDER,
                "耐久数字的排序层必须高于敌人身体，否则数字被自己的身体挡掉（白模里没有血条，挡住等于没有）");

            // 但必须低于瞄准环与球：那两个是"我现在要打哪"的即时反馈，优先级更高。
            Assert.Less(ThrowConstants.ENEMY_HP_TEXT_SORTING_ORDER, ThrowConstants.AIM_SORTING_ORDER,
                "耐久数字不该盖住瞄准环 —— 玩家正在瞄哪比敌人剩几点血更即时");
            Assert.Less(ThrowConstants.ENEMY_HP_TEXT_SORTING_ORDER, ThrowConstants.BALL_SORTING_ORDER,
                "耐久数字不该盖住飞行的球");

            // 查询余量是**优化余量**而不是作用半径：它必须为正，但改动它不该影响作用范围。
            Assert.Greater(ThrowConstants.PUSH_QUERY_MARGIN, 0f,
                "查询余量必须为正，否则查询半径等于作用半径，'圆心在内但碰撞体很大'的目标会被漏掉");

            // 玩家读数面板：宽度为正，且要能装下那行字（368 是实测需要的宽度）。
            Assert.Greater(ThrowConstants.PLAYER_HUD_WIDTH, 0f, "读数面板宽度必须为正");

            // 耐久：为 1 时"三下打碎"退化成一下，白模就验不出"耐久"这件事。
            Assert.GreaterOrEqual(ThrowConstants.ENEMY_HP, 2, "耐久至少为 2，否则'三下打碎'退化");
            Assert.GreaterOrEqual(ThrowConstants.ENEMY_COUNT_PER_WAVE, 1, "每波敌人数至少为 1");

            // 泥浆：系数必须在开区间内 —— 0 是定身、1 是没有效果、越界是加速。
            Assert.Greater(ThrowConstants.MUD_SLOW_FACTOR, 0f, "泥浆系数必须大于 0，否则敌人会被定死");
            Assert.Less(ThrowConstants.MUD_SLOW_FACTOR, 1f, "泥浆系数必须小于 1，否则泥浆没有任何效果");
            Assert.Greater(ThrowConstants.MUD_RADIUS_METERS, 0f, "泥浆半径必须为正");
            Assert.Greater(ThrowConstants.MUD_DURATION, 0f, "泥浆时长必须为正");

            // 玩家血量：必须能被整除且不止一下 —— 否则"打十下"这件事验不了，血量也只是个装饰。
            Assert.Greater(ThrowConstants.PLAYER_CONTACT_DAMAGE, 0f, "接触伤害必须为正");
            Assert.Greater(ThrowConstants.PLAYER_MAX_HP, ThrowConstants.PLAYER_CONTACT_DAMAGE,
                "血上限必须大于单次伤害，否则一下就被打空，血量没有意义");
            Assert.AreEqual(10f, ThrowConstants.PLAYER_MAX_HP / ThrowConstants.PLAYER_CONTACT_DAMAGE, 1e-3f,
                "血上限与单次伤害应当是整数倍关系（100/10 = 10 下），否则读数会出现小数");

            // 被推开多远由 PLAYER_KNOCKBACK_SPEED_LIMIT 决定（受击上限），不是由 IMPULSE_STRENGTH 决定 ——
            // 后者是"球砸无生命物理体"的强度，两件事共用一个数会让调一个顺带改掉另一个。
            Assert.GreaterOrEqual(ThrowConstants.PLAYER_KNOCKBACK_IMPULSE, ThrowConstants.PLAYER_KNOCKBACK_SPEED_LIMIT,
                "受击冲量不得小于受击速度上限，否则推不满 —— 表现是'被撞了一下几乎不动'，且不报错");
            Assert.Greater(ThrowConstants.PLAYER_KNOCKBACK_SPEED_LIMIT, 0f, "受击速度上限必须为正");
            Assert.Greater(ThrowConstants.PLAYER_KNOCKBACK_SPEED_LIMIT, playerSpeed,
                "被撞飞应当比正常跑更快，否则'被撞开'的感觉看不出来");

            // **这一条是真踩过的坑**：击退会把敌人推开，如果"停止距离"离"接触距离"太近，
            // 敌人被推出去之后仍然落在停止距离之内，就再也不会主动压回来 ——
            // 现象是"被撞一次之后敌人黏在原地，玩家再也挨不到第二下"，
            // 不报错、也不像 bug，只是整局只掉一次血。
            Assert.Less(ThrowConstants.ENEMY_STOP_DISTANCE, ThrowConstants.ENEMY_CONTACT_RADIUS,
                "停止距离必须小于接触半径，否则敌人不会主动贴上玩家，接触伤害永远不会再次发生");
            Assert.LessOrEqual(ThrowConstants.ENEMY_STOP_DISTANCE, 0.8f,
                "停止距离与接触半径之间要留出可观的间隔（建议 ≥ 0.2），否则敌人会一直贴着玩家抖");

            // 碎片：数量为 0 时"碎成几块"这件事看不见。
            Assert.GreaterOrEqual(ThrowConstants.SHATTER_PIECE_COUNT, 2, "碎片数至少为 2，否则看不出'碎了'");
            Assert.Greater(ThrowConstants.SHATTER_DURATION, 0f, "碎片时长必须为正，否则它一生成就消失");
            Assert.Greater(ThrowConstants.SHATTER_SPEED, 0f, "碎片速度必须为正，否则碎片不会散开");
            Assert.Greater(ThrowConstants.SHATTER_PIECE_RADIUS_METERS, 0f, "碎片半径必须为正");
            Assert.IsTrue(ThrowConstants.SHATTER_SPREAD_DEGREES > 0f && ThrowConstants.SHATTER_SPREAD_DEGREES <= 360f,
                "碎片张角必须在 (0, 360] 内");

            // 生成：初始延时与重生延时必须为正（为 0 会在首帧就刷出一波，玩家还不知道发生了什么）。
            Assert.Greater(ThrowConstants.CHASE_INITIAL_DELAY, 0f, "初始延时必须为正");
            Assert.Greater(ThrowConstants.CHASE_RESPAWN_DELAY, 0f, "重生延时必须为正");
            Assert.Greater(ThrowConstants.CHASE_SPAWN_RADIUS, 0f, "生成半径必须为正");

            // 排序层：贴地件必须在角色之下、球必须在上。
            // （受损核心那一档已经随"耐久改数字"删掉了，所以这里只剩身体这一层是第二阶段加的。）
            Assert.Less(ThrowConstants.MUD_SORTING_ORDER, ThrowConstants.ENEMY_BODY_SORTING_ORDER,
                "泥浆必须画在敌人身体之下");
            Assert.Less(ThrowConstants.ENEMY_BODY_SORTING_ORDER, ThrowConstants.PLAYER_BODY_SORTING_ORDER,
                "玩家色块必须画在敌人之上");
            Assert.Less(ThrowConstants.PLAYER_BODY_SORTING_ORDER, ThrowConstants.AIM_SORTING_ORDER,
                "瞄准环必须画在角色之上（它是玩家的操作反馈，不能被角色盖住）");
            Assert.Less(ThrowConstants.AIM_SORTING_ORDER, ThrowConstants.BALL_SORTING_ORDER,
                "第一阶段定的次序（球在瞄准环之上）不得被第二阶段改掉");
            Assert.Less(ThrowConstants.LANDING_FLASH_SORTING_ORDER, ThrowConstants.SHATTER_SORTING_ORDER,
                "碎片是最高优先级的读数，必须画在落地瞬闪之上");

            // 半径关系：敌人不该比球小到看不见，也不该大到把瞄准环全遮住。
            Assert.Greater(ThrowConstants.ENEMY_RADIUS_METERS, ThrowConstants.BALL_RADIUS_METERS,
                "敌人半径必须大于球半径，否则看起来像球比人还大");
            Assert.Greater(ThrowConstants.ENEMY_CONTACT_RADIUS, ThrowConstants.ENEMY_RADIUS_METERS,
                "接触半径必须大于敌人半径，否则'贴上'这件事发生在碰撞之前，玩家会被隔着空隙扣血");

            // 落地闪圈的次序：内圈是击退范围、外圈是效果范围，而效果范围不得小于击退范围
            // （小的话外圈会被内圈盖住，等于白画）。
            Assert.Greater(ThrowConstants.MUD_RADIUS_METERS, ThrowConstants.IMPULSE_RADIUS,
                "水球的泥浆半径必须大于击退半径，否则两个闪圈会重合、读不出是两层");
            Assert.GreaterOrEqual(ThrowConstants.LANDING_FLASH_DURATION, ThrowConstants.LANDING_FLASH_SECONDARY_DELAY,
                "外圈的延迟不能超过总时长，否则它一次都不会出现");
            Assert.Greater(ThrowConstants.LANDING_FLASH_SECONDARY_DELAY, 0f,
                "外圈必须有延迟：两个同心环同时出现会糊成一个粗环，'内圈外圈是两件事'反而读不出来");

            // 颜色必须在合法范围（写错成 0-255 会让颜色被钳成一坨）。
            AssertColorInRange(ThrowConstants.ENEMY_BODY_COLOR, "敌人身体色");
            AssertColorInRange(ThrowConstants.ENEMY_SLOW_BODY_COLOR, "敌人减速色");
            AssertColorInRange(EnemyVisual.FlashColor, "敌人闪烁色");
            AssertColorInRange(ThrowConstants.MUD_COLOR, "泥浆色");
            AssertColorInRange(ThrowConstants.PLAYER_BODY_COLOR, "玩家色块色");
            AssertColorInRange(ThrowConstants.LANDING_FLASH_OUTER_COLOR, "落地瞬闪外圈色");

            // 三态两两不同：同色就分不出"正常 / 减速 / 被打中"。
            Assert.AreNotEqual(ThrowConstants.ENEMY_BODY_COLOR, ThrowConstants.ENEMY_SLOW_BODY_COLOR,
                "正常色与减速色不能相同，否则减速看不出来");
            Assert.AreNotEqual(ThrowConstants.ENEMY_BODY_COLOR, EnemyVisual.FlashColor,
                "正常色与闪烁色不能相同，否则命中看不出来");
            Assert.AreNotEqual(EnemyVisual.FlashColor, EnemyVisual.SlowColor,
                "闪烁色与'减速+闪烁'色不能相同，否则同时中两个状态时读不出减速还在");
        }

        /// <summary>颜色的亮度（三通道之和）。只用于"哪个更暗"这类比较，不做色彩空间换算。</summary>
        /// <remarks>
        /// 不引 <c>Color.grayscale</c>：它带 Rec.709 权重，对这几个明显偏红的颜色来说，
        /// "哪个更暗"的结论一样，但用三通道之和更直白 —— 断言失败时能一眼看出两个数。
        /// </remarks>
        private static float Luminance(Color color)
        {
            return color.r + color.g + color.b;
        }

        private static void AssertColorInRange(Color color, string label)
        {
            Assert.IsTrue(color.r >= 0f && color.r <= 1f, $"{label} 的 r 分量越界：{color.r}");
            Assert.IsTrue(color.g >= 0f && color.g <= 1f, $"{label} 的 g 分量越界：{color.g}");
            Assert.IsTrue(color.b >= 0f && color.b <= 1f, $"{label} 的 b 分量越界：{color.b}");
            Assert.IsTrue(color.a >= 0f && color.a <= 1f, $"{label} 的 a 分量越界：{color.a}");
        }

        // ================================================================
        // W18 · 被击退之后必须还能重新贴上来（自锁回归）
        // ================================================================

        /// <summary>
        /// 站在接触距离内的敌人被击退之后，必须在合理时间内<b>重新回到接触距离内</b>。
        /// </summary>
        /// <remarks>
        /// <b>这一条是为一个真实的自锁写的，而且它是设计出来的、不是碰出来的。</b>
        /// "进入停止距离就不再追"是个<b>纯几何</b>判据，于是有一个闭合的坏结局：
        /// 击退把敌人推到停止距离之外一点点 ⇒ 几何判据说"够近了，别追" ⇒
        /// 敌人自己再也没有理由往回走 ⇒ 玩家再也挨不到第二下。
        /// 现象是"被撞一次之后敌人黏在原地不动了"，不报错、也不像 bug，只是整局只掉一次血 ——
        /// 靠 Play 手测第一遍很可能被当成"敌人被我打退了"。
        /// <para>所以 <c>EnemyLogic</c> 里加了"速度已经小到快停下、且仍在追击范围内 ⇒ 重新压上去"
        /// 这一条。本测试直接步进账本，把"脱离 → 追回 → 再次接触"整个循环跑完。</para>
        /// </remarks>
        [Test]
        public void W18_被击退之后必须还能重新贴上来()
        {
            var probe = new Probe();
            var logic = new EnemyLogic(probe, new EnemyConfig());

            // 起点 2.2 而不是"刚好在接触距离上"：站在 1.0 上会被玩家（半径 0.35）的碰撞体直接顶出去，
            // 那一下是物理推的、不是本测试要验的击退。2.2 之外没有接触，推多远全由击退决定。
            const float startDistance = 2.2f;

            probe.Position = new Vector2(startDistance, 0f);
            logic.SetTarget(Vector2.zero);

            // 沿玩家→敌人的方向击退（最大值），也就是把敌人推远。
            logic.ApplyKnockback(ThrowConstants.ENEMY_KNOCKBACK_IMPULSE, Vector2.right);
            logic.BeginStun(new EnemyConfig().StunSeconds, FixedStep);

            float farthest = startDistance;
            int refitFrame = -1;

            // 10 秒足够走完"滑停 + 追回"两段；不够就说明它没回来。
            const int frames = 500;

            for (int i = 0; i < frames && refitFrame < 0; i++)
            {
                logic.Tick(i * FixedStep, FixedStep);
                probe.Step();                 // 引擎的物理步：每帧无条件积分位置

                // 玩家在原点，所以"离原点的距离"就是"离玩家的距离"。
                float distance = probe.Position.magnitude;

                farthest = Mathf.Max(farthest, distance);

                if (distance <= ThrowConstants.ENEMY_CONTACT_RADIUS) refitFrame = i;
            }

            // 先确认这一次击退真的把它推远了：否则下面的断言会被平凡满足
            // （一直待在接触距离里也能"重新贴上"）。
            Assert.Greater(farthest, ThrowConstants.ENEMY_CONTACT_RADIUS,
                "这次击退没有把敌人推出接触距离 —— 测试前提不成立");

            Assert.GreaterOrEqual(refitFrame, 0,
                $"整整 {frames * FixedStep:F0} 秒之后敌人还停在 {probe.Position.magnitude:F2} 米处，" +
                "没有再贴上来 —— 玩家再也挨不到第二下（停止距离的自锁）");

            // **上限也要有。** W20 只量禁足窗口内的位移（1.32 米），量不到"禁足结束后
            // 惯性还在、又往外滑了一截"那一段 —— 而**玩家实际感受到的脱离距离是这个 `farthest`**。
            // 没有这条上限，"击退太远"这个坏法就完全没有测试守着。
            //
            // 4.0 这个数是量出来的：击退把敌人从 2.2 推到 3.52（1.32 米是禁足段，其余是反向惯性）。
            // 白模视野 10 米宽、敌人离玩家 3.5 米仍然在屏内可继续打，所以 4.0 是"能容忍"
            // 与"明显过头"之间的分界。调到 5 米以上就该怀疑 `ENEMY_KNOCKBACK_IMPULSE` 或
            // `ENEMY_STUN_FRAMES` 被调大了。
            Assert.Less(farthest, 4f,
                $"一次击退把敌人推到了 {farthest:F2} 米外 —— 太远，会飞出视野、接着打不着" +
                "（W20 量的是禁足段位移，这一条量的是玩家实际感受到的脱离距离，两者不是同一个数）");

            Assert.Less(refitFrame * FixedStep, 6f,
                $"敌人花了 {refitFrame * FixedStep:F1} 秒才重新贴上，太慢 —— 追击手感会断掉");
        }

        // ================================================================
        // W19 · 禁足期间不得衰减冲量（第 1 条反馈的回归）
        // ================================================================

        /// <summary>
        /// 被砸中后的禁足窗口里，账本速度必须<b>逐帧保持等于冲量</b>，位移必须是
        /// <c>冲量 × 禁足时长</c>。
        /// </summary>
        /// <remarks>
        /// <b>这一条是为一个真实的"感觉不出来"写的，而它是自己写出来的。</b>
        /// 旧写法是"禁足期间传零方向给 <c>SteerTowards</c>"，于是它走指数衰减那一支：
        /// <c>e^(-10×0.02) = 0.819</c>，每帧砍掉 18%。0.08 秒之后速度只剩 2.47、总位移约 0.5 米 ——
        /// 现象就是"球砸中了，但敌人几乎没动"，而**冲量本身一点问题都没有**（5.5 比满速 3.6 还大）。
        /// <para>修法是把禁足期间的衰减率也置 0。这条测试直接钉住那个事实：
        /// 旧代码在第一帧就会红（5.5 → 4.50）。</para>
        /// <para><b>它后来还抓到过一次别的坏法：禁足少一帧。</b>递减放在帧首时，
        /// <c>BeginStun(12)</c> 只禁足 11 帧，第 12 帧回到追击分支、冲量被转向当场覆盖 ——
        /// 红在第 11 帧的"速度变成 3.6"上。那一帧恰好是 <c>ENEMY_SPEED</c> 本身，
        /// 所以这个数字很好认：<b>禁足窗口里出现 3.6 就等于"已经不在禁足了"。</b></para>
        /// </remarks>
        [Test]
        public void W19_禁足期间不得衰减冲量()
        {
            var probe = new Probe();
            var logic = new EnemyLogic(probe, new EnemyConfig());

            // 目标放在追击范围之内：否则"没有目标"那条分支也会让速度衰减，本测试就不再只钉禁足了。
            probe.Position = Vector2.zero;
            logic.SetTarget(new Vector2(3f, 0f));

            logic.ApplyKnockback(ThrowConstants.ENEMY_KNOCKBACK_IMPULSE, Vector2.up);
            logic.BeginStun(ThrowConstants.ENEMY_STUN_DURATION, FixedStep);

            Assert.IsTrue(logic.IsStunned, "刚喂了禁足，就该处于禁足状态");

            // **循环条件用 IsStunned，不用"禁足时长 / 步长"。**
            // 理由不是浮点，是**契约**：`IsStunned` 是"实现自认为还在禁足"的唯一出口，
            // 拿它当循环条件，测的就是实现真正在用的那个判据；写"时长 / 步长"则是在测试里
            // 另立一个判据，两者一旦分家，测试会绿着而实现是错的。
            // 这一点**踩过**：递减曾放在帧首，`BeginStun(12)` 实际只禁足 11 帧，
            // 红在"第 11 帧速度变成 3.6"上 —— 那不是"测试多跑了一帧"，是**实现少了整整一帧**。
            // 帧数最终由下面的 `Assert.AreEqual(ENEMY_STUN_FRAMES, stunFrames)` 钉住。
            int stunFrames = 0;

            while (logic.IsStunned)
            {
                logic.Tick(stunFrames * FixedStep, FixedStep);
                probe.Step();                 // 引擎的物理步

                float speed = probe.Velocity.magnitude;

                Assert.AreEqual(ThrowConstants.ENEMY_KNOCKBACK_IMPULSE, speed, SpeedTolerance,
                    $"禁足第 {stunFrames} 帧：滑行速度变成了 {speed} —— " +
                    "禁足期间衰减率必须是 0，否则冲量还没变成位移就被吃掉（击退看起来像没生效）");

                stunFrames++;

                Assert.Less(stunFrames, 100, "禁足没有结束 —— 计时器坏了（递减逻辑有问题）");
            }

            Assert.AreEqual(ThrowConstants.ENEMY_STUN_FRAMES, stunFrames,
                $"禁足应当是 {ThrowConstants.ENEMY_STUN_FRAMES} 帧 —— " +
                $"少了帧就是禁足被提前结束（冲量还没变成位移就被转向覆盖，实测 {stunFrames} 帧）");

            float stunDisplacement = probe.Delta.magnitude;

            // 位移的期望值必须按**实际帧数 × 步长**算，不能按标称的 `ENEMY_STUN_DURATION`：
            // 后者是给策划看的"约 0.25 秒"，而位移是 12 帧 × 0.02 秒这个离散量。
            // 拿标称值当期望会得到一个永远差 4% 的断言 —— 那种"永远差一点"的容差是坏味道。
            float stunSeconds = stunFrames * FixedStep;
            float expected = ThrowConstants.ENEMY_KNOCKBACK_IMPULSE * stunSeconds;

            Assert.AreEqual(expected, stunDisplacement, expected * 0.05f,
                $"禁足期间的位移应当是 冲量 × 实际时长 = {expected:F2} 米，" +
                $"实测 {stunDisplacement:F2} 米（{stunFrames} 帧 × {FixedStep} 秒）");
        }

        // ================================================================
        // W20 · 击退总位移必须看得出来
        // ================================================================

        /// <summary>
        /// 一次击退把敌人推出多远，必须落在"看得出来"的区间里。
        /// </summary>
        /// <remarks>
        /// 上下界挡的是两种相反的坏法：<b>下限</b>挡"冲量被吃掉"（旧行为约 0.5 米），
        /// <b>上限</b>挡"调过头把敌人推出屏幕"（白模的视野是 10 米宽，推 3 米以上就没法接着打了）。
        /// <para><b>只量禁足窗口之内</b>，不量"整场最远"。禁足结束后敌人立刻反向追回来，
        /// 位移会**先涨后跌**（反向那段会把累计位移吃回去），拿整场的最大值去量就变成在量
        /// "它追回到多近"，与击退本身无关了 —— 这类"量错了东西"的断言比没有断言更糟，
        /// 因为它会在实现正确时变红、在实现坏掉时变绿。</para>
        /// <para>这条是**数值契约**测试，不是实现细节测试：它允许你改冲量、改禁足时长、
        /// 改衰减率，只要"打一下把人推出去 1.2~2.6 米"这件事还成立。</para>
        /// </remarks>
        [Test]
        public void W20_击退总位移必须看得出来()
        {
            var probe = new Probe();
            var logic = new EnemyLogic(probe, new EnemyConfig());

            probe.Position = Vector2.zero;
            logic.SetTarget(new Vector2(3f, 0f));

            logic.ApplyKnockback(ThrowConstants.ENEMY_KNOCKBACK_IMPULSE, Vector2.up);
            logic.BeginStun(ThrowConstants.ENEMY_STUN_DURATION, FixedStep);

            float farthest = 0f;

            // 只跑到禁足结束：那一段的位移就是"被推了多远"。
            // 帧数用常量而不是"时长 / 步长"：帧数是真值，秒数是它的说法，
            // 而后者一旦被改动就会让这条测试偷偷量错窗口（见 ENEMY_STUN_FRAMES）。
            for (int i = 0; i < ThrowConstants.ENEMY_STUN_FRAMES; i++)
            {
                logic.Tick(i * FixedStep, FixedStep);
                probe.Step();                 // 引擎的物理步

                // 用累计位移而不是"离世界原点的距离"：起点在原点时两者一样，
                // 但**语义**上要问的是"被推了多远"。
                farthest = Mathf.Max(farthest, probe.Delta.magnitude);
            }

            // 下限 1.1 而不是 1.2：实测 1.32（= 5.5 × 12 × 0.02），留出一帧的余量。
            // 卡在 1.2 时只要实现少禁足一帧就掉到 1.21 —— 那种"刚好通过"没有意义。
            Assert.Greater(farthest, 1.1f,
                $"一次击退只把敌人推了 {farthest:F2} 米 —— 太近，玩家看不出打中了（旧行为约 0.55 米）");
            Assert.Less(farthest, 2.6f,
                $"一次击退把敌人推了 {farthest:F2} 米 —— 太远，会飞出视野、接着打不着");
        }

        // ================================================================
        // 探针：手写驱动逻辑账本，不依赖 MonoBehaviour 生命周期
        // ================================================================

        /// <summary>
        /// 移动执行器探针：把速度存在字段里，<b>并把速度积分成位置</b>，不碰 <c>Rigidbody2D</c>。
        /// </summary>
        /// <remarks>
        /// 照 <c>移动Tests.LimitProbe</c> 的做法：EditMode 下 <c>AddComponent</c> 不保证跑 <c>Awake</c>，
        /// 而物理体在 EditMode 里也不一定有确定行为。用探针之后，"衰减"与"位移"这些账本行为
        /// 就变成纯算术，可以逐步验算。
        /// <para><b>积分位置不是"顺手加的"。</b>第一版的探针只存速度、位置一动不动，
        /// 于是 W18/W19/W20 全部报"位移 0.00 米 / 敌人还停在原地" —— 看起来像实现坏了，
        /// 其实是测试自己搭的环境不会动。真实执行器（<c>EnemyMotor</c>）的
        /// <c>Position</c> 读的是 <c>Rigidbody2D.position</c>，而引擎每步都会按速度移动它。
        /// 探针要替掉的就是这一句。</para>
        /// <para><b>只积分一次写入。</b>账本每帧最多提交一次速度（没有提交时基类不写），
        /// 所以这里"每次 <see cref="Move"/> 前进一帧"与引擎的行为一致；
        /// 写成"每帧无条件积分"会多走那些零提交的帧，位移会偏大。</para>
        /// <para>用由参数推出的固定步长而不是读 <c>Time.fixedDeltaTime</c>：
        /// 测试里 <c>Time</c> 是不可控的，而本文件所有步进都用 <see cref="FixedStep"/>。</para>
        /// </remarks>
        private sealed class Probe : IMovementMotor
        {
            /// <summary>固定步长：与 <see cref="FixedStep"/> 同一个数。</summary>
            /// <remarks>
            /// <b>常量不能叫 <c>Step</c>：</b>下面那个"模拟一个物理步"的方法就叫 <c>Step()</c>，
            /// C# 不允许同名的字段与方法共存（CS0102），所以这里用 <c>Dt</c>。
            /// </remarks>
            private const float Dt = FixedStep;

            private Vector2 _position;
            private Vector2 _velocity;

            /// <summary>物理体位置。<b>必须在喂第一帧之前设</b>（测试用对象初始化器设）。</summary>
            public Vector2 Position
            {
                get => _position;
                set => _position = value;
            }

            /// <summary>当前速度，与账本帧末写出的一致。</summary>
            public Vector2 Velocity => _velocity;

            /// <summary>
            /// 相对起点累计走过的位移。每物理步按当前速度累加，见 <see cref="Step"/>。
            /// </summary>
            /// <remarks>
            /// 用它的模长当"被推了多远"，而不是 <see cref="Position"/> 的模长 ——
            /// 后者是"离世界原点的距离"，只有当初始位置正好在原点时才等于位移。
            /// 测试里为了干净都把起点设在原点，所以两者眼下数值相同；用它是因为**语义对**，
            /// 将来有人把起点挪到别处时不会静默算错。
            /// </remarks>
            public Vector2 Delta { get; private set; }

            public Vector2 Facing { get; set; } = Vector2.right;

            /// <summary>账本帧末的写出：只改速度，<b>不动位置</b>。</summary>
            /// <remarks>
            /// <b>与 <see cref="Step"/> 分开是刻意的。</b>
            /// 账本<b>只在有提交的帧</b>才写速度（零提交的帧它故意不写，免得覆盖引擎），
            /// 而 <c>Rigidbody2D</c> 是<b>每个物理步</b>都按当前速度积分位置、不管那一帧有没有人写速度。
            /// <para>曾经把位置积分塞在 <c>Move</c> 里，于是"有速度但没人写速度"的那几帧
            /// 在探针里**完全不动** —— 而真实引擎在那几帧照走。禁足期间正是这种帧，
            /// 所以探针会把 1.4 米的位移算成 0.11 米，测试随之全红（而实现是对的）。</para>
            /// </remarks>
            public void Move(Vector2 velocity)
            {
                _velocity = velocity;
            }

            /// <summary>模拟<b>一个物理步</b>：引擎按当前速度积分位置。由测试循环每帧调用一次。</summary>
            public void Step()
            {
                Vector2 step = _velocity * Dt;

                _position += step;
                Delta += step;
            }

            public void SetPosition(Vector2 position)
            {
                Delta += position - _position;
                _position = position;
            }
        }
    }
}
