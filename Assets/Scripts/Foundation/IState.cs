using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>状态机能看见的宿主</summary>
    // 窄口：不反向依赖逻辑层 ActorLogic；取数口为 Motion 六标量
    public interface IStateHost
    {
        MotionParams Motion { get; }

        /// <summary>有惯性按加速度逼近，零惯性当帧直达</summary>
        void MoveTowards(Vector2 direction, float speed);

        /// <summary>有惯性滑停，零惯性当帧停</summary>
        void BrakeTowards();

        void SnapVelocity(Vector2 velocity);
    }

    // 状态接口：标签＋四个钩子（进/出/每帧/是否结束）
    public interface IState<TStateTag, TContext>
    {
        TStateTag StateTag { get; }

        void Enter(TContext ctx);

        void Exit();

        void Tick(TContext ctx);

        /// <summary>结束即让位给仲裁的下一状态</summary>
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
