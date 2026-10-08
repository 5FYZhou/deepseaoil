using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>玩家取值边界，合并 player 表行与 PlayerConfig SO</summary>
    /// <remarks>表行管血量与受击，SO管移动与冲刺，装配期折算一次</remarks>
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

        /// <summary>敌人贴上圆心距，世界单位，略小于两半径之和</summary>
        public float ContactRadius => _row.ContactRadius;

        /// <remarks>移动与冲刺参数，装配期折算快照，改SO不生效</remarks>
        public PlayerConfig Config => _config;

        public ProjectileSpec Ball { get; }

        public bool SnapToEightDirections => _config.snapToEightDirections;

        /// <summary>输入缓冲容量，秒，须≥各输入窗口</summary>
        public float InputBufferSeconds => _config.inputBufferTime;

        public float DashCooldownSeconds => _config.dashCooldown;

        public float DashBufferSeconds => _config.dashBufferTime;

        /// <summary>相机深度，世界单位，取水球行ThrowTuning，缺失=100</summary>
        public float CameraPlaneDepth
            => Ball != null && Ball.Tuning != null ? Ball.Tuning.cameraPlaneDepth : 100f;

        /// <summary>投掷射程上限，世界单位，取水球行，缺失=0=不限</summary>
        public float MaxThrowDistance => Ball != null ? Ball.MaxThrowDistance : 0f;
    }
}
