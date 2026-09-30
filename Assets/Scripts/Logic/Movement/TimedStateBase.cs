using DeepseaOil.Config;

namespace DeepseaOil.Logic.State
{
    /// <summary>
    /// 计时状态基类
    /// </summary>
    public abstract class TimedStateBase<TStateTag> : StateBase<TStateTag>
    {
        private float _enteredAt;
        private float _duration;

        protected TimedStateBase(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        /// <summary>本状态的持续时长（秒），由子类按当前模式给出。</summary>
        protected abstract float GetDuration(LogicContext ctx);

        /// <summary>进入状态时调用一次。</summary>
        public override void Enter(LogicContext ctx)
        {
            _duration = GetDuration(ctx);
            _enteredAt = ctx.now;
        }

        /// <summary>是否已到时长。</summary>
        public override bool IsDone(LogicContext ctx) => ctx.now - _enteredAt >= _duration;
    }
}
