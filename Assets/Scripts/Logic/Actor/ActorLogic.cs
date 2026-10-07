using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>角色逻辑基类：持有执行器 ＋ 每物理帧推进状态机，向<b>组合件</b>退化。</summary>
    /// <remarks><b>帧的骨架收在这里：</b><c>BeginStep → OnTick → Commit</c>（速度账本与控制律都已下放到 <see cref="IActorMotor"/>，本类不转发任何速度写入）。
    /// <b>全工程只有本类的 <see cref="FixedTick"/> 发起这条序列</b>，状态机也只由它驱动 —— 这是"每帧只有一个速度写者"的实现形态，不要从别处驱动状态机。
    /// <b>不读 Unity <c>Time</c></b>：时刻与 Δt 由 <see cref="LogicContext"/> 逐帧喂入，于是整套行为能在 EditMode 里喂 dt 复现。</remarks>
    public abstract class ActorLogic : IFixedTickable, Foundation.IStateHost
    {
        /// <summary>移动执行器：<b>速度账本的持有者、控制律的实现处、也是本帧速度的唯一写者</b>。</summary>
        public IActorMotor Motor { get; }

        protected LogicContext Ctx { get; private set; }

        /// <remarks>角色共用运动参数<b>由子类显式给出</b>：本类的 <see cref="Config"/> 是虚属性，构造函数里读它就是读一个尚未初始化的派生对象。</remarks>
        protected ActorLogic(IActorMotor motor, CharacterConfig config)
        {
            Motor = motor;
            motor.Configure(config);
        }

        public CharacterConfig Config => Motor.Config;

        /// <summary>推进一个物理帧（<b>全工程唯一驱动状态机的地方</b>）。</summary>
        public void FixedTick(LogicContext ctx)
        {
            Ctx = ctx;

            Motor.BeginStep(ctx.now, ctx.deltaTime);

            OnTick(in ctx);

            Motor.Commit();
        }

        protected abstract void OnTick(in LogicContext ctx);

        /// <inheritdoc />
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
