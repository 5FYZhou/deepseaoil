using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 角色逻辑基类：持有执行器与角色配置，提供所有角色共用的速度、重力与朝向操作。
    /// </summary>
    /// <remarks>
    /// 不读 Unity Time、不继承 MonoBehaviour：时间与环境由 <see cref="LogicContext"/> 逐帧喂入。
    /// 本类<b>不保存任何"上一帧输入"状态</b>：输入边沿统一由 <c>InputBuffer</c> 提供（见 <c>Docs/M1微规划.md</c> §三 D14）。
    /// <b>速度真值只有引擎一份</b>：帧首读真值、帧内只累加本帧的提交、帧末一次写出；速度类变量一律不跨帧（见 <c>Docs/速度设计.md</c>）。
    /// 提交分两类：瞬变累进 <c>_delta</c>（格/秒），加速度累进 <c>_accel</c>（格/秒²），帧末统一乘一次 Δt。
    /// </remarks>
    public abstract class ActorLogic : IFixedTickable
    {
        private float _moveLockUntil = float.NegativeInfinity;
        private Vector2 _frameStart;
        private Vector2 _delta;
        private Vector2 _accel;
        private bool _gravity = true;

        /// <summary>移动执行器。</summary>
        protected IMovementMotor Motor { get; }

        /// <summary>角色共用运动参数。</summary>
        public CharacterConfig Config { get; }

        /// <summary>本帧喂入的快照，全帧唯一来源。</summary>
        protected LogicContext Ctx { get; private set; }

        protected ActorLogic(IMovementMotor motor, CharacterConfig config)
        {
            Motor = motor;
            Config = config;
        }

        /// <summary>本帧工作速度：帧首真值 + 本帧已提交的全部变更。</summary>
        public Vector2 Velocity => _frameStart + _delta + _accel * Ctx.deltaTime;

        /// <summary>
        /// 帧首读到的真值。
        /// </summary>
        /// <remarks>它是<b>上一物理步结束时</b>的速度：碰撞解算发生在 FixedUpdate 之后，故本帧的碰撞结果要下一帧才读得到。</remarks>
        public Vector2 FrameStartVelocity => _frameStart;

        /// <summary>本帧净提交的速度变化量；与引擎回读值对照即可看出引擎是否否决。</summary>
        public Vector2 SubmittedDelta => _delta + _accel * Ctx.deltaTime;

        /// <summary>当前朝向：-1 左，1 右。</summary>
        public int Facing => Motor.Facing;

        /// <summary>本帧是否站在地面。</summary>
        public bool IsGrounded => Ctx.worldInfo.Grounded;

        /// <summary>推进一个物理帧。</summary>
        public void FixedTick(LogicContext ctx)
        {
            Ctx = ctx;
            _frameStart = Motor.Velocity;
            _delta = Vector2.zero;
            _accel = Vector2.zero;

            OnTick(in ctx);

            // 零提交帧不写速度：没有变更就不必覆盖引擎，第二个写者因此不会被清掉。
            if (!HasSubmission) return;

            Motor.Move(_frameStart + _delta + _accel * ctx.deltaTime);
        }

        /// <summary>子类的账本维护与状态驱动。</summary>
        protected abstract void OnTick(in LogicContext ctx);

        /// <summary>
        /// 尝试消费一次跳跃输入，供移动状态使用；<paramref name="airJump"/> 为真时占用一次空中跳跃余额。
        /// </summary>
        /// <remarks>缓冲窗口与余额的数值属于具体角色，故由子类实现；状态只表达"我要跳"。</remarks>
        public abstract bool TryConsumeJump(float now, bool airJump);

        /// <summary>贴墙时走一次蹬墙跳：给一次斜向初速并锁定水平输入。</summary>
        /// <remarks>初速与锁定时长属于具体角色，故由子类实现。</remarks>
        public abstract bool TryWallJump(in LogicContext ctx);

        /// <summary>累加一次冲量：一次性的速度变化，单位格/秒，<b>不</b>乘 Δt。</summary>
        public void AddImpulse(Vector2 deltaVelocity) => _delta += deltaVelocity;

        /// <summary>累加一次持续力：单位格/秒²，每帧提交，本帧贡献 = 该值 × Δt。</summary>
        public void AddForce(Vector2 acceleration) => _accel += acceleration;

        /// <summary>按输入方向加速到目标速度；移动锁定期内不响应，并按输入方向更新朝向。</summary>
        public void MoveHorizontal(float inputX, float targetSpeed, float accel)
        {
            if (IsMoveLocked) return;

            FaceTowards(inputX > 0f ? 1 : inputX < 0f ? -1 : 0);
            ApproachX(inputX * targetSpeed, accel);
        }

        /// <summary>无水平输入时把水平速度渐减到 0；移动锁定期内不响应。</summary>
        public void BrakeHorizontal(float accel)
        {
            if (IsMoveLocked) return;

            ApproachX(0f, accel);
        }

        /// <summary>空中横向控制。</summary>
        public void AirMove(in InputSnapshot input)
        {
            MoveHorizontal(input.Move.x, Config.moveSpeed, Config.moveAcceleration);
        }

        /// <summary>急停：水平速度立刻归零；移动锁定期内不响应。</summary>
        public void StopHorizontal()
        {
            if (IsMoveLocked) return;

            SetVelocityX(0f);
        }

        /// <summary>把下落速度硬钳到上限（只在下越界时提交）。</summary>
        public void ClampFallSpeed(float maxFall)
        {
            if (Velocity.y < -maxFall) SetVelocityY(-maxFall);
        }

        /// <summary>渐进逼近目标速度；控制律的唯一实现点（见 <c>Docs/速度设计.md</c> §五）。</summary>
        /// <remarks>反向输入时改走衰减率，两支取较快者——纯指数衰减永不反向，直接用它会把角色停在原地。</remarks>
        public void ApproachX(float target, float accel)
        {
            float current = Velocity.x;
            float delta = Mathf.Clamp(target - current, -accel * Ctx.deltaTime, accel * Ctx.deltaTime);

            if (target != 0f
                && current != 0f
                && (target > 0f) != (current > 0f))
            {
                float decay = -Config.turnDecayRate * current * Ctx.deltaTime;
                if (Mathf.Abs(decay) > Mathf.Abs(delta)) delta = decay;
            }

            _delta.x += delta;
        }

        /// <summary>接管两个分量：本帧工作速度即为给定值，覆盖已提交的（含重力）。</summary>
        public void SnapVelocity(Vector2 velocity)
        {
            SetVelocityX(velocity.x);
            SetVelocityY(velocity.y);
            FaceTowards(velocity.x > 0f ? 1 : velocity.x < 0f ? -1 : 0);
        }

        /// <summary>把垂直分量瞬变到给定值：该分量已被接管，撤掉它上面待生效的加速度。</summary>
        public void SetVelocityY(float vy)
        {
            _accel.y = 0f;
            _delta.y = vy - _frameStart.y;
        }

        /// <summary>面向给定方向。</summary>
        public void FaceTowards(int direction)
        {
            if (direction != 0) Motor.Facing = direction;
        }

        /// <summary>开关重力累加。</summary>
        public void SetGravity(bool enabled) => _gravity = enabled;

        /// <summary>在接下来的 <paramref name="duration"/> 秒内不响应水平输入。</summary>
        public void StartMoveLock(float now, float duration)
        {
            _moveLockUntil = now + duration;
        }

        /// <summary>
        /// 提交重力、可变跳高截断与垂直上限钳制。
        /// </summary>
        /// <param name="jumpReleased">本帧是否为"按住 → 松开"的边沿，由 <c>InputBuffer.IsJumpReleased()</c> 提供。</param>
        /// <remarks>钳制与重力同在垂直分量上，必须看到含重力的值，故顺序固定为 截断 → 重力 → 钳制。</remarks>
        protected void ApplyGravity(in LogicContext ctx, bool jumpReleased)
        {
            if (!_gravity) return;

            bool jumpHeld = ctx.inputSnapshot.JumpHeld;

            // 上升期松开跳跃键：只截断一次（jumpReleased 已含"上一帧按住"）。
            if (jumpReleased && Velocity.y > 0f)
            {
                SetVelocityY(Velocity.y * Config.jumpCutMultiplier);
            }

            float gravity = jumpHeld && Velocity.y > 0f ? Config.riseGravity : Config.fallGravity;
            _accel.y -= gravity;

            if (Velocity.y > Config.maxRiseSpeed) SetVelocityY(Config.maxRiseSpeed);
            else if (Velocity.y < -Config.maxFallSpeed) SetVelocityY(-Config.maxFallSpeed);
        }

        /// <summary>把水平分量瞬变到给定值：该分量已被接管，撤掉它上面待生效的加速度。</summary>
        private void SetVelocityX(float vx)
        {
            _accel.x = 0f;
            _delta.x = vx - _frameStart.x;
        }

        private bool HasSubmission => _delta.x != 0f || _delta.y != 0f || _accel.x != 0f || _accel.y != 0f;

        private bool IsMoveLocked => Ctx.now < _moveLockUntil;
    }
}
