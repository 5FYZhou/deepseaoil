using System.Collections.Generic;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 状态组通用骨架：持有状态机、按<b>两段式</b>仲裁转移，并把"当前状态"暴露给外部。
    /// </summary>
    /// <remarks>
    /// 状态机只做"叫我切谁就切谁"；"该不该切、按什么顺序"由本类回答 ——
    /// 子类用 <see cref="TryDecidePreempt"/>（评估）/ <see cref="TryCommitPreempt"/>（消费）/ <see cref="Fallback"/> 补上自己的规则。
    /// <para><b>"先判后消费"这条纪律现在属于骨架，不属于某一个组：</b>评估是纯查询，
    /// 判不中就不消费任何缓冲与余额。收口前它只写在移动组里，而领域式分层（移动 / 战斗 / 状态效果
    /// 各一台状态组）之后，三个组必须共用同一套切换语义，否则"同样是抢占，一边吃掉了余额、一边没吃"
    /// 这种事只会以手感差异的形式出现。</para>
    /// <para><b>为什么公开入口由子类自己定：</b>每个组要的东西不一样（移动层要门禁、状态效果层要挂起的冲量）。
    /// 骨架只提供 <see cref="TickStates"/>，谁暴露什么样的公开 <c>Tick</c> 由子类决定 ——
    /// 免得留一个"能调但语义不全"的公共入口。</para>
    /// </remarks>
    public abstract class StateGroup<TStateTag> where TStateTag : struct, System.Enum
    {
        private readonly StateMachine<TStateTag> _machine = new();

        /// <summary>"还没有进入任何状态"时对外报的标签（各组的 <c>Empty</c> 哨兵）。</summary>
        protected abstract TStateTag EmptyTag { get; }

        /// <summary>当前状态标签；还没进入任何状态时是 <see cref="EmptyTag"/>。</summary>
        public TStateTag Current => _machine.CurrentState == null ? EmptyTag : _machine.CurrentState.StateTag;

        /// <summary>基础态：没有更高优先级的抢占、且当前状态已结束时的归宿。</summary>
        protected abstract TStateTag Fallback(in LogicContext ctx);

        /// <summary>
        /// 状态切换回调（当前、前一个）；<b>首次进入不发</b>。
        /// </summary>
        /// <remarks>子类用它把切换发布成事实事件（如 <c>MovementStateChanged</c>）。
        /// 转发而不是直接暴露状态机：状态机是本骨架的内部件。</remarks>
        protected event System.Action<TStateTag, TStateTag> StateChanged
        {
            add => _machine.OnStateChanged += value;
            remove => _machine.OnStateChanged -= value;
        }

        /// <summary>注册一个状态（键 = 状态自己的 <see cref="IState{TStateTag}.StateTag"/>）。</summary>
        protected void AddState(IState<TStateTag> state)
        {
            _machine.AddState(state);
        }

        /// <summary>直接切换（子类在把入场参数喂好之后调）。</summary>
        protected bool ChangeState(TStateTag tag, in LogicContext ctx)
        {
            return _machine.ChangeState(tag, in ctx);
        }

        /// <summary>推进一帧：先仲裁，再用（可能已切换的）当前状态跑一次。</summary>
        protected void TickStates(in LogicContext ctx)
        {
            _machine.ChangeState(CheckTransitions(in ctx), ctx);

            _machine.CurrentState?.Tick(ctx);
        }

        /// <summary>抢占判定：<b>纯查询</b>，不消费任何缓冲与余额。</summary>
        protected virtual bool TryDecidePreempt(in LogicContext ctx, out TStateTag target)
        {
            target = EmptyTag;
            return false;
        }

        /// <summary>抢占提交：消费余额并喂入场参数；判定已通过，返回 <c>false</c> 表示本帧不切换。</summary>
        protected virtual bool TryCommitPreempt(TStateTag target, in LogicContext ctx)
        {
            return false;
        }

        private TStateTag CheckTransitions(in LogicContext ctx)
        {
            IState<TStateTag> current = _machine.CurrentState;

            // 首帧没有当前状态：抢占照常判定，判不中才落到基础态。
            // 不能让首帧直接走基础态——那会让第一个物理帧成为"无抢占"特权帧。
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
