using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 敌人移动执行器（<see cref="IMovementMotor"/> 的实现）：把逻辑层算出的速度写进物理体。
    /// </summary>
    /// <remarks>
    /// <b>为什么不复用玩家那个 <c>MovementMotor</c>：</b>
    /// <list type="number">
    /// <item>它绑着玩家语义（场景里那份引用、Inspector 里拖的那个组件），
    /// 复用会把"玩家"这个词绑到敌人身上；</item>
    /// <item>它的 <c>body</c> 是<b>缓存字段</b>，两个消费者共享同一个实例时"谁先取走引用"会变成看不见的耦合；</item>
    /// <item>它只有 40 行 —— 照它的写法重写一份，比给它的每个假设加注释便宜。</item>
    /// </list>
    /// <para><b>抽公共基类的触发条件（现在不抽）：</b>当第三个需要同样实现的执行器出现、
    /// 或者敌人侧开始需要"贴墙 / 站地"这类探测时再抽 <c>RigidbodyMotorBase</c>。
    /// 两个实现、且其中一个被场景资产引用着，抽基类要先动一个正在工作的文件。</para>
    /// <para><b>初始化是惰性自愈的</b>（与 <c>MovementMotor</c> 同一条纪律）：
    /// <c>Awake</c> 并非总会跑 —— EditMode 下 <c>AddComponent</c> 就不触发 ——
    /// 而本类只有"取到刚体"这一个依赖。做成幂等自愈，就不存在"生命周期没跑导致的空引用"。</para>
    /// <para><b>重力与旋转在这里强制关掉。</b>组合根建刚体时也设了一遍，两处都设是刻意的：
    /// 只靠组合根设的话，将来有人单独给某个物体挂上本组件就会得到一个会自由落体的角色。</para>
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
        /// 竖直朝向不参与镜像，否则"朝左走时按一下上"会把精灵翻回朝右。
        /// 当前的敌人是个圆，镜像看不出区别 —— 但契约要一致，
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

        /// <summary>瞬移物理体；会打断插值。</summary>
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
