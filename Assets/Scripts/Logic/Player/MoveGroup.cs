using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 移动状态组：持有移动层的状态实例与状态机，<b>并且是唯一写速度的地方</b>。
    /// </summary>
    /// <remarks>
    /// 仲裁交给骨架（<see cref="StateGroup{TStateTag}"/>）：本类补三件事 ——
    /// 基础态（走 / 站）、抢占链（当前只有冲刺）、以及<b>把上层的门禁落到速度上</b>。
    /// <para><b>冲刺的"资格"是两层条件</b>：冷却（本组持有的 <c>Cooldown</c>）＋
    /// 缓冲窗口（<c>InputBuffer</c>，由玩家逻辑转发）。两半刻意不合并 ——
    /// 窗口有采样率、有容量、会被暂停清空，那是另一套语义。</para>
    /// <para><b>为什么冲刺冷却归本组而不是玩家逻辑</b>：审查定了"冷却件按层持有" ——
    /// 投掷冷却归战斗层，冲刺冷却归移动层，两处只是同一个通用件的两个实例。</para>
    /// <para>俯视角下抢占链只剩冲刺一条；加新状态时在构造函数里 <c>AddState</c>、
    /// 在 <see cref="TryDecidePreempt"/> 排优先级。</para>
    /// </remarks>
    public sealed class MoveGroup : StateGroup<MovementStateTag>
    {
        private readonly PlayerLogic _logic;
        private readonly DashState _dash;

        /// <summary>冲刺冷却（存绝对时刻：不受暂停影响，也不需要每帧累减）。</summary>
        private readonly Cooldown _dashCooldown = new Cooldown();

        public MoveGroup(PlayerLogic logic)
        {
            _logic = logic;

            _dash = new DashState(logic, logic.Config);

            AddState(new IdleState(logic, logic.Config));
            AddState(new MoveState(logic, logic.Config));
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
            return _dashCooldown.CanUse(now) && _logic.CanConsumeDashBuffer(now);
        }

        /// <summary>消费冲刺资格；冷却不足、或缓冲里没有窗口内的按下时拒绝。</summary>
        public bool TryConsumeDash(float now)
        {
            if (!_dashCooldown.CanUse(now)) return false;
            if (!_logic.TryConsumeDashBuffer(now)) return false;

            _dashCooldown.MarkUsed(now, _logic.DashCooldownSeconds);

            return true;
        }

        /// <summary>
        /// 推进一个物理帧：<b>状态先按输入写速度，门禁最后统一施加</b>。
        /// </summary>
        /// <param name="gates">上层（状态效果 / 战斗）提交的门禁；空门禁时本层完全按自己的状态走。</param>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
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

            _dash.Configure(Direction(in ctx, _logic.Direction));

            return true;
        }

        /// <summary>
        /// 把门禁落到速度上 —— <b>全工程唯一"按上层要求改速度"的地方</b>。
        /// </summary>
        /// <remarks>
        /// 放在状态跑完之后：状态（<c>MoveState</c> / <c>IdleState</c>）会整体接管速度，
        /// 门禁写在它们前面等于没写 —— 这也是旧实现"挨打了却纹丝不动"的成因之一。
        /// </remarks>
        private void ApplyGates(in MoveGates gates)
        {
            if (gates.HasForcedVelocity) _logic.SetVelocity(gates.ForcedVelocity);
        }

        /// <summary>冲刺取向：优先用本帧输入方向，无输入则用最近朝向。</summary>
        private static Vector2 Direction(in LogicContext ctx, Vector2 fallback)
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
