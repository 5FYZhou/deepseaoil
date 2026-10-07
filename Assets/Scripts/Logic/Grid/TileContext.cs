using UnityEngine;
using cfg.demo;
using DeepseaOil.Logic.Combat;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 一次状态回调能看到的全部环境事实。由 <c>GridLogic</c> 构造并注入。
    /// </summary>
    /// <remarks>
    /// <b>为什么把"能做的事"做成端口而不是直接给 <c>GridLogic</c>：</b>状态实现只该知道
    /// "我在哪一格、现在几点、我能提交什么"。给了具体门面，状态就能顺手去改别的格子、
    /// 读别人的状态 —— 于是"状态之间怎么互相影响"这件事会从一处（<c>GridLogic</c>）散到 N 个状态里。
    /// <para><b>端口从三个减到两个</b>（审查已定：结算口合并）：原来伤害与减速各一个口，
    /// 而"这一格上有哪些效果"并不是两件独立的事 —— 合并成一个 <see cref="ITileResolver"/>
    /// 之后，加第三种效果只是加一个 <see cref="TileEffectKind"/> 成员，端口不用再长。
    /// 效果本身用结构体描述，于是"Kind 与参数对不上"这种静默错误由工厂方法堵住。</para>
    /// <para><b>放开档位：</b>状态<b>能提交各种效果，但仍不碰目标</b> ——
    /// 它不拿目标列表、不自己施加，只交一句"这一格要发生什么"。</para>
    /// </remarks>
    public readonly struct TileContext
    {
        /// <summary>本格坐标。</summary>
        public readonly Vector3Int Cell;

        /// <summary>逻辑时间（秒）。</summary>
        public readonly float Now;

        /// <summary>本帧时长（秒）；暂停时为 0。</summary>
        public readonly float DeltaTime;

        /// <summary>提交下一次 Tick / 请求状态转换。</summary>
        public readonly ITileScheduler Scheduler;

        /// <summary>唯一的效果出口；可为 <c>null</c>（逻辑层单跑测试的场合）。</summary>
        public readonly ITileResolver Resolver;

        public TileContext(
            Vector3Int cell,
            float now,
            float deltaTime,
            ITileScheduler scheduler,
            ITileResolver resolver)
        {
            Cell = cell;
            Now = now;
            DeltaTime = deltaTime;
            Scheduler = scheduler;
            Resolver = resolver;
        }
    }

    /// <summary>
    /// 状态提交口：下一次 Tick 与状态转换。
    /// </summary>
    /// <remarks>
    /// <b>两个提交都是"请求"而不是"立刻执行"：</b>Tick 请求进双缓冲队列（本帧提交、下帧消费），
    /// 状态转换进待处理表（本帧的 Tick 循环跑完之后统一结算）——
    /// 于是"状态 A 的 OnTick 把格子切成状态 B、B 立刻又切回 A"这种同帧递归不可能发生。
    /// </remarks>
    public interface ITileScheduler
    {
        /// <summary>请求在下一帧对本格再 Tick 一次。</summary>
        void ScheduleTick(Vector3Int cell);

        /// <summary>请求把本格切换到 <paramref name="next"/>；<b>本帧 Tick 循环结束后</b>统一生效。</summary>
        void Transition(Vector3Int cell, TileStateType next);
    }

    /// <summary>格子能提交的效果种类。</summary>
    public enum TileEffectKind
    {
        /// <summary>哨兵：什么都不做（不该被提交）。</summary>
        None = 0,

        /// <summary>伤害 ＋ 可选的击退。</summary>
        Damage,

        /// <summary>速度修正（减速），续命式。</summary>
        Slow,
    }

    /// <summary>
    /// 一次格子效果的描述：<see cref="Kind"/> ＋ 该种类用到的语义字段。
    /// </summary>
    /// <remarks>
    /// <b>只允许用工厂构造</b>（<see cref="Damage"/> / <see cref="Slow"/>）：这样"Kind 与参数对不上"
    /// 这种错误不可能出现 —— 方案 B（一个入口 ＋ 结构体描述）唯一的风险就是它。
    /// <para>字段是"各种效果共用的一批槽位"：读的时候必须同时看 <see cref="Kind"/>，
    /// 不要只看 <see cref="Amount"/> 之类 —— 工厂保证了"提交方填对了"，读取方仍要按 Kind 分流。</para>
    /// </remarks>
    public readonly struct TileEffect
    {
        /// <summary>效果种类。</summary>
        public readonly TileEffectKind Kind;

        /// <summary>伤害值（<see cref="TileEffectKind.Damage"/>）；<c>0</c> = 只击退。</summary>
        public readonly float Amount;

        /// <summary>击退冲量（<see cref="TileEffectKind.Damage"/>）；<c>0</c> = 不击退。</summary>
        public readonly float Knockback;

        /// <summary>来源标记（<see cref="TileEffectKind.Damage"/>）。</summary>
        public readonly DamageSource Source;

        /// <summary>速度乘数（<see cref="TileEffectKind.Slow"/>）；<c>1</c> = 不减速。</summary>
        public readonly float SpeedScale;

        /// <summary>这次续命能让修饰再活多久（秒，<see cref="TileEffectKind.Slow"/>）。</summary>
        public readonly float Seconds;

        private TileEffect(
            TileEffectKind kind,
            float amount,
            float knockback,
            DamageSource source,
            float speedScale,
            float seconds)
        {
            Kind = kind;
            Amount = amount;
            Knockback = knockback;
            Source = source;
            SpeedScale = speedScale;
            Seconds = seconds;
        }

        /// <summary>一次伤害 ＋ 可选击退。</summary>
        /// <param name="amount">伤害值；<c>0</c> = 只击退。</param>
        /// <param name="knockback">击退冲量；<c>0</c> = 不击退。</param>
        /// <param name="source">来源标记。</param>
        public static TileEffect Damage(float amount, float knockback, DamageSource source)
            => new TileEffect(TileEffectKind.Damage, amount, knockback, source, 1f, 0f);

        /// <summary>续一次减速修饰。</summary>
        /// <param name="speedScale">速度乘数（<c>1</c> = 不减速）。</param>
        /// <param name="seconds">这次续命能让修饰再活多久（秒）。</param>
        public static TileEffect Slow(float speedScale, float seconds)
            => new TileEffect(TileEffectKind.Slow, 0f, 0f, default, speedScale, seconds);
    }

    /// <summary>
    /// <b>唯一的效果出口</b>：对站在该格上的目标施加一次效果。
    /// </summary>
    /// <remarks>
    /// 取代了收口前的两个口（<c>IDamageDealer</c> ＋ <c>ITileSlowApplier</c>）：
    /// "这一格要发生什么"是一个问题，答案不该分成两个方法。
    /// <para><b>找人是执行者的责任</b>：接口只收"哪一格、什么效果"，
    /// 于是状态<b>不持有、也不查询"格上的目标"</b> —— 它只说一句，施加由实现完成。</para>
    /// <para>方向（击退往哪推）也由实现按"格心 → 受害者"逐个算：一格上可能站着不止一个目标，
    /// 方向是<b>每个目标各一份</b>的。</para>
    /// </remarks>
    public interface ITileResolver
    {
        /// <summary>对 <paramref name="cell"/> 上的全部目标施加一次 <paramref name="effect"/>。</summary>
        /// <param name="cell">格子。</param>
        /// <param name="effect">效果描述（用 <see cref="TileEffect"/> 的工厂构造）。</param>
        void Apply(Vector3Int cell, in TileEffect effect);
    }

    /// <summary>
    /// 「会被减速修饰影响」的目标。
    /// </summary>
    /// <remarks>
    /// <b>为什么不挂进 <c>IDamageable</c>：</b>承受修正与能被伤害不是同一件事 ——
    /// 将来的可推动木箱能被推、能被减速，但不该被迫实现一个空方法。
    /// 收窄成独立接口之后，执行者按需筛选。
    /// <para><b>当前只有一个实现者</b>（敌人）：审查已定"玩家只能被怪打，格子作用不到玩家"，
    /// 所以归属表里也没有玩家 —— 那条口径让格子系统完全不认识玩家。</para>
    /// </remarks>
    public interface ISlowEffectTarget
    {
        /// <summary>
        /// 续一次减速修饰。<b>不是"立刻把速度乘一下"</b>：修饰由目标的状态效果层持有，
        /// 由它决定这一帧的速度门禁输出什么。
        /// </summary>
        /// <param name="speedScale">速度乘数（<c>1</c> = 不减速）。</param>
        /// <param name="seconds">存活时长（秒）。</param>
        void ApplySlow(float speedScale, float seconds);
    }
}
