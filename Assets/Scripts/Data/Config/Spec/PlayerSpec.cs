using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 玩家的取值边界：合并「<c>player</c> 表行」与「<c>PlayerConfig</c> SO」，外加投掷调参。
    /// </summary>
    /// <remarks>
    /// <b>它取代了旧的三件</b>（<c>PlayerSpec</c> 纯结构体 ＋ <c>PlayerHealth</c> 的配置读取 ＋
    /// <c>PlayerController</c> 的 <c>[SerializeField] PlayerConfig</c>）。现在"玩家的配置数据从哪来"
    /// 只有一个答案：<see cref="ConfigModule.GetPlayer"/>。
    /// <para><b>与 <c>PlayerConfig</c> 的分工：</b>那份管<b>移动</b>（速度、加速度、冲刺、缓冲窗口，
    /// 程序调参），本类从表行读<b>血量与受击</b>（策划调参），两者不重叠 ——
    /// 重叠就是"同一语义两处都有"，那会长出"谁优先"的裁决逻辑。</para>
    /// <para><b><see cref="CameraPlaneDepth"/> 为什么在这里：</b>它属 <c>ThrowTuning</c>（观感调参），
    /// 而消费者是 <c>PlayerController</c> 的瞄准换算。让玩家侧去看 <c>ThrowTuning</c> 就等于
    /// "消费者认识第二个数据来源"—— 本类的存在理由正是消掉它。</para>
    /// </remarks>
    public sealed class PlayerSpec
    {
        private readonly Player _row;
        private readonly PlayerConfig _config;

        /// <param name="row">表行（<c>player</c>）。</param>
        /// <param name="config">移动与冲刺参数（SO）。</param>
        /// <param name="ball">水球那一行（射程与瞄准平面深度的来源）。</param>
        public PlayerSpec(Player row, PlayerConfig config, ProjectileSpec ball)
        {
            _row = row;
            _config = config;
            Ball = ball;
        }

        /// <summary>编号（= 表主键）。</summary>
        public int Id => _row.Id;

        /// <summary>显示名。</summary>
        public string Name => _row.Name;

        /// <summary>血量上限。</summary>
        public float MaxHp => _row.MaxHp;

        /// <summary>敌人贴身一次造成的伤害。</summary>
        public float ContactDamage => _row.ContactDamage;

        /// <summary>受击后的无敌时长（秒）。</summary>
        public float InvulnerableDuration => _row.InvulnerableDuration;

        /// <summary>打空后到重来之间的停顿（秒）。</summary>
        public float RetryDelay => _row.RetryDelay;

        /// <summary>两次攻击之间的最短间隔（秒）。</summary>
        public float AttackInterval => _row.AttackInterval;

        /// <summary>受击被推开的冲量（速度，单位/秒）。</summary>
        public float KnockbackImpulse => _row.KnockbackImpulse;

        /// <summary>受击期间的速度上限（单位/秒）；与冲量相等时"补满为止"。</summary>
        public float KnockbackSpeedLimit => _row.KnockbackSpeedLimit;

        /// <summary>敌人"贴上了"的圆心距（世界单位）。</summary>
        /// <remarks>
        /// <b>判定用圆心距，不用接触点与法线：</b>玩家与敌人都只有一个碰撞体，圆心距的结论与逐点接触一致，
        /// 而圆心距能用 EditMode 测试直接喂坐标 —— 接触点与法线不能。
        /// <para>它比"两者碰撞半径之和"略小，所以是"几乎贴在身上"而不是"擦到就算"。</para>
        /// </remarks>
        public float ContactRadius => _row.ContactRadius;

        /// <summary>
        /// 移动与冲刺参数（SO）。<b>只有一个读者</b>：装配链把玩家执行器配起来的那一行
        /// （<c>PlayerLogic</c> → <c>ActorLogic(motor, spec.Config)</c>）。
        /// </summary>
        /// <remarks>
        /// <b>消费者要哪条数就问本类要哪条语义</b>（见下面几个 <c>*Seconds</c> /
        /// <c>SnapToEightDirections</c>），不要再往下取配置对象 —— 那样等于把"玩家参数从哪来"
        /// 的好几个答案又还回去，而 <c>MoveGroup</c> 曾经正是这么做的。
        /// </remarks>
        public PlayerConfig Config => _config;

        /// <summary>水球那一行：射程上限与瞄准平面深度的来源。</summary>
        public ProjectileSpec Ball { get; }

        /// <summary>输入方向是否吸附到 8 向（消费方：<c>PlayerController</c> 的输入处理）。</summary>
        public bool SnapToEightDirections => _config.snapToEightDirections;

        /// <summary>输入缓冲容量（秒）：历史窗口时长，必须 ≥ 下面所有输入各自的窗口。</summary>
        public float InputBufferSeconds => _config.inputBufferTime;

        /// <summary>冲刺冷却（秒）。</summary>
        public float DashCooldownSeconds => _config.dashCooldown;

        /// <summary>冲刺输入缓冲窗口（秒）。</summary>
        public float DashBufferSeconds => _config.dashBufferTime;

        /// <summary>屏幕点投到世界平面时给的相机深度（世界单位）。</summary>
        public float CameraPlaneDepth
            => Ball != null && Ball.Tuning != null ? Ball.Tuning.cameraPlaneDepth : 100f;

        /// <summary>投掷射程上限（世界单位）；取水球那一行。</summary>
        /// <remarks>
        /// <b>射程属"玩家资格"而不是"球的规则"</b>：它回答"我能不能表达这个意图"。
        /// 表列将来若迁到 <c>player</c> 表，改的只是本属性。
        /// <para><c>projectile</c> 表里一行都没有时返回 0，而 <c>TileAim</c> 对非法射程的处理是
        /// "按不限"（它自己的契约）—— 于是"表坏了"的表现是"射程变得很远"，而不是"投不出去"。</para>
        /// </remarks>
        public float MaxThrowDistance => Ball != null ? Ball.MaxThrowDistance : 0f;
    }
}
