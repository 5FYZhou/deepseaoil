using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>角色逻辑基类，持执行器并推进状态机</summary>
    /// <remarks>帧骨架：BeginStep → OnTick → Commit；速度账本与控制律在 Motor。状态机只由 FixedTick 驱动。不读 Unity Time，时刻与 Δt 由 LogicContext 喂入，故 EditMode 可复现。</remarks>
    public abstract class ActorLogic : IFixedTickable, Foundation.IStateHost
    {
        /// <summary>移动执行器，速度账本与控制律，本帧速度唯一写者</summary>
        public IActorMotor Motor { get; }

        protected LogicContext Ctx { get; private set; }

        /// <remarks>共用运动参数由子类显式给出：Config 是虚属性，构造函数里读它是读未初始化对象</remarks>
        protected ActorLogic(IActorMotor motor, CharacterConfig config)
        {
            Motor = motor;
            motor.Configure(config);
        }

        public CharacterConfig Config => Motor.Config;

        /// <summary>推进物理帧，唯一驱动状态机处</summary>
        public void FixedTick(LogicContext ctx)
        {
            Ctx = ctx;

            Motor.BeginStep(ctx.now, ctx.deltaTime);

            OnTick(in ctx);

            Motor.Commit();
        }

        protected abstract void OnTick(in LogicContext ctx);

        Foundation.MotionParams Foundation.IStateHost.Motion => Motor.Motion;

        void Foundation.IStateHost.MoveTowards(Vector2 direction, float speed)
            => Motor.MoveTowards(direction, speed);

        void Foundation.IStateHost.BrakeTowards() => Motor.BrakeTowards();

        void Foundation.IStateHost.SnapVelocity(Vector2 velocity) => Motor.SnapVelocity(velocity);
    }
}
