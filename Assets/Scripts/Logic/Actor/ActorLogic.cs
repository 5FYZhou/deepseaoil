using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 角色逻辑基类：持有执行器与角色配置，提供所有角色共用的速度、外力与朝向操作。
    /// </summary>
    /// <remarks>
    /// 不读 Unity Time、不继承 MonoBehaviour：时间与环境由 <see cref="LogicContext"/> 逐帧喂入。
    /// 本类<b>不保存任何"上一帧输入"状态</b>：输入边沿统一由 <c>InputBuffer</c> 提供（见 <c>Docs/框架设计/分层设计/逻辑层.md</c> §5）。
    /// <b>速度真值只有引擎一份</b>：帧首读真值、帧内只累加本帧的提交、帧末一次写出；速度类变量一律不跨帧（见 <c>Docs/框架设计/分层设计/逻辑层.md</c>）。
    /// 提交分两类：瞬变累进 <c>_delta</c>（格/秒），加速度累进 <c>_accel</c>（格/秒²），帧末统一乘一次 Δt。
    /// </remarks>
    public abstract class ActorLogic : IFixedTickable, Foundation.IStateHost
    {
        private float _moveLockUntil = float.NegativeInfinity;
        private Vector2 _frameStart;
        private Vector2 _delta;
        private Vector2 _accel;
        private float _extraForceScale = 1f;
        private float _speedLimit = float.PositiveInfinity;

        /// <summary>本帧的速度乘数（门禁提交；1 = 不缩放）。<b>帧首复位</b>。</summary>
        private float _speedScale = 1f;

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

        /// <summary>当前朝向（世界方向向量）；俯视角为 8 向，平台跳跃时代是 -1/1。</summary>
        public Vector2 Facing => Motor.Facing;

        /// <summary>推进一个物理帧。</summary>
        public void FixedTick(LogicContext ctx)
        {
            Ctx = ctx;
            _frameStart = Motor.Velocity;
            _delta = Vector2.zero;
            _accel = Vector2.zero;

            // 上限是**一次性**的：上一帧声明的到这一帧开头就失效。不这样做的话"这一帧被推了一下"
            // 会变成"从此一直被限速"——玩家永久失去一部分速度，而没有任何东西会报错。
            _speedLimit = float.PositiveInfinity;

            // 速度乘数同样是**一次性**的：门禁必须每帧重新提交。忘了提交等于"这一帧没减速"，
            // 而不是"从此一直慢"——后者会在泥浆消失后表现为"角色怎么突然不听话了"。
            _speedScale = 1f;

            OnTick(in ctx);

            // 零提交帧不写速度：没有变更就不必覆盖引擎，第二个写者因此不会被清掉。
            if (!HasSubmission) return;

            Motor.Move(Clamped(_frameStart + _delta + _accel * ctx.deltaTime));
        }

        /// <summary>子类的账本维护与状态驱动。</summary>
        protected abstract void OnTick(in LogicContext ctx);

        /// <summary>累加一次冲量：一次性的速度变化，单位格/秒，<b>不</b>乘 Δt。</summary>
        public void AddImpulse(Vector2 deltaVelocity) => _delta += deltaVelocity;

        /// <summary>累加一次持续力：单位格/秒²，每帧提交，本帧贡献 = 该值 × Δt。</summary>
        public void AddForce(Vector2 acceleration) => _accel += acceleration;

        /// <summary>
        /// 俯视角移动：把速度整体接管为 <paramref name="direction"/> × <paramref name="speed"/>（零惯性，无加减速）。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="ApproachX"/> 的分工：那条是"渐进逼近"的控制律，给需要惯性的角色（敌人追击、击退滑行）用；
        /// 本条是"当帧直达"，给俯视角玩家用——松键当帧停，反向当帧换向。
        /// 移动锁定期内不响应，但 <b>朝向照更新</b>：锁定期是"不能位移"，不是"不能转身"。
        /// <paramref name="direction"/> <b>可以是未归一化向量</b>（本方法内归一化）：
        /// 契约不靠调用方守，否则键盘斜向的 (1,1) 会让速度凭空快 √2 倍，且不报错、只是手感不对。
        /// </remarks>
        public void MoveDirection(Vector2 direction, float speed)
        {
            FaceTowards(direction);

            if (IsMoveLocked) return;

            SnapVelocity(direction.normalized * speed);
        }

        /// <summary>急停：速度当帧归零（朝向不变）；移动锁定期内不响应。</summary>
        /// <remarks>
        /// <b>它的语义是"硬停"</b>（重生、禁用、速度被外力完全接管时用）。
        /// 手感上的"松手滑停"走 <see cref="BrakeTowards"/> —— 两者刻意分开，
        /// 否则"想立刻停住"的地方会静默变成"滑一段"，而那种偏差只表现为"角色不听话"。
        /// </remarks>
        public void StopMove()
        {
            if (IsMoveLocked) return;

            SnapVelocity(Vector2.zero);
        }

        /// <summary>
        /// 本帧的速度乘数（<c>1</c> = 不缩放）；由移动层的门禁每帧提交。
        /// </summary>
        /// <remarks>
        /// <b>它落在"目标速度"上而不是乘在已提交的速度上</b>（见 <see cref="MoveTowards"/>）：
        /// 每帧把整体速度乘一次会与加速度互相拉锯 —— 稳态速度远低于"配置速度 × 乘数"。
        /// <para>帧首复位为 <c>1</c>（与速度上限同一条纪律）：门禁必须每帧重新提交。</para>
        /// </remarks>
        public float SpeedScale => _speedScale;

        /// <summary>
        /// 设置本帧的速度乘数。
        /// </summary>
        /// <param name="speedScale">乘数；<c>&gt; 1</c> 夹到 1（加速是另一件事），负数夹到 0（定住而不是反向推）。</param>
        /// <remarks>非数按 <c>1</c>（不起作用）处理：非数一旦进入速度就会让角色带着非数坐标消失，且不报错。</remarks>
        public void SetSpeedScale(float speedScale)
        {
            if (float.IsNaN(speedScale))
            {
                _speedScale = 1f;
                return;
            }

            _speedScale = speedScale < 0f ? 0f : (speedScale > 1f ? 1f : speedScale);
        }

        /// <summary>
        /// 移动层的"走"：<b>有惯性按加速度逼近，零惯性当帧直达</b>。
        /// </summary>
        /// <param name="direction">目标方向（可未归一化；零向量表示没有期望方向）。</param>
        /// <param name="speed">该方向上的目标速度（<b>会乘上本帧的速度乘数</b>）。</param>
        /// <remarks>
        /// 判据是 <see cref="CharacterConfig.moveAcceleration"/>（<c>&le; 0</c> = 零惯性配置）：
        /// 于是"要不要惯性"是一个配置问题而不是一次代码改动 —— 俯视角玩家把加速度填 0
        /// 就退回"当帧到位、松手当帧停"的旧手感。
        /// <para>加速度与转向衰减都取自角色配置，本方法不引入任何新字段；
        /// 控制律本身是 <see cref="SteerTowards"/>，与敌人追击共用一份数学。</para>
        /// <para><b>速度乘数在这里落地</b>（<see cref="SetSpeedScale"/>）：乘的是目标速度，
        /// 于是"泥浆里走得慢"对零惯性角色当帧生效，对有惯性角色的<b>稳态</b>也精确等于
        /// <c>配置速度 × 乘数</c>。</para>
        /// </remarks>
        public void MoveTowards(Vector2 direction, float speed)
        {
            float scaled = speed * _speedScale;

            if (Config.moveAcceleration <= 0f)
            {
                MoveDirection(direction, scaled);
                return;
            }

            SteerTowards(direction, scaled, Config.moveAcceleration, Config.turnDecayRate);
        }

        /// <summary>
        /// 移动层的"停"：<b>有惯性滑停，零惯性当帧停</b>。
        /// </summary>
        /// <remarks>与 <see cref="MoveTowards"/> 同一套判据。"松手后多久归零"因此等于
        /// <c>速度 / 加速度</c>，不需要另立一个"停止时长"参数。</remarks>
        public void BrakeTowards()
        {
            if (Config.moveAcceleration <= 0f)
            {
                StopMove();
                return;
            }

            SteerTowards(Vector2.zero, 0f, Config.moveAcceleration, Config.turnDecayRate);
        }

        /// <summary>按输入方向加速到目标速度；移动锁定期内不响应，并按输入方向更新朝向。</summary>
        public void MoveHorizontal(float inputX, float targetSpeed, float accel)
        {
            if (IsMoveLocked) return;

            FaceTowards(new Vector2(inputX, 0f));
            ApproachX(inputX * targetSpeed, accel);
        }

        /// <summary>无水平输入时把水平速度渐减到 0；移动锁定期内不响应。</summary>
        public void BrakeHorizontal(float accel)
        {
            if (IsMoveLocked) return;

            ApproachX(0f, accel);
        }

        /// <summary>按输入的水平分量横向移动；调用方传水平轴值即可，本类不读输入快照。</summary>
        public void MoveHorizontal(float inputX)
        {
            MoveHorizontal(inputX, Config.moveSpeed, Config.moveAcceleration);
        }

        /// <summary>急停：水平速度立刻归零；移动锁定期内不响应。</summary>
        public void StopHorizontal()
        {
            if (IsMoveLocked) return;

            SetVelocityX(0f);
        }

        /// <summary>把速度硬钳到上限（只在越界时提交）；上限 ≤ 0 表示不限制。</summary>
        /// <remarks>
        /// 钳制是"接管"而非"追加"：它按 <see cref="Velocity"/> 结算后整体覆盖，因此会撤掉两个分量上待生效的加速度。
        /// 同帧还要用 <see cref="ApplyExtraForce"/> 时，<b>先累加外力、后钳制</b>，否则本帧外力被这一覆盖吃掉。
        /// </remarks>
        public void ClampSpeed(float maxSpeed)
        {
            if (maxSpeed <= 0f) return;

            SetVelocity(Vector2.ClampMagnitude(Velocity, maxSpeed));
        }

        /// <summary>
        /// 只对<b>本帧</b>生效的速度上限：帧末写出时统一钳一次，帧首自动失效。
        /// </summary>
        /// <param name="maxSpeed">上限（单位/秒）。非法值（<c>NaN</c> / 无穷 / <c>≤ 0</c>）被忽略，保持不限速。</param>
        /// <remarks>
        /// 与 <see cref="ClampSpeed"/> 的分工：那条是"现在就接管速度"，本方法<b>不立刻改速度</b>、
        /// 只改本帧写出时的那一次钳制 —— 于是移动状态照常按输入写速度，写出时才被压住。
        /// <para><b>为什么要挡非法值：</b><c>NaN</c> 参与比较恒为 <c>false</c>，
        /// <c>Vector2.ClampMagnitude</c> 遇到 <c>NaN</c> 上限会把速度变成非数 ——
        /// 角色随即从屏幕上消失，且不报错（球的那条上限 <c>ClampThrowPoint</c> 栽过同一个跟头）。
        /// 这里不抛异常：白模不该因为一个数字把 Play 打断。</para>
        /// <para>首个消费者是白模的受击推挤：玩家的移动状态在同一个物理帧里已经写过速度了，
        /// 要压住那一次写出只能靠"本帧上限"。</para>
        /// </remarks>
        public void SetSpeedLimit(float maxSpeed)
        {
            if (float.IsNaN(maxSpeed) || float.IsInfinity(maxSpeed) || maxSpeed <= 0f) return;

            _speedLimit = maxSpeed;
        }

        /// <summary>渐进逼近目标速度；控制律的唯一实现点（见 <c>Docs/框架设计/分层设计/逻辑层.md</c> §3）。</summary>
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

        /// <summary>
        /// 二维渐进逼近目标速度；<see cref="ApproachX"/> 的二维版，控制律相同（加速度受限 ＋ 反向走衰减率）。
        /// </summary>
        /// <param name="direction">期望方向。<b>可以是未归一化向量</b>（本方法内归一化），零向量表示"没有期望方向"。</param>
        /// <param name="targetSpeed">该方向上的目标速度（单位/秒）。</param>
        /// <param name="acceleration">加速度上限（单位/秒²）。</param>
        /// <param name="decayPerSecond">反向/归零时的指数衰减率（1/秒）。</param>
        /// <remarks>
        /// <b>为什么必须有这一条，而不是复用 <see cref="ApproachX"/>：</b>那条只看 <c>Velocity.x</c>，
        /// <b>竖直分量永远不衰减</b>。拿它做俯视角的二维击退，朝正上方被推飞的敌人会以恒定速度
        /// 一路飘到地图外 —— 而且不报错、只是"敌人不见了"。
        /// <para><b>零方向分支是指数衰减，且只有这一支。</b>方向为零时本方法只做
        /// <c>_delta -= 当前速度 × (1 − e^(−衰减率·Δt))</c>：符号保持、模长单调收缩，所以不会振荡、
        /// 不会反向、也不会越过零。曾经想过"先求期望速度、再按加速度逼近"，那条路在目标速度为零时
        /// 会退化成匀减速，且<span>与反向分支的判据重合</span>，两支会互相打架。</para>
        /// <para><b>方向变号时取两支中较快的一支</b>（与 <see cref="ApproachX"/> 一致）：纯指数衰减
        /// 永不反向，只看它会把角色停在原地不动。</para>
        /// <para><b>目标速度为零且当前速度也为零时不写速度</b>：零提交帧在 <see cref="FixedTick"/> 里
        /// 会被跳过，"没有变更就不覆盖引擎"这条不变量因此仍然成立。</para>
        /// <para>首个消费者是白模的敌人：追击（需要加速度）与击退滑行（需要衰减）都是它。
        /// 玩家的移动状态仍走 <see cref="MoveDirection"/>（零惯性），两条路互不影响。</para>
        /// </remarks>
        public void SteerTowards(Vector2 direction, float targetSpeed, float acceleration, float decayPerSecond)
        {
            Vector2 current = Velocity;
            Vector2 desired;

            if (direction.sqrMagnitude <= 0f || targetSpeed <= 0f)
            {
                // 没有期望方向：把速度指数收缩到零。dying 时不做这个，角色会永远滑。
                desired = Vector2.zero;
            }
            else
            {
                // 归一化在这里做，调用方可以给未归一化的方向（契约不靠调用方守）。
                desired = direction.normalized * targetSpeed;
            }

            Vector2 delta = desired - current;

            if (desired.sqrMagnitude > 0f && current.sqrMagnitude > 0f && Vector2.Dot(desired, current) < 0f)
            {
                // 反向：改用指数衰减，与线性逼近取较快的一支。
                Vector2 decay = -current * (1f - Mathf.Exp(-decayPerSecond * Ctx.deltaTime));

                if (decay.sqrMagnitude > delta.sqrMagnitude) delta = decay;
            }
            else
            {
                // 同向（或已停止）：按加速度限幅地逼近。
                float maxStep = acceleration * Ctx.deltaTime;

                if (delta.sqrMagnitude > maxStep * maxStep) delta = delta.normalized * maxStep;
            }

            _delta += delta;
        }

        /// <summary>接管两个分量：本帧工作速度即为给定值，覆盖已提交的全部变更；同时按水平分量更新朝向。</summary>
        public void SnapVelocity(Vector2 velocity)
        {
            SetVelocity(velocity);
            FaceTowards(new Vector2(velocity.x, 0f));
        }

        /// <summary>接管两个分量但不改朝向：本帧工作速度即为给定值。</summary>
        /// <remarks>与 <see cref="SnapVelocity"/> 的分工：钳制、外力结算这类"不是角色主动转向"的写法走本条。</remarks>
        public void SetVelocity(Vector2 velocity)
        {
            SetVelocityX(velocity.x);
            SetVelocityY(velocity.y);
        }

        /// <summary>把垂直分量瞬变到给定值：该分量已被接管，撤掉它上面待生效的加速度。</summary>
        public void SetVelocityY(float vy)
        {
            _accel.y = 0f;
            _delta.y = vy - _frameStart.y;
        }

        /// <summary>面向给定方向；零向量表示"不改朝向"（站住时精灵不会自己翻面）。</summary>
        public void FaceTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            Motor.Facing = direction;
        }

        /// <summary>在接下来的 <paramref name="duration"/> 秒内不响应水平输入。</summary>
        public void StartMoveLock(float now, float duration)
        {
            _moveLockUntil = now + duration;
        }

        /// <summary>
        /// 缩放外力累加强度。俯视角角色填 0；需要被击退、被水流推、被吸附的角色按需打开。
        /// </summary>
        public void SetExtraForceScale(float scale) => _extraForceScale = scale;

        /// <summary>
        /// 提交一帧外力：把 <paramref name="force"/>（单位/秒²）按 Δt 与强度缩放累进速度账本。
        /// </summary>
        /// <param name="force">本帧外力加速度；零向量表示无外力（当帧成立即返回，不产生提交）。</param>
        /// <remarks>
        /// <b>参数由调用方给出，本类不持有任何具体力的语义</b>——重力、浮力、水流、风、吸附、击退滑行
        /// 都只是 <paramref name="force"/> 的一种取值。因此这里没有开关、没有曲线、没有方向假设。
        /// <para><b>本类不自动调用它，且当前没有任何角色调用它</b>：施不施加外力是角色自己的决策。
        /// 俯视角玩家<b>刻意不施外力</b>（零惯性 + 无重力），所以 <c>PlayerLogic.OnTick</c> 里只有一行注释占位、
        /// 没有真实调用——这是设计意图，不是未完成。首个真实消费者预计是敌人、击退、水流或吸附。
        /// 目前唯一调用它的是测试探针（<c>移动Tests.LimitProbe</c>，覆盖 M16 与 M18）。</para>
        /// <para>外力与"速度上限"是两件事：要限速请显式调 <see cref="ClampSpeed"/>。
        /// <b>同帧两者并用时必须先累加外力、后钳制</b>——钳制按当帧 <see cref="Velocity"/> 整体覆盖，
        /// 顺序反了本帧外力会被整个吃掉且不报错（<c>移动Tests.M18</c> 专门钉这一条）。</para>
        /// </remarks>
        protected void ApplyExtraForce(in LogicContext ctx, Vector2 force)
        {
            if (force.sqrMagnitude <= 0f || _extraForceScale == 0f) return;

            AddForce(force * _extraForceScale);
        }

        /// <summary>把水平分量瞬变到给定值：该分量已被接管，撤掉它上面待生效的加速度。</summary>
        private void SetVelocityX(float vx)
        {
            _accel.x = 0f;
            _delta.x = Clamped(new Vector2(vx, 0f)).x - _frameStart.x;
        }

        /// <summary>本帧上限只在这里被用一次：<see cref="SetSpeedLimit"/> 不动速度，只压写出。</summary>
        private Vector2 Clamped(Vector2 velocity)
        {
            return float.IsInfinity(_speedLimit) ? velocity : Vector2.ClampMagnitude(velocity, _speedLimit);
        }

        private bool HasSubmission => _delta.x != 0f || _delta.y != 0f || _accel.x != 0f || _accel.y != 0f;

        private bool IsMoveLocked => Ctx.now < _moveLockUntil;
    }
}
