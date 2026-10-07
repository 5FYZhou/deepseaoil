using System;
using System.Collections.Generic;

namespace DeepseaOil.Foundation
{
    /// <summary>泛型状态机：维护状态集，按 <c>Exit → Enter</c> 切换并广播；不含优先级判断——该不该切、按什么顺序由持有它的状态组回答。</summary>
    /// <remarks>首次进入不发 <see cref="OnStateChanged"/>（那是"进入"而不是"变化"）；广播参数为（当前、前一个）。</remarks>
    public sealed class StateMachine<TStateTag, TContext>
    {
        private readonly Dictionary<TStateTag, IState<TStateTag, TContext>> _states = new();

        /// <summary>当前状态；尚未进入任何状态时为 null。</summary>
        public IState<TStateTag, TContext> CurrentState { get; private set; }

        public event Action<TStateTag, TStateTag> OnStateChanged;

        public void AddState(IState<TStateTag, TContext> state)
        {
            _states[state.StateTag] = state;
        }

        /// <summary>切换到指定状态；未注册或已是当前状态时不做事、静默返回 false，不抛异常。</summary>
        public bool ChangeState(TStateTag nextTag, TContext ctx)
        {
            if (!_states.TryGetValue(nextTag, out var next)) return false;
            if (ReferenceEquals(next, CurrentState)) return false;

            bool hadPrevious = CurrentState != null;
            TStateTag previous = hadPrevious ? CurrentState.StateTag : default;

            CurrentState?.Exit();
            CurrentState = next;
            next.Enter(ctx);

            if (hadPrevious) OnStateChanged?.Invoke(nextTag, previous);
            return true;
        }
    }
}
