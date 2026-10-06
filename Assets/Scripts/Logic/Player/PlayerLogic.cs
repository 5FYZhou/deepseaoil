using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家逻辑：持有账本（水球 ＋ 血量）、冲刺余额与计时，回答"有没有资格"，并驱动移动状态组。
    /// </summary>
    /// <remarks>
    /// 不做状态转移决策、不知道任何具体状态类；速度与外力的写入口见基类 <see cref="ActorLogic"/>。
    /// 俯视角下跳跃/二段跳/蹬墙跳已移除（相应状态类也不存在），保留的是冲刺——
    /// 它的"资格"由冷却 ＋ <see cref="InputBuffer"/> 窗口共同决定，是状态组唯一会查询的余额。
    /// <para><b>受击的唯一入口是本类的 <see cref="TakeDamage"/></b>：世界侧（接触、将来的陷阱与技能）
    /// 只能"通知"，数据仍由玩家侧自己改。收口前这条路上有两个写者 ——
    /// 表现层的 <c>PlayerHealthController</c> 直接持有血量，并且经 <c>PlayerController</c> 的
    /// 两个 internal 口子往账本里塞冲量与限速。</para>
    /// <para><b>击退为什么是"挂起 + 帧内接管"：</b><c>ActorLogic.FixedTick</c> 在帧首就把
    /// <c>_delta</c> 与"本帧速度上限"都复位了，而世界侧的结算（<c>CombatRoot</c>）跑在玩家自己那一帧
    /// <b>之后</b> —— 所以帧外直接累加的冲量一定会被下一帧帧首清掉。旧实现正是这么写的，
    /// 现象是"被撞了但纹丝不动"，而且不报错。现在的口径：帧外只挂起，帧内（状态跑完之后）一次性接管速度。</para>
    /// <para><b>为什么接管必须在状态之后：</b><c>MoveState</c> / <c>IdleState</c> 的 <c>SnapVelocity</c>
    /// 会整体覆盖本帧已提交的变更 —— 写在它们前面等于没写。</para>
    /// </remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerConfig _player;
        private readonly PlayerSpec _spec;
        private readonly InputBuffer _buffer;
        private readonly MoveGroup _moveGroup;
        private readonly PlayerStats _stats;

        private float _lastDashAt = float.NegativeInfinity;

        /// <summary>最近一次非零输入方向（<b>已归一化</b>）；零输入时保持不变，供冲刺取向与后续技能使用。</summary>
        /// <remarks>
        /// 初始值是 <c>Vector2.right</c>：尚未有任何输入时按"朝右"冲刺，而不是把方向判成零向量
        /// （零向量会让 <c>DashState.Configure</c> 保持它自己的初值，行为不直观）。
        /// 存归一化值而不是原始输入：本属性的消费者（冲刺取向、后续技能）要的是"方向"，
        /// 把"归一化"留给每个消费者各做一次，迟早会漏掉一处。
        /// </remarks>
        private Vector2 _direction = Vector2.right;

        /// <summary>已递交、还没被写进速度账本的击退冲量（可在帧外累加）。</summary>
        private Vector2 _pendingKnockback;

        /// <param name="motor">移动执行器（接口，测试可塞纯 C# 探针）。</param>
        /// <param name="config">角色运动参数（SO）。</param>
        /// <param name="buffer">按键沿缓冲（冲刺窗口）。</param>
        /// <param name="spec">玩家表值：血量 / 无敌帧 / 击退上限。</param>
        public PlayerLogic(IMovementMotor motor, PlayerConfig config, InputBuffer buffer, in PlayerSpec spec)
            : base(motor, config)
        {
            _player = config;
            _spec = spec;
            _buffer = buffer;
            _stats = new PlayerStats(in spec);
            _moveGroup = new MoveGroup(this);
        }

        /// <summary>当前移动状态。</summary>
        public MovementStateTag CurrentState => _moveGroup.Current;

        /// <summary>移动状态组，供调试面板与测试查看状态实例（只读用途）。</summary>
        public MoveGroup MoveGroup => _moveGroup;

        /// <summary>最近一次非零输入方向（已归一化）；供冲刺取向与调试面板使用。</summary>
        public Vector2 Direction => _direction;

        /// <summary>玩家表值（世界侧判接触 / 重生延时要读它）。</summary>
        public PlayerSpec Spec => _spec;

        /// <summary>账本：水球 ＋ 血量。<b>世界侧经它拿读数、经本类入口改数据</b>。</summary>
        public PlayerStats Stats => _stats;

        /// <summary>是否还有血。</summary>
        public bool IsAlive => _stats.IsAlive;

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

        /// <summary>
        /// 结算一次伤害。<b>世界侧唯一的受伤入口</b>（接触、陷阱、将来的技能都走这里）。
        /// </summary>
        /// <param name="damage">命中事实（伤害值 ＋ 可选的击退）。</param>
        /// <param name="now">当前时间（由驱动方给出；本类不读 <c>UnityEngine.Time</c>）。</param>
        /// <returns>真的生效了为 <c>true</c>（被无敌帧挡掉时为 <c>false</c>）。</returns>
        /// <remarks>
        /// <b>被挡掉时三件事一起不发生</b>：不扣血、不推、也不写无敌时间。
        /// 只扣血不推，玩家会被粘在敌人身上连扣；只推不写无敌，下一帧立刻再扣一次。
        /// <para>冲量只是<b>挂起</b>，真正的接管发生在下一次 <see cref="OnTick"/> 的末尾 ——
        /// 理由见类注释。</para>
        /// </remarks>
        public bool TakeDamage(in Damage damage, float now)
        {
            if (!damage.HasDamage && !damage.HasKnockback) return false;

            if (damage.HasDamage && !_stats.Health.ApplyDamage(damage.Amount, now)) return false;

            if (damage.HasKnockback) _pendingKnockback += damage.Direction * damage.Impulse;

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
        /// 把角色瞬移到给定位置（重生 / 传送用），并把血量恢复满、清掉挂起的击退。
        /// </summary>
        /// <remarks>
        /// 走执行器的物理体位置而不是 <c>transform.position</c>：后者会被刚体的位置积分覆盖掉，
        /// 表现为"瞬移了一下又弹回去"。它<b>不</b>是"移动"：不经过状态机、不改朝向、不产生提交，
        /// 所以想真正停住要另调 <see cref="ActorLogic.StopMove"/>（本方法已经做了）。
        /// </remarks>
        public void RespawnTo(Vector2 position)
        {
            _stats.Health.ResetToFull();

            StopMove();

            _pendingKnockback = Vector2.zero;

            Motor.SetPosition(position);
        }

        /// <summary>是否可冲刺：冷却已过，且缓冲里有窗口内的按下。纯查询，不消费。</summary>
        public bool CanDash(float now)
        {
            return CanDashNow(now) && _buffer.CanConsume(InputType.Dash, now, _player.dashBufferTime);
        }

        /// <summary>消费冲刺缓冲；冷却不足时拒绝。</summary>
        public bool TryConsumeDash(float now)
        {
            if (!CanDashNow(now)) return false;
            if (!_buffer.TryConsume(InputType.Dash, now, _player.dashBufferTime)) return false;

            _lastDashAt = now;
            return true;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // 此处是外力唯一入口，但玩家**刻意不施外力**（零惯性 ＋ 无重力）：
            // 这不是"还没写"，是设计意图——第一个真实消费者预计是敌人、击退、水流或吸附。
            // 要用时形如 ApplyExtraForce(in ctx, new Vector2(knockbackX, knockbackY)); 再按需 ClampSpeed(...)，
            // 注意顺序必须先外力后钳制（钳制会整体覆盖当帧速度）。

            Vector2 move = ctx.inputSnapshot.Move;
            if (move.sqrMagnitude > 0f) _direction = move.normalized;

            _moveGroup.Tick(in ctx);

            ApplyPendingKnockback();
        }

        /// <summary>
        /// 把挂起的击退落进本帧账本：<b>在状态跑完之后</b>一次性接管速度，并按表值限制上限。
        /// </summary>
        /// <remarks>
        /// 逐分量精确比较，不用 <c>!= Vector2.zero</c>：后者带 <c>1e-10</c> 的平方容差，
        /// 小冲量会被静默吞掉（<c>EnemyLogic</c> 的冲量分支栽过同一个跟头）。
        /// </remarks>
        private void ApplyPendingKnockback()
        {
            if (_pendingKnockback.x == 0f && _pendingKnockback.y == 0f) return;

            Vector2 knockback = _pendingKnockback;
            _pendingKnockback = Vector2.zero;

            float limit = _spec.KnockbackSpeedLimit;

            // 上限非法（0 / 非数）时按"不限"处理：SetSpeedLimit 自己会忽略非法值，
            // 而 ClampMagnitude 遇到 0 会把冲量整个吃掉（表现为"挨打后原地不动"）。
            Vector2 takeover = limit > 0f && !float.IsNaN(limit)
                ? Vector2.ClampMagnitude(knockback, limit)
                : knockback;

            SetSpeedLimit(limit);
            SetVelocity(takeover);
        }

        /// <summary>冲刺冷却是否已过。</summary>
        /// <remarks>
        /// 平台跳跃时代的条件还含"在地面 或 空中余额未用完"；俯视角没有明确的空中/地面之分，
        /// 故只按冷却。若将来要做"空中只能冲一次"，需要先引入 Airborne 语义（见 Docs 待确认项）。
        /// </remarks>
        private bool CanDashNow(float now)
        {
            return now - _lastDashAt >= _player.dashCooldown;
        }
    }
}
