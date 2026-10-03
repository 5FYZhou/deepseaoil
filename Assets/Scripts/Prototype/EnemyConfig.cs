using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 一只白模敌人的全部数值（<b>纯数据</b>）。
    /// </summary>
    /// <remarks>
    /// <b>为什么不复用 <c>CharacterConfig</c> 的 ScriptableObject：</b>那会多出一个必须手工拖引用、
    /// 且要单独维护的资产文件，而白模的全部可调数字约定只写在一处（<see cref="ThrowConstants"/>）。
    /// 本类型只是把那几个常量捡出来放进一个实例，于是"调参改哪"这个问题永远只有一个答案。
    /// <para><b>它是唯一与 <see cref="ThrowConstants"/> 的接触点</b>：常量名改了这里编译不过，
    /// 而实现里不会散落着常量名 —— 想换一整套敌人数值（比如第二阶段的"精英怪"），
    /// 换一个实例即可，不必去实现里翻。</para>
    /// <para>本类型不做减益计算、不读时间、不持状态：这两个方法都是<b>静态纯函数</b>。</para>
    /// </remarks>
    public sealed class EnemyConfig
    {
        /// <summary>视觉与碰撞半径（世界单位）。</summary>
        public readonly float Radius;

        /// <summary>追击满速（单位/秒）。</summary>
        public readonly float MaxSpeed;

        /// <summary>加速度（单位/秒²）。</summary>
        public readonly float Acceleration;

        /// <summary>击退滑行的指数衰减率（1/秒）。</summary>
        public readonly float DecayPerSecond;

        /// <summary>停止逼近的距离（世界单位）。</summary>
        public readonly float StopDistance;

        /// <summary>放弃追击的距离（世界单位）。</summary>
        public readonly float ChaseRange;

        /// <summary>
        /// 被砸中后的禁足时长（秒）。
        /// </summary>
        /// <remarks>
        /// 这段时间内敌人既不转向也不衰减，冲量原样保留 —— 所以它同时是"击退飞多远"的旋钮
        /// （位移 ≈ 冲量 × 本值）。见 <see cref="ThrowConstants.ENEMY_STUN_DURATION"/>。
        /// </remarks>
        public readonly float StunSeconds;

        /// <summary>
        /// 按 <see cref="ThrowConstants"/> 取一套白模敌人数值。
        /// </summary>
        /// <remarks>
        /// 常量是编译期常量，参数无参；做成实例方法而不是把常量直接读进实现，是为了让"敌人的一套数值"
        /// 在测试里可以被完整替换掉。
        /// </remarks>
        public EnemyConfig()
            : this(
                ThrowConstants.ENEMY_RADIUS_METERS,
                ThrowConstants.ENEMY_SPEED,
                ThrowConstants.ENEMY_ACCELERATION,
                ThrowConstants.ENEMY_KNOCKBACK_DECAY,
                ThrowConstants.ENEMY_STOP_DISTANCE,
                ThrowConstants.ENEMY_CHASE_RANGE,
                ThrowConstants.ENEMY_STUN_DURATION)
        {
        }

        /// <param name="radius">半径。</param>
        /// <param name="maxSpeed">追击满速。</param>
        /// <param name="acceleration">加速度。</param>
        /// <param name="decayPerSecond">衰减率。</param>
        /// <param name="stopDistance">停止逼近距离。</param>
        /// <param name="chaseRange">放弃追击距离。</param>
        /// <param name="stunSeconds">被砸中后的禁足时长。</param>
        public EnemyConfig(
            float radius,
            float maxSpeed,
            float acceleration,
            float decayPerSecond,
            float stopDistance,
            float chaseRange,
            float stunSeconds)
        {
            Radius = radius;
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            DecayPerSecond = decayPerSecond;
            StopDistance = stopDistance;
            ChaseRange = chaseRange;
            StunSeconds = stunSeconds;
        }

        /// <summary>
        /// 折算成 <c>ActorLogic</c> 要求的角色配置。
        /// </summary>
        /// <remarks>
        /// <b>为什么不复用玩家那个 <c>ScriptableObject</c> 资产：</b>两者要的是不同的角色行为
        /// （玩家零惯性、敌人有加减速），共用一份配置只会让改一个数影响另一个。
        /// 而白模的全部数字约定只写在一处（<see cref="ThrowConstants"/>），
        /// 所以这里<b>运行期造一个</b>而不是新增一个需要人工拖引用的资产文件 ——
        /// 白模的失败模式绝大多数是"忘了接线"，能少一处接线就少一处。
        /// <para><b>与敌人无关的项显式清零</b>（冲刺、8 向吸附）：<c>CharacterConfig</c> 的字段
        /// 是带默认值的公开字段，不写就是"继承了玩家那份默认值"。
        /// 未显式赋值的默认值不会报错，只会在将来某处被读到 —— 比如某天有人给敌人加了"冲刺"。</para>
        /// <para><b>逐只敌人各建一份</b>：对象的创建成本与"四处复制配置常量"的风险相比微不足道，
        /// 而共享实例会引入一个隐蔽的耦合（谁改了它，全场敌人都变）。</para>
        /// </remarks>
        public CharacterConfig BuildCharacterConfig()
        {
            var config = ScriptableObject.CreateInstance<CharacterConfig>();

            config.name = "白模敌人配置";

            config.Name = "白模敌人";
            config.moveSpeed = MaxSpeed;
            config.snapToEightDirections = false;   // 敌人不吃输入，吸附与否无关；显式关掉避免将来误用
            config.moveAcceleration = Acceleration;
            config.turnDecayRate = DecayPerSecond;
            config.extraForceScale = 0f;            // 敌人不走 ApplyExtraForce（速度由自己的账本写）
            config.dashSpeed = 0f;                  // 敌人没有冲刺
            config.dashDuration = 0f;

            return config;
        }

        /// <summary>
        /// 泥浆减速系数：把外因给的比例钳进 <c>[<see cref="ThrowConstants.MUD_SLOW_FACTOR"/>, 1]</c>。
        /// </summary>
        /// <param name="mudSlowFromSource">外因（水球落地）给的比例。不给减益时传 <c>1</c>。</param>
        /// <returns>追击速度要乘的系数，恒在 <c>[MUD_SLOW_FACTOR, 1]</c> 内。</returns>
        /// <remarks>
        /// <b>这个钳制不是防御性编程，是设计边界。</b><see cref="ThrowConstants.MUD_SLOW_FACTOR"/>
        /// 是"最慢能慢到多少"的唯一真值，任何外因都不得比它更慢 ——
        /// 否则一个把比例算成 <c>0</c> 的调用方会把敌人<b>定死</b>（速度恒为 0、永远追不上、也不报错），
        /// 而"定身"是另一套机制（需要时长、需要抵抗），不该由一次伤害顺手做出来。
        /// <para><c>NaN</c> 参与比较恒为 <c>false</c>，<c>Mathf.Clamp(NaN, …)</c> 会原样返回 <c>NaN</c>，
        /// 于是 <c>NaN</c> 会一路传染进速度里。所以这里对非数单独判、按"不起作用"处理。</para>
        /// </remarks>
        public static float SlowMultiplier(float mudSlowFromSource)
        {
            if (float.IsNaN(mudSlowFromSource)) return 1f;

            return Mathf.Clamp(mudSlowFromSource, ThrowConstants.MUD_SLOW_FACTOR, 1f);
        }

        /// <summary>
        /// 换算一次打击该给多少冲量，使"泥浆减速"与"击退"不打架。
        /// </summary>
        /// <param name="knockback">该球种的击退强度（土球整额、水球按 <see cref="ThrowConstants.WATER_KNOCKBACK_SCALE"/> 折减）。</param>
        /// <param name="slowMultiplier">本帧的泥浆系数（<see cref="SlowMultiplier"/> 的输出）。</param>
        /// <returns>实际要交给速度账本的冲量（单位/秒），<b>不低于 0</b>。</returns>
        /// <remarks>
        /// <b>这条是"两个效果叠加"唯一会出错的地方。</b>泥浆减速是"把目标速度压到 基准 × 系数"，
        /// 击退是"给它一股与方向无关的速度"。两者都动速度，于是有一个反直觉的后果：
        /// 站在泥浆里的敌人被砸得越狠，下一帧反而被压得越低端 —— 因为它的目标速度变低了，
        /// 冲量越高，"减掉多少"就越多，读起来像"泥浆把它吸住了"，而不是"被推开了"。
        /// <para>处理办法：让击退<b>按泥浆系数折减</b>，两边同向 —— 泥浆里被推得也近，语义自洽。</para>
        /// <para>折减后为负（泥浆系数为 0 或负）时返回 0 而不是负数：负冲量会把敌人<b>吸向</b>落点。</para>
        /// </remarks>
        public static float ScaledKnockback(float knockback, float slowMultiplier)
        {
            float scaled = knockback * Mathf.Clamp01(slowMultiplier);

            return scaled > 0f ? scaled : 0f;
        }
    }
}
