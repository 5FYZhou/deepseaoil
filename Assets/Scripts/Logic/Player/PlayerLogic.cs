using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家逻辑：持有账本（水球 ＋ 血量）与<b>三层领域状态机</b>（状态效果 / 战斗 / 移动），
    /// 并回答"我现在能不能做某件事"。
    /// </summary>
    /// <remarks>
    /// <b>分层与门禁（审查已定的口径）</b>：状态效果层 → 战斗层 → 移动层，每层只向下层输出门禁与锁，
    /// <b>只有移动层拥有写速度的权限</b>。本类是按这个顺序驱动的唯一地方：
    /// <code>
    /// ① StatusGroup.Tick(ctx, 挂起的击退)   // 产出门禁（受击 = 速度被外力接管）
    /// ② （战斗层没有物理帧职责：瞄准与开火都在渲染帧）
    /// ③ MoveGroup.Tick(ctx, 门禁)           // 唯一写速度
    /// </code>
    /// <para><b>不做状态转移决策、不知道任何具体状态类</b>；速度与外力的写入口见基类 <see cref="ActorLogic"/>。</para>
    /// <para><b>受击的唯一入口是 <see cref="TakeDamage"/></b>：世界侧只能"通知"，数据仍由玩家侧自己改。</para>
    /// <para><b>击退为什么是"挂起 + 帧内进入受击"：</b><c>ActorLogic.FixedTick</c> 在帧首就把
    /// <c>_delta</c> 与"本帧速度上限"都复位了，而世界侧的结算跑在玩家那一帧<b>之后</b> ——
    /// 帧外直接累加的冲量一定会被下一帧帧首清掉（旧实现正是这么写的，现象是"被撞了但纹丝不动"，
    /// 而且不报错）。现在的口径：帧外只挂起，帧内由状态效果层变成一次"进入受击"。</para>
    /// </remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerConfig _player;
        private readonly PlayerSpec _spec;
        private readonly InputBuffer _buffer;
        private readonly PlayerStats _stats;

        private readonly StatusGroup _status;
        private readonly CombatGroup _combat;
        private readonly MoveGroup _moveGroup;

        /// <summary>最近一次非零输入方向（<b>已归一化</b>）；零输入时保持不变，供冲刺取向与后续技能使用。</summary>
        /// <remarks>
        /// 初始值是 <c>Vector2.right</c>：尚未有任何输入时按"朝右"冲刺，而不是把方向判成零向量
        /// （零向量会让 <c>DashState.Configure</c> 保持它自己的初值，行为不直观）。
        /// 存归一化值而不是原始输入：本属性的消费者（冲刺取向、后续技能）要的是"方向"，
        /// 把"归一化"留给每个消费者各做一次，迟早会漏掉一处。
        /// </remarks>
        private Vector2 _direction = Vector2.right;

        /// <summary>已递交、还没被状态效果层消费的击退冲量（可在帧外累加）。</summary>
        private Vector2 _pendingKnockback;

        /// <param name="motor">移动执行器（接口，测试可塞纯 C# 探针）。</param>
        /// <param name="config">角色运动参数（SO）：速度、加速度、冲刺。</param>
        /// <param name="buffer">按键沿缓冲（冲刺窗口）。</param>
        /// <param name="spec">玩家表值：血量 / 无敌帧 / 击退 / 攻击间隔。</param>
        public PlayerLogic(IMovementMotor motor, PlayerConfig config, InputBuffer buffer, in PlayerSpec spec)
            : base(motor, config)
        {
            _player = config;
            _spec = spec;
            _buffer = buffer;

            _stats = new PlayerStats(in spec);
            _status = new StatusGroup(this);
            _combat = new CombatGroup(this);
            _moveGroup = new MoveGroup(this);
        }

        /// <summary>当前移动状态（调试面板与测试读它）。</summary>
        public MovementStateTag CurrentState => _moveGroup.Current;

        /// <summary>移动层（走 / 站 / 冲刺）。</summary>
        public MoveGroup MoveGroup => _moveGroup;

        /// <summary>状态效果层（受击 / 硬直）。</summary>
        public StatusGroup Status => _status;

        /// <summary>战斗层（瞄准 / 投掷资格）。</summary>
        public CombatGroup Combat => _combat;

        /// <summary>最近一次非零输入方向（已归一化）；供冲刺取向与调试面板使用。</summary>
        public Vector2 Direction => _direction;

        /// <summary>玩家表值（世界侧判接触 / 重生延时要读它）。</summary>
        public PlayerSpec Spec => _spec;

        /// <summary>账本：水球 ＋ 血量。<b>世界侧经它拿读数、经本类入口改数据</b>。</summary>
        public PlayerStats Stats => _stats;

        /// <summary>是否还有血。</summary>
        public bool IsAlive => _stats.IsAlive;

        /// <summary>冲刺冷却时长（秒）。移动层记冷却时读它。</summary>
        public float DashCooldownSeconds => _player.dashCooldown;

        /// <summary>
        /// 满速档位：配置速度与冲刺速度里的较大者。
        /// </summary>
        /// <remarks>
        /// <b>它是"受击速度上限"的基准</b>：外因（击退、水流、吸附）只被允许把速度往"当前档位"里补，
        /// 补满为止，不允许把角色推到比它自己能力更快。于是"连续挨打会不会越推越快"有了确定答案。
        /// <para>返回的是只读派生值、不是缓存字段：缓存了就要在每个写入速度的地方同步它，
        /// 而那正是"两个速度真值"的开端。</para>
        /// </remarks>
        public float MaximumSpeed => Mathf.Max(Config.moveSpeed, _player.dashSpeed);

        // ─────────────────────────────────────────────
        // 装配期注入（世界侧组合根调一次）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注入瞄准所需的世界信息与投掷裁决口。
        /// </summary>
        /// <param name="geometry">格子几何。</param>
        /// <param name="maxThrowDistance">射程上限（见 <c>CombatGroup.Configure</c> 的数值口径说明）。</param>
        /// <param name="sink">世界侧的裁决口。</param>
        public void ConfigureAim(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _combat.Configure(in geometry, maxThrowDistance, sink);
        }

        // ─────────────────────────────────────────────
        // 渲染帧（由 PlayerController.RenderTick 驱动）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 渲染帧：算一次瞄准并去重发布事实。
        /// </summary>
        /// <param name="aimWorld">瞄准点（世界坐标）。"屏幕 → 世界"的换算在表现层，只有它认识相机。</param>
        /// <param name="now">渲染帧时间（<c>Time.time</c>）。</param>
        public void UpdateAim(Vector2 aimWorld, float now)
        {
            _combat.UpdateAim(Motor.Position, aimWorld, now);
        }

        /// <summary>收起瞄准（暂停 / 拿不到相机时）。</summary>
        public void ClearAim()
        {
            _combat.ClearAim();
        }

        /// <summary>
        /// 表达一次投掷意图（"我想把这一颗球扔到瞄准的那一格"）。
        /// </summary>
        /// <param name="ball">球种。</param>
        /// <param name="now">渲染帧时间。</param>
        /// <returns>被世界侧采纳为 <c>true</c>。</returns>
        public bool RequestThrow(BallType ball, float now)
        {
            return _combat.RequestThrow(ball, now);
        }

        // ─────────────────────────────────────────────
        // 物理帧
        // ─────────────────────────────────────────────

        /// <summary>
        /// 结算一次伤害。<b>世界侧唯一的受伤入口</b>（接触、陷阱、将来的技能都走这里）。
        /// </summary>
        /// <param name="damage">命中事实（伤害值 ＋ 可选的击退）。</param>
        /// <param name="now">当前时间（由驱动方给出；本类不读 <c>UnityEngine.Time</c>）。</param>
        /// <returns>真的生效了为 <c>true</c>（被无敌帧挡掉时为 <c>false</c>）。</returns>
        /// <remarks>
        /// <b>被挡掉时三件事一起不发生</b>：不扣血、不推、也不写无敌时间。
        /// 只扣血不推，玩家会被粘在敌人身上连扣；只推不写无敌，下一帧立刻再扣一次。
        /// <para>冲量只是<b>挂起</b>，真正的接管发生在下一次 <see cref="OnTick"/>：
        /// 状态效果层把它变成一次"进入受击"，再由移动层落实到速度上。</para>
        /// </remarks>
        public bool TakeDamage(in Damage damage, float now)
        {
            if (!damage.HasDamage && !damage.HasKnockback) return false;

            if (damage.HasDamage && !_stats.Health.ApplyDamage(damage.Amount, now)) return false;

            if (damage.HasKnockback)
            {
                Vector2 impulse = damage.Direction * damage.Impulse;
                float limit = _spec.KnockbackSpeedLimit;

                // 上限非法（0 / 非数）时按"不限"处理：ClampMagnitude 遇到 0 会把冲量整个吃掉
                _pendingKnockback = limit > 0f && !float.IsNaN(limit)
                    ? Vector2.ClampMagnitude(impulse, limit)
                    : impulse;
            }

            return true;
        }

        /// <summary>
        /// 累加一次冲量（一次性的速度变化，单位/秒）—— <b>本类仍是玩家速度的唯一写者</b>。
        /// </summary>
        /// <remarks>
        /// 基类的方法是 <c>protected</c>，这里用 <c>new</c> 提升成公开访问点。
        /// 调用方<b>不要</b>因此去写 <c>Rigidbody2D.velocity</c>：速度必须经本类账本，
        /// 否则帧末写出会覆盖掉外力，表现为"被推了一下又弹回去"。
        /// <para><b>注意它在帧外累加会被帧首清掉</b>（见类注释）：世界侧要"推开玩家"请用
        /// <see cref="TakeDamage"/> 递交 <c>Damage.Impulse</c>，那条路会挂起。</para>
        /// </remarks>
        public new void AddImpulse(Vector2 deltaVelocity)
        {
            base.AddImpulse(deltaVelocity);
        }

        /// <summary>
        /// 把角色瞬移到给定位置（重生 / 传送用），并把血量恢复满、停住、清掉挂起的击退。
        /// </summary>
        /// <remarks>
        /// 走执行器的物理体位置而不是 <c>transform.position</c>：后者会被刚体的位置积分覆盖掉，
        /// 表现为"瞬移了一下又弹回去"。它<b>不</b>是"移动"：不经过状态机、不改朝向、不产生提交。
        /// <para><b>速度要硬清零</b>（<c>Motor.Move(zero)</c>）：账本里的 <c>StopMove</c> 只影响
        /// "本帧该提交什么"，而重生是瞬移 —— 物理体上残留着上一局的速度时，
        /// 复活后会自己滑一段（有惯性配置下能滑出一点几个单位）。这不是"第二个速度写者"：
        /// 本类就是那个写者，这里只是它自己的一次硬清零。</para>
        /// </remarks>
        public void RespawnTo(Vector2 position)
        {
            _stats.Health.ResetToFull();

            StopMove();                    // 账本层：本帧不再提交任何速度

            _pendingKnockback = Vector2.zero;

            Motor.Move(Vector2.zero);      // 引擎层：把残留速度清掉（否则复活后会继续滑）
            Motor.SetPosition(position);
        }

        /// <summary>是否可冲刺：冷却已过，且缓冲里有窗口内的按下。<b>纯查询</b>，不消费。</summary>
        /// <remarks>只回答"缓冲那一半"；冷却那一半在移动层（<c>MoveGroup.CanDash</c>）。</remarks>
        public bool CanConsumeDashBuffer(float now)
        {
            return _buffer.CanConsume(InputType.Dash, now, _player.dashBufferTime);
        }

        /// <summary>消费冲刺缓冲；窗口内没有按下时返回 <c>false</c>。</summary>
        public bool TryConsumeDashBuffer(float now)
        {
            return _buffer.TryConsume(InputType.Dash, now, _player.dashBufferTime);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            Vector2 move = ctx.inputSnapshot.Move;
            if (move.sqrMagnitude > 0f) _direction = move.normalized;

            // ① 状态效果层：帧外挂起的击退在这里变成一次"进入受击"，并产出本帧门禁
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            // ② 战斗层：物理帧没有职责（瞄准与开火都在渲染帧），故不参与本序列

            // ③ 移动层：唯一写速度的地方；上层门禁在这里落地
            _moveGroup.Tick(in ctx, _status.Gates);
        }
    }
}
