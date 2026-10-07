using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 移动执行器基类：<b>把逻辑层算出的速度写进物理体，并把朝向落成视觉镜像</b>；
    /// 同时承载<b>速度账本与角色控制律</b>（<see cref="IActorMotor"/>）。
    /// </summary>
    /// <remarks>
    /// <para><b>账本为什么在这里：</b>审查已定"把 <c>Motor</c> 开放出来、让 Logic 退化为组合件"。
    /// 账本（一帧的速度累加区 ＋ 帧末一次写出）与控制律（渐进逼近 / 零惯性直达 / 反向衰减）
    /// 只服务"这一帧怎么驱动物理体"这一个问题，留在 <c>ActorLogic</c> 里等于让每个角色各继承一份同样的字段。
    /// 搬出来之后，状态层与移动层注入的是执行器，而控制律可以用一个不碰引擎的探针在 EditMode 里直接测。</para>
    /// <para><b>玩家与敌人共用这一份实现</b>（<see cref="PlayerMotor"/> / <see cref="EnemyMotor"/>），
    /// 差异只有"各自额外的物理参数"。最早两份实现（玩家一个 <c>MovementMotor</c>、敌人一个
    /// <c>EnemyMotor</c>）的理由是"抽基类要先动一个正在工作的文件"；改成统一结构之后，
    /// 重复的代价出现了：两份 <c>Facing</c> 的镜像契约会各自漂。</para>
    /// <para><b>重力由逻辑层施加</b>（俯视角把重力缩放设为 0），本类永不设阻尼。</para>
    /// <para><see cref="Velocity"/> 分两层：<see cref="EngineVelocity"/> 是<b>引擎真值回读口</b>，
    /// 而 <c>IActorLedger.Velocity</c> 是<b>本帧工作速度</b>（帧首真值 ＋ 本帧提交）。
    /// 两者不一致时（撞墙、被顶住）正是"引擎是否否决了提交"的可见形态 ——
    /// 调试面板把两者并排显示就是为了这个。</para>
    /// <para><b>本类不做任何环境检测</b>：俯视角的阻挡全部由刚体碰撞解算。需要"是否站地 / 是否贴墙"
    /// 的角色（悬崖巡逻、贴墙判定）届时在自己的子类上实现，不要往这里加射线。</para>
    /// <para><b>初始化与生命周期无关</b>：物理体引用惰性自取（见 <see cref="Body"/>），
    /// 且每次访问都补跑一次 <see cref="Initialize"/>。原因是 <c>Awake</c> 并非总会跑
    /// （EditMode 下 <c>AddComponent</c> 就不触发），做成幂等自愈就不存在
    /// "忘了接线 / 生命周期没跑"导致的 <c>NullReferenceException</c>。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class ActorMotor : MonoBehaviour, IActorMotor
    {
        [SerializeField] private Rigidbody2D body = default;

        private ActorLedger _ledger;
        private Vector2 _facing = Vector2.right;
        private bool _initialized;

        /// <summary>物理体引用：自取（不依赖 Inspector 接线，也不依赖 <c>Awake</c> 时机）。</summary>
        protected Rigidbody2D Body
        {
            get
            {
                Initialize();
                return body;
            }
        }

        /// <summary>初始化是否已跑过；测试据此断言初始化链路真的执行了。</summary>
        public bool IsInitialized => _initialized;

        /// <summary>速度账本（控制律与一帧的提交都住在它里面，见 <see cref="ActorLedger"/>）。</summary>
        private ActorLedger Ledger => _ledger ??= new ActorLedger(
            readVelocity: () => Body.velocity,
            writeVelocity: v => Body.velocity = v);

        /// <summary>
        /// 立即固化物理参数（幂等）。
        /// </summary>
        /// <remarks>
        /// 惰性初始化要等到第一次读 <see cref="Position"/> / <see cref="EngineVelocity"/> 才跑，
        /// 而"建完刚体到第一次读位置之间"隔着的那一个物理步会用<b>默认重力</b>跑 —— 例如敌人在
        /// <c>FixedUpdate</c> 里被造出来，它第一次读位置在下一个物理帧。偏差极小（约 0.002 单位），
        /// 但它是"物理参数什么时候生效"这个问题的隐式答案，所以建完刚体就显式调一次，
        /// 让答案变成一行代码。
        /// </remarks>
        public void EnsureInitialized()
        {
            Initialize();
        }

        // ─────────────────────────────────────────────
        // IMovementMotor：引擎侧的读写
        // ─────────────────────────────────────────────

        /// <summary>引擎当前速度（单位/秒）。<b>这是回读口</b>，不是本帧工作速度。</summary>
        public Vector2 EngineVelocity => Body.velocity;

        /// <summary>物理体位置；边界钳位用 <c>Rigidbody2D.position</c> 而非 <c>transform.position</c>。</summary>
        public Vector2 Position => Body.position;

        /// <summary>以给定速度驱动一次移动（不经账本）。<b>只有账本的 Commit 与重生该调它。</b></summary>
        /// <remarks>绕过账本直接调它会让本帧帧末的 <c>Commit</c> 覆盖掉这次写入，
        /// 表现为"被推了一下又弹回去"—— 要改速度请用账本入口。</remarks>
        public void Move(Vector2 velocity)
        {
            Body.velocity = velocity;
        }

        /// <summary>瞬移物理体（边界钳位 / 重生用）；会打断插值，只在必要时调用。</summary>
        public void SetPosition(Vector2 position)
        {
            Body.position = position;
        }

        /// <summary>
        /// 朝向：内部保存完整世界方向（俯视角 8 向），只把<b>水平分量</b>落成 <c>localScale.x</c> 的符号。
        /// </summary>
        /// <remarks>
        /// 竖直朝向不参与镜像——否则"朝左走时按一下上"会把精灵翻回朝右
        /// （<c>value.x == 0</c> 时 <c>Mathf.Abs(scale.x) * 1f</c> 恰好等于取绝对值）。
        /// 给 <see cref="Vector2.zero"/> 时朝向与镜像都不动，保证"零输入不改朝向"。
        /// </remarks>
        public Vector2 Facing
        {
            get => _facing;
            set
            {
                if (value.sqrMagnitude <= 0f) return;

                _facing = value;

                if (value.x == 0f) return;   // 纯竖直：不碰镜像

                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (value.x < 0f ? -1f : 1f);
                transform.localScale = scale;
            }
        }

        /// <summary>引擎速度（<c>IMovementMotor.Velocity</c>：回读口）。</summary>
        Vector2 IMovementMotor.Velocity => EngineVelocity;

        // ─────────────────────────────────────────────
        // IActorLedger：全部转发给账本
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        public CharacterConfig Config => Ledger.Config;

        /// <inheritdoc />
        public Vector2 FrameStartVelocity => Ledger.FrameStartVelocity;

        /// <inheritdoc />
        public Vector2 SubmittedDelta => Ledger.SubmittedDelta;

        /// <inheritdoc />
        Vector2 IActorLedger.Velocity => Ledger.Velocity;

        /// <inheritdoc />
        public float SpeedScale
        {
            get => Ledger.SpeedScale;
            set => Ledger.SpeedScale = value;
        }

        /// <inheritdoc />
        public void Configure(CharacterConfig config)
        {
            Ledger.Configure(config);
        }

        /// <inheritdoc />
        public void BeginStep(float now, float deltaTime)
        {
            Ledger.BeginStep(now, deltaTime);
        }

        /// <inheritdoc />
        public void Commit()
        {
            Ledger.Commit();
        }

        /// <inheritdoc />
        public void AddImpulse(Vector2 deltaVelocity) => Ledger.AddImpulse(deltaVelocity);

        /// <inheritdoc />
        public void AddForce(Vector2 acceleration) => Ledger.AddForce(acceleration);

        /// <inheritdoc />
        public void SetVelocity(Vector2 velocity) => Ledger.SetVelocity(velocity);

        /// <inheritdoc />
        public void ClampSpeed(float maxSpeed) => Ledger.ClampSpeed(maxSpeed);

        /// <inheritdoc />
        public void SetSpeedLimit(float maxSpeed) => Ledger.SetSpeedLimit(maxSpeed);

        /// <inheritdoc />
        public void SetExtraForceScale(float scale) => Ledger.SetExtraForceScale(scale);

        /// <inheritdoc />
        public void StartMoveLock(float now, float duration) => Ledger.StartMoveLock(now, duration);

        // ─────────────────────────────────────────────
        // 控制律（IStateHost 的入口）
        // ─────────────────────────────────────────────

        /// <summary>接管两个分量，并按水平分量更新朝向。</summary>
        public void SnapVelocity(Vector2 velocity)
        {
            Ledger.SnapVelocity(velocity, v => FaceTowards(new Vector2(v.x, 0f)));
        }

        /// <inheritdoc />
        public void MoveTowards(Vector2 direction, float speed)
        {
            FaceTowards(direction);

            if (Ledger.IsMoveLocked) return;

            Ledger.MoveTowards(direction, speed);
        }

        /// <inheritdoc />
        public void BrakeTowards()
        {
            Ledger.BrakeTowards();
        }

        /// <summary>急停：速度当帧归零（朝向不变）；移动锁定期内不响应。</summary>
        /// <remarks><b>它的语义是"硬停"</b>（重生、禁用、速度被外力完全接管时用）。
        /// 手感上的"松手滑停"走 <see cref="BrakeTowards"/> —— 两者刻意分开。</remarks>
        public void StopMove()
        {
            Ledger.StopMove();
        }

        /// <summary>俯视角移动：把速度整体接管为 <paramref name="direction"/> × <paramref name="speed"/>。</summary>
        public void MoveDirection(Vector2 direction, float speed)
        {
            FaceTowards(direction);

            Ledger.MoveDirection(direction, speed);
        }

        /// <summary>面向给定方向；零向量表示"不改朝向"（站住时精灵不会自己翻面）。</summary>
        public void FaceTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            Facing = direction;
        }

        /// <summary>
        /// 提交一帧外力（转发给账本）。
        /// </summary>
        /// <remarks>参数由调用方给出，本类不持有任何具体力的语义。
        /// 它与 <see cref="SetSpeedLimit"/> 目前没有生产消费者（见 <see cref="ActorLedger.ApplyExtraForce"/>）。</remarks>
        public void ApplyExtraForce(Vector2 force) => Ledger.ApplyExtraForce(force);

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 补齐物理体引用并固化俯视角物理参数；可重复调用，只生效一次。
        /// </summary>
        /// <remarks>子类要加自己的物理参数时覆写本方法（先调 <c>base.Initialize()</c>，
        /// 或在 <see cref="ApplyPhysics"/> 里扩展）。</remarks>
        protected virtual void Initialize()
        {
            if (_initialized) return;

            if (body == null) body = GetComponent<Rigidbody2D>();

            if (body == null) return;   // 由调用点决定怎么处理；这里不自作主张停用

            ApplyPhysics(body);

            _initialized = true;
        }

        /// <summary>
        /// 俯视角的共同物理参数：引擎重力整体关掉、永不随旋转。
        /// </summary>
        /// <remarks>速度全部由逻辑层账本给出，所以引擎侧不需要重力；旋转会让角色"带着旋转去撞墙"，
        /// 看起来像陀螺。<para>子类覆写后应调 <c>base.ApplyPhysics</c>，再补自己那几条
        /// （例如敌人的连续碰撞检测）。</para></remarks>
        protected virtual void ApplyPhysics(Rigidbody2D rigidbody2D)
        {
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.freezeRotation = true;
        }
    }
}
