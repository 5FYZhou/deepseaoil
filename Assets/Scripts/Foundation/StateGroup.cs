using System.Collections.Generic;

namespace DeepseaOil.Foundation
{
    /// <summary>状态组骨架，持状态机，两段式仲裁转移</summary>
    /// <remarks>子类用 TryDecidePreempt（评估）/ TryCommitPreempt（消费）/ Fallback 补规则，评估不消费。只提供 TickStates。</remarks>
    public abstract class StateGroup<TStateTag, TContext> where TStateTag : struct, System.Enum
    {
        private readonly StateMachine<TStateTag, TContext> _machine = new();

        protected abstract TStateTag EmptyTag { get; }

        /// <summary>当前状态，无状态时 EmptyTag</summary>
        public TStateTag Current => _machine.CurrentState == null ? EmptyTag : _machine.CurrentState.StateTag;

        protected abstract TStateTag Fallback(in TContext ctx);

        /// <summary>状态切换回调，首次不发</summary>
        protected event System.Action<TStateTag, TStateTag> StateChanged
        {
            add => _machine.OnStateChanged += value;
            remove => _machine.OnStateChanged -= value;
        }

        protected void AddState(IState<TStateTag, TContext> state)
        {
            _machine.AddState(state);
        }

        protected bool ChangeState(TStateTag tag, in TContext ctx)
        {
            return _machine.ChangeState(tag, ctx);
        }

        protected void TickStates(in TContext ctx)
        {
            _machine.ChangeState(CheckTransitions(in ctx), ctx);

            _machine.CurrentState?.Tick(ctx);
        }

        protected virtual bool TryDecidePreempt(in TContext ctx, out TStateTag target)
        {
            target = EmptyTag;
            return false;
        }

        /// <summary>抢占提交，消费余额，false 则不切换</summary>
        protected virtual bool TryCommitPreempt(TStateTag target, in TContext ctx)
        {
            return false;
        }

        private TStateTag CheckTransitions(in TContext ctx)
        {
            IState<TStateTag, TContext> current = _machine.CurrentState;

            // 首帧无当前状态，抢占照常判定，判不中才落到基础态
            if (TryDecidePreempt(in ctx, out TStateTag target)
                && (current == null || !EqualityComparer<TStateTag>.Default.Equals(target, current.StateTag))
                && TryCommitPreempt(target, in ctx))
            {
                return target;
            }

            if (current == null) return Fallback(in ctx);

            return current.IsDone(ctx) ? Fallback(in ctx) : current.StateTag;
        }
    }
}
