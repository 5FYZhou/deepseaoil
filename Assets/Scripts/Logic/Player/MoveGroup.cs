using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;

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
    /// 优先级与设计理由见 <c>Docs/M1微规划.md</c>。
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
            _machine.AddState(new JumpState(logic, logic.Config));
            _machine.AddState(new DoubleJumpState(logic, logic.Config));
            _machine.AddState(new FallState(logic, logic.Config));
            _machine.AddState(_dash);
            _machine.AddState(new WallSlideState(logic, logic.Config));

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

        private MovementStateTag CheckTransitions(in LogicContext ctx)
        {
            var current = _machine.CurrentState;
            if (current == null) return GetFallBackState(in ctx);

            // 目标等于当前状态时不算抢占：既不消费，也不能遮掉 IsDone（否则定时状态永远退不出去）。
            if (TryDecidePreempt(in ctx, out MovementStateTag target)
                && target != current.StateTag
                && TryCommitPreempt(target, in ctx))
            {
                return target;
            }

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

            if (ctx.worldInfo.Grounded)
            {
                if (_logic.WouldJumpFromGround(ctx.now))
                {
                    target = MovementStateTag.Jump;
                    return true;
                }
            }
            // 贴墙时跳过通用跳跃：蹬墙跳由 WallSlideState 自己处理，否则二段跳会先抢走这次按下。
            else if (!ctx.worldInfo.TouchingWall)
            {
                // 土狼跳优先于二段跳：前者不占空中余额。
                if (_logic.CanGroundJump(ctx.now) && _logic.HasBufferedJump(ctx.now))
                {
                    target = MovementStateTag.Jump;
                    return true;
                }

                if (_logic.CanDoubleJump && _logic.HasBufferedJump(ctx.now))
                {
                    target = MovementStateTag.DoubleJump;
                    return true;
                }
            }

            target = MovementStateTag.Empty;
            return false;
        }

        /// <summary>抢占提交：消费余额并喂入场参数；判定已通过，失败即本帧不切换。</summary>
        private bool TryCommitPreempt(MovementStateTag target, in LogicContext ctx)
        {
            switch (target)
            {
                case MovementStateTag.Dash:
                    if (!_logic.TryConsumeDash(ctx.now)) return false;

                    _dash.Configure(Direction(in ctx, _logic.Facing));
                    return true;

                case MovementStateTag.Jump:
                    return _logic.TryConsumeJump(ctx.now, airJump: false);

                case MovementStateTag.DoubleJump:
                    return _logic.TryConsumeJump(ctx.now, airJump: true);

                default:
                    return false;
            }
        }

        /// <summary>基础态：当前状态结束后该回到的"无事可做"状态。</summary>
        private static MovementStateTag GetFallBackState(in LogicContext ctx)
        {
            if (ctx.worldInfo.Grounded)
            {
                return ctx.inputSnapshot.Move.x != 0f ? MovementStateTag.Move : MovementStateTag.Idle;
            }

            return ctx.worldInfo.TouchingWall ? MovementStateTag.WallSlide : MovementStateTag.Fall;
        }

        private static int Direction(in LogicContext ctx, int fallback)
        {
            if (ctx.inputSnapshot.Move.x > 0f) return 1;
            if (ctx.inputSnapshot.Move.x < 0f) return -1;
            return fallback;
        }

        private static void OnStateChanged(MovementStateTag current, MovementStateTag previous)
        {
            EventBus<MovementStateChanged>.Publish(new MovementStateChanged(current, previous));
        }
    }
}
