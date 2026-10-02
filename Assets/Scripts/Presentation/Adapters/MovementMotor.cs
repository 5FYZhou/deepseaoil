using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 移动执行器：把逻辑算出的速度写进物理体，并把朝向落成视觉镜像。
    /// </summary>
    /// <remarks>
    /// 重力由逻辑层施加（俯视角玩家把重力缩放设为 0），本类永不设阻尼。
    /// <see cref="Velocity"/> 是引擎真值回读口：逻辑层帧首读它作本帧基准（读到的是上一物理步结束时的值）。
    /// <see cref="Position"/> / <see cref="SetPosition"/> 供边界钳位使用：走物理体位置而非 <c>transform</c>。
    /// <b>本类不做任何环境检测</b>：俯视角的阻挡全部由刚体碰撞解算。需要"是否站地/是否贴墙"的角色
    /// （悬崖巡逻、贴墙判定）届时在它自己的执行器上实现，不要往这里加射线。
    /// <para><b>初始化与生命周期无关</b>：物理体引用惰性自取（见 <see cref="Body"/>），
    /// 且每次访问都补跑一次 <see cref="Initialize"/>。原因是 <c>Awake</c> 并非总会跑
    /// （EditMode 下 <c>AddComponent</c> 就不触发），而本类只有这一个依赖——
    /// 做成幂等自愈，就不存在"忘了接线/生命周期没跑"导致的 <c>NullReferenceException</c>。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovementMotor : MonoBehaviour, IMovementMotor
    {
        [SerializeField] private Rigidbody2D body = default;

        private Vector2 _facing = Vector2.right;
        private bool _initialized;

        /// <summary>物理体引用：自取（不依赖 Inspector 接线，也不依赖 <c>Awake</c> 时机）。</summary>
        private Rigidbody2D Body
        {
            get
            {
                Initialize();
                return body;
            }
        }

        /// <summary>初始化是否已跑过；测试据此断言初始化链路真的执行了。</summary>
        public bool IsInitialized => _initialized;

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

        /// <summary>瞬移物理体（边界钳位用）；会打断插值，只在越界时调用。</summary>
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
