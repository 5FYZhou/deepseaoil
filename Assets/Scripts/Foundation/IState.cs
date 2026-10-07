using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>状态机能看见的宿主：运动标量 ＋ 几个"提交速度"的入口。</summary>
    // 窄口是为了不反向依赖逻辑层的账本 ActorLogic；取数口是 Motion 的六个标量，非法值兜底见 MotionParams。
    public interface IStateHost
    {
        /// <summary>状态机能看见的运动标量（装配期由执行器折算一次）。</summary>
        MotionParams Motion { get; }

        /// <summary>移动层的"走"：有惯性按加速度逼近，零惯性当帧直达。</summary>
        void MoveTowards(Vector2 direction, float speed);

        /// <summary>移动层的"停"：有惯性滑停，零惯性当帧停。</summary>
        void BrakeTowards();

        /// <summary>直接接管两个分量的速度（冲刺这类"一次性"写法）。</summary>
        void SnapVelocity(Vector2 velocity);
    }

    // 状态接口：标签 ＋ 四个钩子（进 / 出 / 每帧 / 是否结束）。
    // 上下文是泛型参数，骨架不认识任何具体领域；参数按值传，实现方不用写修饰符。
    public interface IState<TStateTag, TContext>
    {
        TStateTag StateTag { get; }

        void Enter(TContext ctx);

        void Exit();

        void Tick(TContext ctx);

        /// <summary>状态是否已结束（结束即让位给状态组仲裁出的下一状态）。</summary>
        bool IsDone(TContext ctx);
    }

    public abstract class StateBase<TStateTag, TContext> : IState<TStateTag, TContext>
    {
        protected IStateHost Host { get; }

        protected MotionParams Motion => Host.Motion;

        protected StateBase(IStateHost host)
        {
            Host = host;
        }

        public abstract TStateTag StateTag { get; }

        public abstract void Enter(TContext ctx);

        public abstract void Exit();

        public abstract void Tick(TContext ctx);

        public abstract bool IsDone(TContext ctx);
    }
}
