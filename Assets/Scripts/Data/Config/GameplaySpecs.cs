using Assets.Scripts.Data;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个球种的全部数值。<b>纯数据</b>，由 Luban 的 <c>projectile</c> 行填充。
    /// </summary>
    /// <remarks>
    /// 表行 → 本结构体的折算只发生在 <see cref="SpecCatalog"/> 一处，
    /// 于是"表里加一列"的影响面是"一个结构体 + 一个折算点"，而不是散落在所有消费者里。
    /// </remarks>
    public readonly struct BallSpec
    {
        /// <summary>球种（= 表主键）。</summary>
        public readonly BallType Type;

        /// <summary>显示名。</summary>
        public readonly string Name;

        /// <summary>飞行参数（时长 / 弧高 / 距离上下限）。</summary>
        public readonly ThrowSpec Throw;

        /// <summary>落地后目标格转成的状态；<see cref="TileStateType.Normal"/> = 不改格子。</summary>
        public readonly TileStateType TileState;

        public BallSpec(BallType type, string name, in ThrowSpec throwSpec, TileStateType tileState)
        {
            Type = type;
            Name = name;
            Throw = throwSpec;
            TileState = tileState;
        }
    }

    /// <summary>
    /// 一个格子状态的全部数值。<b>纯数据</b>，由 Luban 的 <c>tile_state</c> 行填充。
    /// </summary>
    /// <remarks>
    /// <b>"状态转换时产生一次基础伤害"这条全局设定的数值就在这里</b>（<see cref="EnterDamage"/>）：
    /// 它是状态的属性而不是某个球种的属性 —— 换一种球触发同一个状态，伤害口径不变。
    /// </remarks>
    public readonly struct TileStateSpec
    {
        /// <summary>状态 ID（= 表主键）。</summary>
        public readonly TileStateType Id;

        /// <summary>显示名。</summary>
        public readonly string Name;

        /// <summary>踩在上面的速度系数（<c>1</c> = 不减速）。</summary>
        public readonly float SlowFactor;

        /// <summary>持续时间（秒）；<c>0</c> = 永久（只能被别的状态顶掉）。</summary>
        public readonly float Duration;

        /// <summary>进入该状态时对该格敌人的伤害；<c>0</c> = 不伤害。</summary>
        public readonly float EnterDamage;

        /// <summary>进入该状态时对该格敌人的击退冲量；<c>0</c> = 不击退。</summary>
        public readonly float EnterKnockback;

        public TileStateSpec(
            TileStateType id,
            string name,
            float slowFactor,
            float duration,
            float enterDamage,
            float enterKnockback)
        {
            Id = id;
            Name = name;
            SlowFactor = slowFactor;
            Duration = duration;
            EnterDamage = enterDamage;
            EnterKnockback = enterKnockback;
        }
    }

    /// <summary>一个敌人种类的全部数值。<b>纯数据</b>，由 Luban 的 <c>enemy</c> 行填充。</summary>
    public readonly struct EnemySpec
    {
        public readonly int Id;
        public readonly string Name;

        /// <summary>视觉与碰撞半径（世界单位）。</summary>
        public readonly float Radius;

        /// <summary>追击满速（单位/秒）。</summary>
        public readonly float MaxSpeed;

        /// <summary>加速度（单位/秒²）。</summary>
        public readonly float Acceleration;

        /// <summary>击退滑行的指数衰减率（1/秒）。</summary>
        public readonly float KnockbackDecay;

        /// <summary>进入这个距离就不再压上去（世界单位）。</summary>
        public readonly float StopDistance;

        /// <summary>超出这个距离就放弃追击（世界单位）。</summary>
        public readonly float ChaseRange;

        /// <summary>受击后的禁足时长（秒）。</summary>
        public readonly float StunSeconds;

        /// <summary>耐久。</summary>
        public readonly int Hp;

        /// <summary>受击闪烁频率（Hz）。</summary>
        public readonly float FlashHz;

        public EnemySpec(
            int id, string name, float radius, float maxSpeed, float acceleration,
            float knockbackDecay, float stopDistance, float chaseRange,
            float stunSeconds, int hp, float flashHz)
        {
            Id = id;
            Name = name;
            Radius = radius;
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            KnockbackDecay = knockbackDecay;
            StopDistance = stopDistance;
            ChaseRange = chaseRange;
            StunSeconds = stunSeconds;
            Hp = hp;
            FlashHz = flashHz;
        }
    }

    /// <summary>玩家的血量与受击数值。<b>纯数据</b>，由 Luban 的 <c>player</c> 行填充。</summary>
    /// <remarks>
    /// 与 <c>PlayerConfig</c>（SO）的分工：那份管<b>移动</b>（速度、加速度、冲刺、缓冲窗口，程序调参），
    /// 这份管<b>血量与受击</b>（策划调参）。两者不重叠。
    /// </remarks>
    public readonly struct PlayerSpec
    {
        public readonly int Id;
        public readonly string Name;

        /// <summary>血量上限。</summary>
        public readonly float MaxHp;

        /// <summary>敌人贴身一次造成的伤害。</summary>
        public readonly float ContactDamage;

        /// <summary>受击后的无敌时长（秒）。</summary>
        public readonly float InvulnerableDuration;

        /// <summary>打空后到重来之间的停顿（秒）。</summary>
        public readonly float RetryDelay;

        /// <summary>两次攻击之间的最短间隔（秒）。</summary>
        public readonly float AttackInterval;

        /// <summary>受击被推开的冲量（速度，单位/秒）。</summary>
        public readonly float KnockbackImpulse;

        /// <summary>受击期间的速度上限（单位/秒）；与冲量相等时"补满为止"。</summary>
        public readonly float KnockbackSpeedLimit;

        /// <summary>敌人"贴上了"的圆心距（世界单位）。</summary>
        /// <remarks>
        /// <b>判定用圆心距，不用接触点与法线：</b>玩家与敌人都只有一个碰撞体，圆心距的结论与逐点接触一致，
        /// 而圆心距能用 EditMode 测试直接喂坐标 —— 接触点与法线不能。
        /// <para>它比"两者碰撞半径之和"略小，所以是"几乎贴在身上"而不是"擦到就算"。</para>
        /// </remarks>
        public readonly float ContactRadius;

        public PlayerSpec(
            int id, string name, float maxHp, float contactDamage, float invulnerableDuration,
            float retryDelay, float attackInterval, float knockbackImpulse, float knockbackSpeedLimit,
            float contactRadius)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            ContactDamage = contactDamage;
            InvulnerableDuration = invulnerableDuration;
            RetryDelay = retryDelay;
            AttackInterval = attackInterval;
            KnockbackImpulse = knockbackImpulse;
            KnockbackSpeedLimit = knockbackSpeedLimit;
            ContactRadius = contactRadius;
        }
    }

    /// <summary>一波敌人的数值。<b>纯数据</b>，由 Luban 的 <c>wave</c> 行填充。</summary>
    public readonly struct WaveSpec
    {
        public readonly int Id;
        public readonly string Name;

        /// <summary>每波敌人数。</summary>
        public readonly int EnemiesPerWave;

        /// <summary>同一波内两只敌人之间的间隔（秒）。</summary>
        public readonly float SpawnInterval;

        /// <summary>开局到第一波的等待（秒）。</summary>
        public readonly float InitialDelay;

        /// <summary>清完一波到下一波的等待（秒）。</summary>
        public readonly float RespawnDelay;

        /// <summary>出生环半径（世界单位）。</summary>
        public readonly float SpawnRadius;

        public WaveSpec(
            int id, string name, int enemiesPerWave, float spawnInterval,
            float initialDelay, float respawnDelay, float spawnRadius)
        {
            Id = id;
            Name = name;
            EnemiesPerWave = enemiesPerWave;
            SpawnInterval = spawnInterval;
            InitialDelay = initialDelay;
            RespawnDelay = respawnDelay;
            SpawnRadius = spawnRadius;
        }
    }

    /// <summary>一个格子的初始状态。<b>纯数据</b>，由 Luban 的 <c>tile_initial</c> 行填充。</summary>
    /// <remarks>
    /// 当前没有"关卡"维度：所有行都作用于当前场景。关卡系统立项后要加 <c>level</c> 列，
    /// 由组合根按当前关卡过滤 —— 那是加列，不是改结构。
    /// </remarks>
    public readonly struct TileInitialSpec
    {
        public readonly int CellX;
        public readonly int CellY;
        public readonly TileStateType State;

        public TileInitialSpec(int cellX, int cellY, TileStateType state)
        {
            CellX = cellX;
            CellY = cellY;
            State = state;
        }
    }


    /// <summary>
    /// 一条元素反应规则
    /// </summary>
    public readonly struct ElementRuleSpec
    {
        public readonly int Priority;

        public readonly ElementTag RequireTags;
        public readonly ElementTag ExcludeTags;

        public readonly int TemperatureMin;
        public readonly int TemperatureMax;

        public readonly int WetMin;
        public readonly int WetMax;

        public readonly int ConductivityMin;

        public readonly TileType ResultTileType;

        public ElementRuleSpec(int p, ElementTag r, ElementTag e, 
            int tmin, int tmax, int wmin, int wmax, int cmin, TileType tile)
        {
            Priority = p;
            RequireTags = r;
            ExcludeTags = e; 
            TemperatureMin = tmin; 
            TemperatureMax = tmax; 
            WetMin = wmin; 
            WetMax = cmin;    
            ConductivityMin = tmin;
            ResultTileType = tile;
        }

        public bool Match(ElementSpec a)
        {
            if ((a.Tags & RequireTags) != RequireTags)
                return false;

            if ((a.Tags & ExcludeTags) != 0)
                return false;

            if (a.Temperature < TemperatureMin)
                return false;

            if (a.Temperature > TemperatureMax)
                return false;

            if (a.Wet < WetMin)
                return false;

            if (a.Wet > WetMax)
                return false;

            if (a.Conductivity < ConductivityMin)
                return false;

            return true;
        }
    }
}
