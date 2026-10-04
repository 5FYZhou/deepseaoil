using UnityEngine;
using cfg.demo;
using DeepseaOil.Logic.Combat;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 一次状态回调能看到的全部环境事实。由 <c>GridLogic</c> 构造并注入。
    /// </summary>
    /// <remarks>
    /// <b>为什么把"能做的事"做成三个端口而不是直接给 <c>GridLogic</c>：</b>状态实现只该知道
    /// "我在哪一格、现在几点、我能提交什么"。给了具体门面，状态就能顺手去改别的格子、
    /// 读别人的状态 —— 于是"状态之间怎么互相影响"这件事会从一处（<c>GridLogic</c>）散到 N 个状态里。
    /// <para><b>本次只被 <c>MudTileState</c> 使用</b>，但三个端口都是"骨架完整"的一部分：
    /// 周期结算（毒 / 火）与联动（蔓延 / 连锁）是格子系统的既定扩展方向。</para>
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

        public TileContext(Vector3Int cell, float now, float deltaTime, ITileScheduler scheduler, IDamageDealer dealer)
        {
            Cell = cell;
            Now = now;
            DeltaTime = deltaTime;
            Scheduler = scheduler;
            Dealer = dealer;
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
}
