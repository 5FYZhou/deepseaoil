using System;
using System.Collections.Generic;

namespace DeepseaOil.Logic.State
{
    /// <summary>
    /// 泛型状态机：维护状态集、执行 <c>Exit → Enter</c> 切换并广播变化。
    /// </summary>
    /// <remarks>
    /// 不含任何优先级判断——"该不该切、按什么顺序"由持有它的状态组回答（见 <c>MoveGroup</c>）。
    /// 首次进入不发 <see cref="OnStateChanged"/>：那是"进入"而不是"变化"。
    /// </remarks>
    public sealed class StateMachine<TStateTag>
    {
        private readonly Dictionary<TStateTag, IState<TStateTag>> _states = new();

        /// <summary>当前状态；尚未进入任何状态时为 null。</summary>
        public IState<TStateTag> CurrentState { get; private set; }

        /// <summary>状态发生切换时广播（当前、前一个）。</summary>
        public event Action<TStateTag, TStateTag> OnStateChanged;

        /// <summary>注册状态，以 <see cref="IState{TStateTag}.StateTag"/> 为键。</summary>
        public void AddState(IState<TStateTag> state)
        {
            _states[state.StateTag] = state;
        }

        /// <summary>切换到指定状态；未注册或已是当前状态时不做任何事。</summary>
        public bool ChangeState(TStateTag nextTag, in LogicContext ctx)
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
