using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>玩家的取值边界：合并「<c>player</c> 表行」与「<c>PlayerConfig</c> SO」，外加投掷调参。</summary>
    /// <remarks>表行管血量与受击（策划调参），SO 管移动与冲刺（程序调参），两者语义不重叠；两份数据都在装配期折算一次。</remarks>
    public sealed class PlayerSpec
    {
        private readonly Player _row;
        private readonly PlayerConfig _config;

        public PlayerSpec(Player row, PlayerConfig config, ProjectileSpec ball)
        {
            _row = row;
            _config = config;
            Ball = ball;
        }

        public int Id => _row.Id;

        public string Name => _row.Name;

        public float MaxHp => _row.MaxHp;

        public float ContactDamage => _row.ContactDamage;

        public float InvulnerableDuration => _row.InvulnerableDuration;

        public float RetryDelay => _row.RetryDelay;

        public float AttackInterval => _row.AttackInterval;

        public float KnockbackImpulse => _row.KnockbackImpulse;

        public float KnockbackSpeedLimit => _row.KnockbackSpeedLimit;

        /// <summary>敌人"贴上了"的圆心距（世界单位）；比"两者碰撞半径之和"略小，所以是"几乎贴在身上"而不是"擦到就算"。</summary>
        public float ContactRadius => _row.ContactRadius;

        /// <summary>移动与冲刺参数（SO）；只有一个读者：装配链把玩家执行器配起来的那一行（<c>PlayerLogic</c> → <c>ActorLogic(motor, spec.Config)</c>）—— 消费方要哪条语义就问本类要哪条，不要再往下取配置对象，且装配期折算成快照后改 SO 不生效。</summary>
        public PlayerConfig Config => _config;

        public ProjectileSpec Ball { get; }

        public bool SnapToEightDirections => _config.snapToEightDirections;

        /// <summary>输入缓冲容量（秒）：历史窗口时长，必须 ≥ 下面所有输入各自的窗口。</summary>
        public float InputBufferSeconds => _config.inputBufferTime;

        public float DashCooldownSeconds => _config.dashCooldown;

        public float DashBufferSeconds => _config.dashBufferTime;

        /// <summary>屏幕点投到世界平面时给的相机深度（世界单位，取水球行的 <c>ThrowTuning</c>）；取不到时 100。</summary>
        public float CameraPlaneDepth
            => Ball != null && Ball.Tuning != null ? Ball.Tuning.cameraPlaneDepth : 100f;

        /// <summary>投掷射程上限（世界单位），取水球那一行；射程属"玩家资格"而不是"球的规则"。表里一行都没有时返回 0，而 <c>TileAim</c> 对非法射程按"不限"处理 —— 表现是"射程变得很远"，不是"投不出去"。</summary>
        public float MaxThrowDistance => Ball != null ? Ball.MaxThrowDistance : 0f;
    }
}
