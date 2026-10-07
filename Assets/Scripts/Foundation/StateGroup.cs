using System.Collections.Generic;

namespace DeepseaOil.Foundation
{
    /// <summary>状态组通用骨架：持有状态机、按两段式仲裁转移，并把"当前状态"暴露给外部。</summary>
    /// <remarks>状态机只做"叫我切谁就切谁"；"该不该切、按什么顺序"由本类回答：子类用 <see cref="TryDecidePreempt"/>（评估）/ <see cref="TryCommitPreempt"/>（消费）/ <see cref="Fallback"/> 补自己的规则。
    /// 评估是纯查询：判不中就不消费任何缓冲与余额 —— 三个组必须共用同一套切换语义，否则差异只以手感形式出现。骨架只提供 <see cref="TickStates"/>，公开 <c>Tick</c> 由子类自己定（移动层要门禁、状态效果层要挂起的冲量）。</remarks>
    public abstract class StateGroup<TStateTag, TContext> where TStateTag : struct, System.Enum
    {
        private readonly StateMachine<TStateTag, TContext> _machine = new();

        protected abstract TStateTag EmptyTag { get; }

        /// <summary>当前状态标签；还没进入任何状态时是 <see cref="EmptyTag"/>。</summary>
        public TStateTag Current => _machine.CurrentState == null ? EmptyTag : _machine.CurrentState.StateTag;

        protected abstract TStateTag Fallback(in TContext ctx);

        /// <summary>状态切换回调（当前、前一个）；首次进入不发。</summary>
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

        /// <summary>抢占提交：消费余额并喂入场参数；判定已通过，返回 <c>false</c> 表示本帧不切换。</summary>
        protected virtual bool TryCommitPreempt(TStateTag target, in TContext ctx)
        {
            return false;
        }

        private TStateTag CheckTransitions(in TContext ctx)
        {
            IState<TStateTag, TContext> current = _machine.CurrentState;

            // 首帧没有当前状态：抢占照常判定，判不中才落到基础态（否则第一个物理帧成了"无抢占"特权帧）。
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
