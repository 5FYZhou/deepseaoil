using System;
using System.Collections.Generic;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 泛型状态机：维护状态集、执行 <c>Exit → Enter</c> 切换并广播变化。
    /// </summary>
    /// <remarks>
    /// 不含任何优先级判断——"该不该切、按什么顺序"由持有它的状态组回答（见 <see cref="StateGroup{TStateTag, TContext}"/>）。
    /// 首次进入不发 <see cref="OnStateChanged"/>：那是"进入"而不是"变化"。
    /// <para><b>上下文是泛型参数</b>（见 <see cref="IState{TStateTag, TContext}"/> 的说明）：
    /// 骨架因此能在不认识 <c>LogicContext</c> 的前提下服务多个领域层。</para>
    /// </remarks>
    public sealed class StateMachine<TStateTag, TContext>
    {
        private readonly Dictionary<TStateTag, IState<TStateTag, TContext>> _states = new();

        /// <summary>当前状态；尚未进入任何状态时为 null。</summary>
        public IState<TStateTag, TContext> CurrentState { get; private set; }

        /// <summary>状态发生切换时广播（当前、前一个）。</summary>
        public event Action<TStateTag, TStateTag> OnStateChanged;

        /// <summary>注册状态，以 <see cref="IState{TStateTag, TContext}.StateTag"/> 为键。</summary>
        public void AddState(IState<TStateTag, TContext> state)
        {
            _states[state.StateTag] = state;
        }

        /// <summary>切换到指定状态；未注册或已是当前状态时不做任何事。</summary>
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
