using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 敌人移动执行器（<see cref="IMovementMotor"/> 的白模实现）：把逻辑层算出的速度写进物理体。
    /// </summary>
    /// <remarks>
    /// <b>为什么不复用表现层那个 <c>MovementMotor</c>：</b>
    /// <list type="number">
    /// <item>它是 <c>sealed</c> 且带自己的 <c>Awake</c>，复用会把"玩家"这个语义绑到敌人身上；</item>
    /// <item>它的 <c>body</c> 是<b>缓存字段</b>，一旦两个消费者共享同一个实例，
    /// "谁先把引用取走"就会变成一个看不见的耦合；</item>
    /// <item>它只是一个 40 行的门面 —— 照它的写法重写一份，比给它的每个假设加注释便宜。</item>
    /// </list>
    /// <para><b>初始化是惰性自愈的</b>（与 <c>MovementMotor</c> 同一个理由，那个理由在那里已经吃过一次亏）：
    /// <c>Awake</c> 并非总会跑 —— EditMode 下 <c>AddComponent</c> 就不触发，
    /// 而本类只有"取到刚体"这一个依赖。做成幂等自愈，就不存在"生命周期没跑导致的
    /// <c>NullReferenceException</c>"。</para>
    /// <para><b>重力与旋转在这里强制关掉。</b><see cref="EnemyActor"/> 建出刚体时也设了一遍，
    /// 两处都设是刻意的：只靠组合根设的话，将来有人单独给某个物体挂上本组件（或从 prefab 继承）
    /// 就会得到一个会自由落体的角色。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyMotor : MonoBehaviour, IMovementMotor
    {
        [SerializeField] private Rigidbody2D body = default;

        private Vector2 _facing = Vector2.right;
        private bool _initialized;

        /// <summary>物理体引用：自取，不依赖 inspector 接线。</summary>
        private Rigidbody2D Body
        {
            get
            {
                Initialize();
                return body;
            }
        }

        /// <summary>引擎当前速度（单位/秒）。</summary>
        public Vector2 Velocity => Body.velocity;

        /// <summary>物理体位置。用 <c>Rigidbody2D.position</c> 而不是 <c>transform.position</c>。</summary>
        public Vector2 Position => Body.position;

        /// <summary>
        /// 朝向：保存完整世界方向，只把<b>水平分量</b>落成 <c>localScale.x</c> 的符号。
        /// </summary>
        /// <remarks>
        /// 与 <c>MovementMotor</c> 同款：竖直朝向不参与镜像，否则"朝左走时按一下上"
        /// 会把精灵翻回朝右。白模的敌人是个圆，镜像看不出区别 —— 但契约要一致，
        /// 否则将来给敌人换上会朝向的精灵时，缺陷会以"某个方向走的时候翻面"的形式冒出来。
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

        /// <summary>瞬移物理体（白模不用，接口要求实现）；会打断插值。</summary>
        public void SetPosition(Vector2 position)
        {
            Body.position = position;
        }

        private void Awake()
        {
            Initialize();
        }

        /// <summary>补齐物理体引用并固化俯视角物理参数；可重复调用，只生效一次。</summary>
        private void Initialize()
        {
            if (_initialized) return;

            if (body == null) body = GetComponent<Rigidbody2D>();

            if (body == null) return;   // 由调用点决定怎么处理；这里不自作主张停用

            // 俯视角：引擎重力整体关掉（速度全部由逻辑层账本给出），且永不随旋转。
            body.gravityScale = 0f;
            body.freezeRotation = true;

            _initialized = true;
        }
    }
}
