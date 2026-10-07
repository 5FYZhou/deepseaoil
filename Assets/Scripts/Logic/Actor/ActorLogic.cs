using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 角色逻辑基类：<b>只做"持有执行器 ＋ 每物理帧推进状态机"两件事</b>，向<b>组合件</b>退化。
    /// </summary>
    /// <remarks>
    /// <b>速度账本与控制律已经下放到执行器</b>（<see cref="IActorMotor"/>）：审查已定
    /// "把 <c>Motor</c> 开放出来，具体逻辑放在 Motor 才是正确的"。本类因此不再转发任何
    /// 速度写入或控制律方法 —— 谁要写速度、谁要算朝向，直接拿 <see cref="Motor"/>。
    /// <para><b>帧的骨架仍然收在这里：</b><c>BeginStep → OnTick → Commit</c>。
    /// 这条序列是"每帧只有一个速度写者"的实现形态，全工程只有本方法发起它
    /// （状态机也只由本方法驱动，所以"谁先算、谁后写"是确定的）。</para>
    /// <para><b>不读 Unity <c>Time</c></b>：时刻与 Δt 由 <see cref="LogicContext"/> 逐帧喂入
    /// （本类把它们原样交给执行器），于是整套行为能在 EditMode 里喂 dt 复现。</para>
    /// <para><b>本类不保存任何"上一帧输入"状态</b>：输入边沿统一由 <c>InputBuffer</c> 提供。</para>
    /// </remarks>
    public abstract class ActorLogic : IFixedTickable, Foundation.IStateHost
    {
        /// <summary>
        /// 移动执行器：<b>速度账本的持有者、控制律的实现处、也是本帧速度的唯一写者</b>。
        /// </summary>
        /// <remarks>公开是刻意的（审查："把 Motor 开放出去"）——
        /// 状态层、移动层、调试件都直接用它，不再经本类转一手。</remarks>
        public IActorMotor Motor { get; }

        /// <summary>本帧喂入的快照，全帧唯一来源。</summary>
        protected LogicContext Ctx { get; private set; }

        /// <param name="motor">移动执行器（账本 ＋ 控制律 ＋ 物理体读写）。</param>
        /// <param name="config">
        /// 角色共用运动参数。<b>由子类显式给出</b>：本类的配置读法（<see cref="Config"/>）是虚属性，
        /// 构造函数里读它就是读一个尚未初始化的派生对象（C# 的经典陷阱）。
        /// </param>
        protected ActorLogic(IActorMotor motor, CharacterConfig config)
        {
            Motor = motor;
            motor.Configure(config);
        }

        /// <summary>
        /// 角色共用运动参数 —— 执行器持有的那一份（<see cref="IActorMotor.Configure"/> 的同源读法）。
        /// </summary>
        /// <remarks>
        /// <b>它的读者只有装配链</b>：状态机读的是 <see cref="Foundation.IStateHost.Motion"/>
        /// （六个标量），本类不再把它摆给骨架看。保留它是因为子类的构造函数要吃它。
        /// <para>惰性读取而不是构造时缓存：配置由 <see cref="ActorLogic"/> 的构造函数写入执行器，
        /// 缓存一份等于同一件事有两个真值。</para>
        /// </remarks>
        public CharacterConfig Config => Motor.Config;

        /// <summary>推进一个物理帧。</summary>
        /// <remarks>
        /// <b>零提交帧不写速度</b>由执行器的 <c>Commit</c> 保证（没有变更就不覆盖引擎，
        /// 第二个写者因此不会被清掉）。
        /// </remarks>
        public void FixedTick(LogicContext ctx)
        {
            Ctx = ctx;

            Motor.BeginStep(ctx.now, ctx.deltaTime);

            OnTick(in ctx);

            Motor.Commit();
        }

        /// <summary>子类的账本维护与状态驱动。</summary>
        protected abstract void OnTick(in LogicContext ctx);

        // ─────────────────────────────────────────────
        // IStateHost：状态机骨架看得见的窄口，全部转发给执行器
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        /// <remarks>
        /// <b>只有这一个标量口，没有配置口。</b>骨架要 <c>moveSpeed</c> 这类数时经它取 ——
        /// 于是 <c>Foundation</c> 不认识 <c>CharacterConfig</c>（那曾是全工程唯一的逆向层依赖）。
        /// </remarks>
        Foundation.MotionParams Foundation.IStateHost.Motion => Motor.Motion;

        /// <inheritdoc />
        void Foundation.IStateHost.MoveTowards(Vector2 direction, float speed)
            => Motor.MoveTowards(direction, speed);

        /// <inheritdoc />
        void Foundation.IStateHost.BrakeTowards() => Motor.BrakeTowards();

        /// <inheritdoc />
        void Foundation.IStateHost.SnapVelocity(Vector2 velocity) => Motor.SnapVelocity(velocity);
    }
}
