using DeepSeaOil.Logic.Movement;
using UnityEngine;

namespace DeepSeaOil.Presentation
{
    /// <summary>
    /// 移动执行器：把逻辑层算出的速度写进物理体，并用射线提供地面与贴墙检测。
    /// </summary>
    /// <remarks>
    /// 重力由逻辑层施加，本类永不设 <c>gravityScale</c> 与阻尼（见 <c>Docs/架构约束.md</c> §五）。
    /// <see cref="Velocity"/> 是引擎真值回读口：逻辑层帧首读它作本帧基准（读到的是上一物理步结束时的值）。
    /// 检测不做缓存：逻辑层只读喂进去的 <c>WorldInfo</c>，本类的射线每物理帧只被组合根读一次。
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovementMotor : MonoBehaviour, IMovementMotor
    {
        [SerializeField] private Rigidbody2D body = default;
        [SerializeField] private Transform groundCheck = default;
        [SerializeField] private Transform wallCheck = default;
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private float wallCheckDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = default;

        /// <summary>引擎当前速度（单位/秒）。</summary>
        public Vector2 Velocity => body.velocity;

        /// <summary>朝向：-1 左，1 右；直接读写 <c>localScale.x</c> 的符号，不另存状态。</summary>
        public int Facing
        {
            get => transform.localScale.x < 0f ? -1 : 1;
            set
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (value < 0 ? -1f : 1f);
                transform.localScale = scale;
            }
        }

        /// <summary>脚底向下的射线是否命中地面层。</summary>
        public bool IsGrounded => groundCheck != null
            && Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundMask).collider != null;

        public bool IsTouchingWall => WallSide != 0;

        /// <summary>墙面所在方向：-1 在左，1 在右，0 表示未贴墙。</summary>
        public int WallSide
        {
            get
            {
                if (wallCheck == null) return 0;

                RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, Vector2.right * Facing, wallCheckDistance, groundMask);
                return hit.collider != null ? Facing : 0;
            }
        }

        /// <summary>以给定速度驱动一次移动。</summary>
        public void Move(Vector2 velocity)
        {
            body.velocity = velocity;
        }

        private void Awake()
        {
            if (body == null) TryGetComponent(out body);

            if (groundCheck == null || wallCheck == null)
            {
                Debug.LogError("MovementMotor 缺少检查点引用（groundCheck / wallCheck），检测将恒为假。", this);
            }

            // 检查点位于自身碰撞体内部，遮罩若含自身所在层会命中自己。
            if (groundMask == 0
                || (groundMask.value & (1 << gameObject.layer)) != 0)
            {
                Debug.LogError("MovementMotor 的 groundMask 未设置或包含了自身所在层：射线会命中自己，检测将恒为真。", this);
            }
        }
    }
}
