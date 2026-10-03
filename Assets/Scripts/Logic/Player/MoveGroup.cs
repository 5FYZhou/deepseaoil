using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 移动状态组：持有状态实例与状态机，并逐帧按优先级链仲裁转移。
    /// </summary>
    /// <remarks>
    /// 状态机只做"叫我切谁就切谁"，本类负责全部"该不该切、按什么顺序"。
    /// 仲裁分两段：当前状态结束 → 基础态；否则只有更高级别的条件才能抢占。
    /// 抢占**先判后消费**：判定是纯查询，目标等于当前状态时不消费任何缓冲与余额。
    /// 状态类本身是通用件（只依赖 <see cref="ActorLogic"/>），本类因查询玩家资格而属玩家专属。
    /// 俯视角下抢占链只剩冲刺一条；加新状态时在 <c>AddState</c> 注册、在 <c>TryDecidePreempt</c> 排优先级。
    /// </remarks>
    public sealed class MoveGroup
    {
        private readonly PlayerLogic _logic;
        private readonly StateMachine<MovementStateTag> _machine = new();

        private readonly DashState _dash;

        public MoveGroup(PlayerLogic logic)
        {
            _logic = logic;

            _dash = new DashState(logic, logic.Config);

            _machine.AddState(new IdleState(logic, logic.Config));
            _machine.AddState(new MoveState(logic, logic.Config));
            _machine.AddState(_dash);

            _machine.OnStateChanged += OnStateChanged;
        }

        /// <summary>当前状态标签。</summary>
        public MovementStateTag Current
        {
            get
            {
                var state = _machine.CurrentState;
                return state == null ? MovementStateTag.Idle : state.StateTag;
            }
        }

        /// <summary>推进一个物理帧：先仲裁，再用（可能已切换的）当前状态跑一次。</summary>
        public void Tick(in LogicContext ctx)
        {
            _machine.ChangeState(CheckTransitions(in ctx), ctx);
            _machine.CurrentState.Tick(ctx);
        }

        /// <summary>冲刺状态实例，供调试与测试读取其入场方向。</summary>
        /// <remarks>只读用途。状态类本身不该被外部配置，入场参数一律经 <see cref="TryCommitPreempt"/> 喂入。</remarks>
        public DashState Dash => _dash;

        private MovementStateTag CheckTransitions(in LogicContext ctx)
        {
            var current = _machine.CurrentState;

            // 首帧没有当前状态：抢占照常判定，判不中才落到基础态。
            // 不能让首帧直接 return 基础态——那会让第一个物理帧成为"无抢占"特权帧，
            // 玩家的第一次按下（冷却与缓冲都成立）会被吞掉，要到第二帧才生效。
            if (TryDecidePreempt(in ctx, out MovementStateTag target)
                && (current == null || target != current.StateTag)
                && TryCommitPreempt(target, in ctx))
            {
                return target;
            }

            if (current == null) return GetFallBackState(in ctx);

            return current.IsDone(ctx) ? GetFallBackState(in ctx) : current.StateTag;
        }

        /// <summary>抢占判定：纯查询，不消费任何缓冲与余额。</summary>
        private bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            if (_logic.CanDash(ctx.now))
            {
                target = MovementStateTag.Dash;
                return true;
            }

            target = MovementStateTag.Empty;
            return false;
        }

        /// <summary>抢占提交：消费余额并喂入场参数；判定已通过，失败即本帧不切换。</summary>
        /// <remarks>
        /// <b>喂方向必须在消费成功之后</b>：<c>Configure</c> 会改写状态对象的字段，若放在消费之前，
        /// 提交失败（缓冲窗口已过）时状态已被改过，留下一个与"上次真正冲刺过的方向"不符的脏值。
        /// </remarks>
        private bool TryCommitPreempt(MovementStateTag target, in LogicContext ctx)
        {
            switch (target)
            {
                case MovementStateTag.Dash:
                    if (!_logic.TryConsumeDash(ctx.now)) return false;

                    _dash.Configure(Direction(in ctx, _logic.Direction));
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>基础态：当前状态结束后该回到的"无事可做"状态。俯视角只有走/站两档。</summary>
        private static MovementStateTag GetFallBackState(in LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude > 0f
                ? MovementStateTag.Move
                : MovementStateTag.Idle;
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
