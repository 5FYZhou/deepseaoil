using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 移动状态组：持有移动层的状态实例与状态机，<b>并且是唯一写速度的地方</b>。
    /// </summary>
    /// <remarks>
    /// <b>它直接拿执行器</b>（<see cref="IActorMotor"/>），不再经 <c>PlayerLogic</c> 转发 ——
    /// 审查已定"不暴露无意义的转发：移动层自己去 <c>ctx</c> 拿时间、直接读 <c>PlayerConfig</c> /
    /// <c>InputBuffer</c>"。收口前冲刺冷却、缓冲窗口这三个成员只服务本类，
    /// 却要绕一层 <c>PlayerLogic</c> 取，白绕一圈。
    /// <para><b>仲裁交给骨架</b>（<see cref="StateGroup{TStateTag}"/>）：本类补三件事 ——
    /// 基础态（走 / 站）、抢占链（当前只有冲刺）、以及<b>把上层的门禁落到速度上</b>。</para>
    /// <para><b>冲刺的"资格"是两层条件</b>：冷却（本组持有的 <c>Cooldown</c>）＋
    /// 缓冲窗口（<see cref="InputBuffer"/>）。两半刻意不合并 ——
    /// 窗口有采样率、有容量、会被暂停清空，那是另一套语义。</para>
    /// <para><b>为什么冲刺冷却归本组</b>：审查定了"冷却件按层持有" ——
    /// 投掷冷却归战斗层，冲刺冷却归移动层，两处只是同一个通用件的两个实例。</para>
    /// </remarks>
    public sealed class MoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        private readonly PlayerLogic _logic;
        private readonly IActorMotor _motor;

        /// <summary>玩家取值边界：冲刺冷却与缓冲窗口从它读语义（<b>不持有 <c>PlayerConfig</c></b>）。</summary>
        /// <remarks>
        /// 收口前这里缓存的是 <c>motor.Config as PlayerConfig</c>，再由两个 private 属性转发
        /// <c>dashCooldown</c> / <c>dashBufferTime</c>。那两跳都是多余的：本组要的是"冲刺冷却几秒"
        /// 这条<b>语义</b>，而语义点在 <see cref="PlayerSpec"/> 上。于是配置对象不再往逻辑层里走。
        /// </remarks>
        private readonly PlayerSpec _spec;

        private readonly InputBuffer _buffer;
        private readonly DashState _dash;

        /// <summary>冲刺冷却（存绝对时刻：不受暂停影响，也不需要每帧累减）。</summary>
        private readonly Cooldown _dashCooldown = new Cooldown();

        /// <summary>最近一次非零输入方向（<b>已归一化</b>）；零输入时保持不变，供冲刺取向用。</summary>
        /// <remarks>
        /// 初始值是 <c>Vector2.right</c>：尚未有任何输入时按"朝右"冲刺，而不是把方向判成零向量
        /// （零向量会让 <c>DashState.Configure</c> 保持它自己的初值，行为不直观）。
        /// 它住在本组而不是 <c>PlayerLogic</c>：唯一的消费者是冲刺，而"谁用谁持有"。
        /// </remarks>
        private Vector2 _direction = Vector2.right;

        /// <param name="logic">宿主（组合件：状态机骨架认识的窄口在这里）。</param>
        /// <param name="spec">玩家取值边界（冲刺冷却与缓冲窗口的语义来源）。</param>
        /// <param name="motor">移动执行器（速度提交与速度乘数的唯一出口）。</param>
        /// <param name="buffer">按键沿缓冲（冲刺窗口）。</param>
        public MoveGroup(PlayerLogic logic, PlayerSpec spec, IActorMotor motor, InputBuffer buffer)
        {
            _logic = logic;
            _spec = spec;
            _motor = motor;
            _buffer = buffer;

            _dash = new DashState(logic);

            AddState(new IdleState(logic));
            AddState(new MoveState(logic));
            AddState(_dash);

            StateChanged += OnStateChanged;
        }

        protected override MovementStateTag EmptyTag => MovementStateTag.Empty;

        /// <summary>基础态：当前状态结束后该回到的"无事可做"状态。俯视角只有走 / 站两档。</summary>
        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude > 0f
                ? MovementStateTag.Move
                : MovementStateTag.Idle;
        }

        /// <summary>冲刺状态实例，供调试与测试读取其入场方向。</summary>
        /// <remarks>只读用途。状态类本身不该被外部配置，入场参数一律经 <see cref="TryCommitPreempt"/> 喂入。</remarks>
        public DashState Dash => _dash;

        /// <summary>冲刺资格：冷却已过，且缓冲里有窗口内的按下。<b>纯查询</b>，不消费。</summary>
        public bool CanDash(float now)
        {
            return _dashCooldown.CanUse(now)
                && _buffer.CanConsume(InputType.Dash, now, _spec.DashBufferSeconds);
        }

        /// <summary>消费冲刺资格；冷却不足、或缓冲里没有窗口内的按下时拒绝。</summary>
        public bool TryConsumeDash(float now)
        {
            if (!_dashCooldown.CanUse(now)) return false;
            if (!_buffer.TryConsume(InputType.Dash, now, _spec.DashBufferSeconds)) return false;

            _dashCooldown.MarkUsed(now, _spec.DashCooldownSeconds);

            return true;
        }

        /// <summary>
        /// 推进一个物理帧：<b>速度乘数先交给账本、状态再按输入写速度、强制速度最后整条接管</b>。
        /// </summary>
        /// <param name="gates">上层（状态效果 / 战斗）提交的门禁；空门禁时本层完全按自己的状态走。</param>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
            // 乘数落在"目标速度"上，所以必须赶在状态算速度之前交给账本（见 IActorMotor.SpeedScale）。
            _motor.SpeedScale = gates.SpeedScale;

            Vector2 move = ctx.inputSnapshot.Move;

            if (move.sqrMagnitude > 0f) _direction = move.normalized;

            TickStates(in ctx);

            ApplyGates(in gates);
        }

        /// <summary>抢占判定：纯查询，不消费任何缓冲与余额。</summary>
        protected override bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            if (CanDash(ctx.now))
            {
                target = MovementStateTag.Dash;
                return true;
            }

            target = MovementStateTag.Empty;
            return false;
        }

        /// <summary>
        /// 抢占提交：消费余额并喂入场参数；判定已通过，失败即本帧不切换。
        /// </summary>
        /// <remarks>
        /// <b>喂方向必须在消费成功之后</b>：<c>Configure</c> 会改写状态对象的字段，若放在消费之前，
        /// 提交失败（缓冲窗口已过）时状态已被改过，留下一个与"上次真正冲刺过的方向"不符的脏值。
        /// </remarks>
        protected override bool TryCommitPreempt(MovementStateTag target, in LogicContext ctx)
        {
            if (target != MovementStateTag.Dash) return false;

            if (!TryConsumeDash(ctx.now)) return false;

            _dash.Configure(ResolveDashDirection(in ctx, _direction));

            return true;
        }

        /// <summary>
        /// 把门禁落到速度上 —— <b>全工程唯一"按上层要求改速度"的地方</b>。
        /// </summary>
        /// <remarks>
        /// 放在状态跑完之后：状态（<c>MoveState</c> / <c>IdleState</c>）会整体接管速度，
        /// 门禁写在它们前面等于没写 —— 这也是旧实现"挨打了却纹丝不动"的成因之一。
        /// <para><b>强制速度不叠加速度乘数</b>：受击滑停是外力，叠上地面减速会把它拖短，
        /// 而"被撞飞多远"应当只由冲量与受击减速度决定。</para>
        /// </remarks>
        private void ApplyGates(in MoveGates gates)
        {
            if (gates.HasForcedVelocity) _motor.SetVelocity(gates.ForcedVelocity);
        }

        /// <summary>冲刺取向：优先用本帧输入方向，无输入则用最近朝向。</summary>
        private static Vector2 ResolveDashDirection(in LogicContext ctx, Vector2 fallback)
        {
            Vector2 move = ctx.inputSnapshot.Move;

            return move.sqrMagnitude > 0f ? move.normalized : fallback;
        }

        private static void OnStateChanged(MovementStateTag current, MovementStateTag previous)
        {
            EventBus<MovementStateChanged>.Publish(new MovementStateChanged(current, previous));
        }
    }
}
