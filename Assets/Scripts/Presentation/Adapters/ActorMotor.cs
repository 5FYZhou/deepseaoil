using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>移动执行器基类：<b>把逻辑层算出的速度写进物理体，并把朝向落成视觉镜像</b>；同时承载速度账本与角色控制律。</summary>
    /// <remarks>
    /// <b>重力由逻辑层施加</b>（俯视角把重力缩放设为 0），本类永不设阻尼。<see cref="Velocity"/> 分两层：<see cref="EngineVelocity"/> 是<b>引擎真值回读口</b>，<c>IActorLedger.Velocity</c> 是<b>本帧工作速度</b>；两者不一致（撞墙、被顶住）正是"引擎是否否决了提交"的可见形态。
    /// <b>初始化与生命周期无关</b>：物理体引用惰性自取、每次访问补跑一次 <see cref="Initialize"/> —— <c>Awake</c> 并非总会跑（EditMode 下 <c>AddComponent</c> 就不触发），做成幂等自愈就不存在"忘了接线"导致的 <c>NullReferenceException</c>。
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class ActorMotor : MonoBehaviour, IActorMotor
    {
        [SerializeField] private Rigidbody2D body = default;

        private ActorLedger _ledger;
        private Vector2 _facing = Vector2.right;
        private bool _initialized;

        protected Rigidbody2D Body
        {
            get
            {
                Initialize();
                return body;
            }
        }

        public bool IsInitialized => _initialized;

        private ActorLedger Ledger => _ledger ??= new ActorLedger(
            readVelocity: () => Body.velocity,
            writeVelocity: v => Body.velocity = v);

        /// <summary>立即固化物理参数（幂等）。</summary>
        /// <remarks>惰性初始化要等到第一次读位置才跑，而"建完刚体到第一次读位置之间"那一个物理步会用<b>默认重力</b>跑（偏差约 0.002 单位）：建完刚体就显式调一次，让"物理参数什么时候生效"变成一行代码。</remarks>
        public void EnsureInitialized()
        {
            Initialize();
        }

        /// <summary>引擎当前速度（单位/秒）。<b>这是回读口</b>，不是本帧工作速度。</summary>
        public Vector2 EngineVelocity => Body.velocity;

        /// <summary>物理体位置；边界钳位用 <c>Rigidbody2D.position</c> 而非 <c>transform.position</c>。</summary>
        public Vector2 Position => Body.position;

        /// <summary>以给定速度驱动一次移动（不经账本）。<b>只有账本的 Commit 与重生该调它。</b></summary>
        /// <remarks>绕过账本直接调它会让本帧帧末的 <c>Commit</c> 覆盖掉这次写入，表现为"被推了一下又弹回去"。</remarks>
        public void Move(Vector2 velocity)
        {
            Body.velocity = velocity;
        }

        public void SetPosition(Vector2 position)
        {
            Body.position = position;
        }

        /// <summary>朝向：内部保存完整世界方向，只把<b>水平分量</b>落成 <c>localScale.x</c> 的符号。</summary>
        /// <remarks>竖直朝向不参与镜像（否则"朝左走时按一下上"会把精灵翻回朝右）；给 <see cref="Vector2.zero"/> 时朝向与镜像都不动。</remarks>
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

        Vector2 IMovementMotor.Velocity => EngineVelocity;

        /// <summary>角色共用运动参数（<b>只写不读</b>；状态机读的是 <see cref="Motion"/>）。</summary>
        public CharacterConfig Config => Ledger.Config;

        public MotionParams Motion => Ledger.Motion;

        public Vector2 FrameStartVelocity => Ledger.FrameStartVelocity;

        public Vector2 SubmittedDelta => Ledger.SubmittedDelta;

        Vector2 IActorLedger.Velocity => Ledger.Velocity;

        /// <inheritdoc />
        public float SpeedScale
        {
            get => Ledger.SpeedScale;
            set => Ledger.SpeedScale = value;
        }

        public void Configure(CharacterConfig config)
        {
            Ledger.Configure(config);
        }

        public void BeginStep(float now, float deltaTime)
        {
            Ledger.BeginStep(now, deltaTime);
        }

        public void Commit()
        {
            Ledger.Commit();
        }

        public void AddImpulse(Vector2 deltaVelocity) => Ledger.AddImpulse(deltaVelocity);

        public void AddForce(Vector2 acceleration) => Ledger.AddForce(acceleration);

        public void SetVelocity(Vector2 velocity) => Ledger.SetVelocity(velocity);

        public void ClampSpeed(float maxSpeed) => Ledger.ClampSpeed(maxSpeed);

        public void SetExtraForceScale(float scale) => Ledger.SetExtraForceScale(scale);

        public void SnapVelocity(Vector2 velocity)
        {
            Ledger.SnapVelocity(velocity, v => FaceTowards(new Vector2(v.x, 0f)));
        }

        /// <inheritdoc />
        public void MoveTowards(Vector2 direction, float speed)
        {
            FaceTowards(direction);
            Ledger.MoveTowards(direction, speed);
        }

        /// <inheritdoc />
        public void BrakeTowards()
        {
            Ledger.BrakeTowards();
        }

        public void StopMove()
        {
            Ledger.StopMove();
        }

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

        /// <summary>提交一帧外力（转发给账本）。本类不持有任何具体力的语义；它与 <see cref="SetExtraForceScale"/> <b>没有生产消费者</b>，但<b>有测试语义</b>。</summary>
        public void ApplyExtraForce(Vector2 force) => Ledger.ApplyExtraForce(force);

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>补齐物理体引用并固化俯视角物理参数；可重复调用，只生效一次。</summary>
        /// <remarks>子类要加自己的物理参数时覆写本方法（先调 <c>base.Initialize()</c>，或在 <see cref="ApplyPhysics"/> 里扩展）。</remarks>
        protected virtual void Initialize()
        {
            if (_initialized) return;

            if (body == null) body = GetComponent<Rigidbody2D>();

            if (body == null) return;   // 由调用点决定怎么处理；这里不自作主张停用

            ApplyPhysics(body);

            _initialized = true;
        }

        /// <summary>俯视角的共同物理参数：引擎重力整体关掉、永不随旋转。</summary>
        /// <remarks>速度全部由逻辑层账本给出，所以引擎侧不需要重力；旋转会让角色"带着旋转去撞墙"，看起来像陀螺。子类覆写后应调 <c>base.ApplyPhysics</c>，再补自己那几条（例如敌人的连续碰撞检测）。</remarks>
        protected virtual void ApplyPhysics(Rigidbody2D rigidbody2D)
        {
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.freezeRotation = true;
        }
    }
}
