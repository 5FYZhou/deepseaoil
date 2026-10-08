using System;
using System.Collections.Generic;

namespace DeepseaOil.Foundation
{
    /// <summary>泛型状态机，维护状态集，Exit → Enter 切换并广播</summary>
    public sealed class StateMachine<TStateTag, TContext>
    {
        private readonly Dictionary<TStateTag, IState<TStateTag, TContext>> _states = new();

        /// <summary>当前状态，无状态时 null</summary>
        public IState<TStateTag, TContext> CurrentState { get; private set; }

        public event Action<TStateTag, TStateTag> OnStateChanged;

        public void AddState(IState<TStateTag, TContext> state)
        {
            _states[state.StateTag] = state;
        }

        /// <summary>切换状态，未注册或已是当前态则静默返回 false</summary>
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
