using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 移动执行器基类：<b>把逻辑层算出的速度写进物理体，并把朝向落成视觉镜像</b>。
    /// </summary>
    /// <remarks>
    /// 玩家与敌人共用这一份实现（<see cref="PlayerMotor"/> / <see cref="EnemyMotor"/>），
    /// 差异只有"各自额外的物理参数"。
    /// <para><b>为什么当初是两份、后来合成一份：</b>最早玩家一个 <c>MovementMotor</c>、敌人一个
    /// <c>EnemyMotor</c>，理由是"抽基类要先动一个正在工作的文件"。改成统一结构（敌人与玩家同构）之后，
    /// 那个理由消失了，而重复的代价出现了：两份 <c>Facing</c> 的镜像契约会各自漂
    /// —— 注释里早就写着"契约要一致，否则将来换精灵时会冒缺陷"，而注释拦不住漂移。
    /// 现在镜像、惰性初始化、速度读写各只有一份。</para>
    /// <para><b>重力由逻辑层施加</b>（俯视角把重力缩放设为 0），本类永不设阻尼。</para>
    /// <para><see cref="Velocity"/> 是引擎真值回读口：逻辑层帧首读它作本帧基准
    /// （读到的是上一物理步结束时的值）。<see cref="Position"/> / <see cref="SetPosition"/>
    /// 走物理体位置而非 <c>transform</c>（后者会被刚体的位置积分覆盖）。</para>
    /// <para><b>本类不做任何环境检测</b>：俯视角的阻挡全部由刚体碰撞解算。需要"是否站地 / 是否贴墙"
    /// 的角色（悬崖巡逻、贴墙判定）届时在自己的子类上实现，不要往这里加射线。</para>
    /// <para><b>初始化与生命周期无关</b>：物理体引用惰性自取（见 <see cref="Body"/>），
    /// 且每次访问都补跑一次 <see cref="Initialize"/>。原因是 <c>Awake</c> 并非总会跑
    /// （EditMode 下 <c>AddComponent</c> 就不触发），做成幂等自愈就不存在
    /// "忘了接线 / 生命周期没跑"导致的 <c>NullReferenceException</c>。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class ActorMotor : MonoBehaviour, IMovementMotor
    {
        [SerializeField] private Rigidbody2D body = default;

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

        /// <summary>
        /// 立即固化物理参数（幂等）。
        /// </summary>
        /// <remarks>
        /// 惰性初始化要等到第一次读 <see cref="Position"/> / <see cref="Velocity"/> 才跑，
        /// 而"建完刚体到第一次读位置之间"隔着的那一个物理步会用<b>默认重力</b>跑 —— 例如敌人在
        /// <c>FixedUpdate</c> 里被造出来，它第一次读位置在下一个物理帧。偏差极小（约 0.002 单位），
        /// 但它是"物理参数什么时候生效"这个问题的隐式答案，所以建完刚体就显式调一次，
        /// 让答案变成一行代码。
        /// </remarks>
        public void EnsureInitialized()
        {
            Initialize();
        }

        /// <summary>引擎当前速度（单位/秒）。</summary>
        public Vector2 Velocity => Body.velocity;

        /// <summary>物理体位置；边界钳位用 <c>Rigidbody2D.position</c> 而非 <c>transform.position</c>。</summary>
        public Vector2 Position => Body.position;

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

        /// <summary>以给定速度驱动一次移动；<see cref="Vector2.zero"/> 即当帧停住。</summary>
        public void Move(Vector2 velocity)
        {
            Body.velocity = velocity;
        }

        /// <summary>瞬移物理体（边界钳位 / 重生用）；会打断插值，只在必要时调用。</summary>
        public void SetPosition(Vector2 position)
        {
            Body.position = position;
        }

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
