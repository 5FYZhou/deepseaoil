using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 状态机能看见的宿主：<b>运动标量 ＋ 几个"提交速度"的入口</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么是接口而不是具体账本：</b>状态机骨架住在地基（<c>Foundation</c>），
    /// 而账本 <c>ActorLogic</c> 住在逻辑层 —— 直接吃具体类型会让地基反向依赖逻辑层
    /// （审查点名的技术障碍）。收窄成这几个入口之后，地基只知道"宿主能接收方向与速度"。
    /// <para><b>它不认识角色配置。</b>本接口曾暴露 <c>DeepseaOil.Data.CharacterConfig</c>，
    /// 那是全工程唯一的逆向层依赖；现在取数口是 <see cref="Motion"/>（六个标量），
    /// 地基因此不认识数据层，也不认识任何具体角色。见 <see cref="MotionParams"/> 的类注释。</para>
    /// <para><b>接口只列状态真正用到的东西</b>：多列一个方法就多一个必须实现的成员，
    /// 而"实现一个没人调的方法"正是接口膨胀的开端。</para>
    /// </remarks>
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

    /// <summary>
    /// 状态接口：<b>标签 ＋ 四个钩子</b>（进 / 出 / 每帧 / 是否结束）。
    /// </summary>
    /// <typeparam name="TStateTag">状态标签（枚举）。</typeparam>
    /// <typeparam name="TContext">每帧喂进来的上下文（各领域自己的结构体）。</typeparam>
    /// <remarks>
    /// <b>为什么上下文是泛型参数：</b>骨架在地基，而上下文（<c>LogicContext</c>）是逻辑层的东西 ——
    /// 泛型化之后骨架不认识任何具体领域，同一套机器可以服务移动、状态效果、战斗三个层。
    /// 代价是每个状态类多写一个类型实参：一次性、而且编译器会拦。
    /// <para><b>参数按值传</b>（不是 <c>in</c>）：上下文是小结构体，
    /// 而"实现方不用关心参数修饰符"让状态类可以照旧只写 <c>LogicContext ctx</c>。</para>
    /// </remarks>
    public interface IState<TStateTag, TContext>
    {
        /// <summary>本状态对应的标签。</summary>
        TStateTag StateTag { get; }

        /// <summary>进入状态时调用一次。</summary>
        void Enter(TContext ctx);

        /// <summary>离开状态时调用一次。</summary>
        void Exit();

        /// <summary>每物理帧调用。</summary>
        void Tick(TContext ctx);

        /// <summary>状态是否已结束（结束即让位给状态组仲裁出的下一状态）。</summary>
        bool IsDone(TContext ctx);
    }

    /// <summary>
    /// 状态基类：把宿主与配置摆好，子类只写自己的四个钩子。
    /// </summary>
    public abstract class StateBase<TStateTag, TContext> : IState<TStateTag, TContext>
    {
        /// <summary>宿主（运动标量的来源，也是"提交速度"的入口）。</summary>
        protected IStateHost Host { get; }

        /// <summary>运动标量（<see cref="IStateHost.Motion"/> 的转发，读起来短一点）。</summary>
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
