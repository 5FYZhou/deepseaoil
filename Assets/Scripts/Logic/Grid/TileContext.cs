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
    /// <para><b>端口是"骨架完整"的一部分：</b>周期结算（毒 / 火）、联动（蔓延 / 连锁）、
    /// 对格上目标施加修正（减速 / 加速）都是格子系统的既定扩展方向。</para>
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

        /// <summary>对本格结算一次伤害。</summary>
        public readonly IDamageDealer Dealer;

        /// <summary>对站在本格上的目标续一次速度修正（减速）。</summary>
        public readonly ITileSlowApplier Slow;

        public TileContext(
            Vector3Int cell,
            float now,
            float deltaTime,
            ITileScheduler scheduler,
            IDamageDealer dealer,
            ITileSlowApplier slow)
        {
            Cell = cell;
            Now = now;
            DeltaTime = deltaTime;
            Scheduler = scheduler;
            Dealer = dealer;
            Slow = slow;
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

    /// <summary>
    /// 伤害结算口：对<b>站在该格上</b>的目标逐个结算。
    /// </summary>
    /// <remarks>
    /// 方向由结算方按"格心 → 受害者"逐个算，所以这里只收"多少伤害、多少击退"，
    /// 不收方向 —— 一格上可能站着不止一个目标，方向是<b>每个目标各一份</b>的。
    /// </remarks>
    public interface IDamageDealer
    {
        /// <summary>对 <paramref name="cell"/> 上的全部目标结算一次。</summary>
        /// <param name="cell">格子。</param>
        /// <param name="amount">伤害值；<c>0</c> = 只击退。</param>
        /// <param name="knockback">击退冲量；<c>0</c> = 不击退。</param>
        /// <param name="source">来源标记。</param>
        void Deal(Vector3Int cell, float amount, float knockback, DamageSource source);
    }

    /// <summary>
    /// 速度修正口：对<b>站在该格上</b>的目标续一次减速修饰。
    /// </summary>
    /// <remarks>
    /// <b>为什么是"续一次"而不是"设一个状态"：</b>格状态<b>不持有、也不查询"格上的目标"</b> ——
    /// 它只在 <c>OnEnter</c> / <c>OnTick</c> 里说一句"这一格续一次减速"，找人与施加都由执行者完成。
    /// 于是"谁摘掉这个修饰"这个问题自动消失：离开泥浆 ⇒ 不再续命 ⇒ 修饰自然过期；
    /// 暂停 ⇒ 格子不 Tick ⇒ 恢复后立刻续上，无感。
    /// <para><b>代价写在明处：</b>修饰的时长语义因此变成"格子续命间隔"，
    /// 它必须大于一个渲染帧 + 一个物理帧，否则会出现"踩着泥浆却不受影响"的空档。</para>
    /// </remarks>
    public interface ITileSlowApplier
    {
        /// <summary>给 <paramref name="cell"/> 上的目标续一次减速修饰。</summary>
        /// <param name="cell">格子。</param>
        /// <param name="speedScale">速度乘数（<c>1</c> = 不减速）。</param>
        /// <param name="seconds">这次续命能让修饰再活多久（秒）。</param>
        void ApplySlow(Vector3Int cell, float speedScale, float seconds);
    }

    /// <summary>
    /// 「会被减速修饰影响」的目标。
    /// </summary>
    /// <remarks>
    /// <b>为什么不挂进 <c>IDamageable</c>：</b>承受修正与能被伤害不是同一件事 ——
    /// 将来的可推动木箱能被推、能被减速，但不该被迫实现一个空方法。
    /// 收窄成独立接口之后，执行者按需筛选。
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
