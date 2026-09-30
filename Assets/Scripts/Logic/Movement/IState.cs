using DeepseaOil.Config;

namespace DeepseaOil.Logic.State
{
    /// <summary>
    /// 状态接口
    /// </summary>
    public interface IState<TStateTag>
    {
        /// <summary>本状态对应的枚举值。</summary>
        TStateTag StateTag { get; }

        /// <summary>进入状态时调用一次。</summary>
        void Enter(LogicContext ctx);

        /// <summary>离开状态时调用一次。</summary>
        void Exit();

        /// <summary>每物理帧调用。</summary>
        void Tick(LogicContext ctx);

        /// <summary>状态是否已结束（结束即让位给状态组仲裁出的下一状态）。</summary>
        bool IsDone(LogicContext ctx);
    }

    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class StateBase<TStateTag> : IState<TStateTag>
    {
        protected ActorLogic Logic { get; }
        protected CharacterConfig Config { get; }

        public abstract TStateTag StateTag { get; }

        protected StateBase(ActorLogic logic, CharacterConfig config)
        {
            Logic = logic;
            Config = config;
        }

        public abstract void Enter(LogicContext ctx);
        public abstract void Exit();
        public abstract void Tick(LogicContext ctx);
        public abstract bool IsDone(LogicContext ctx);
    }
}
