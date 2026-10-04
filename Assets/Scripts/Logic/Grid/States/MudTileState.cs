using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Grid.States
{
    /// <summary>
    /// 泥浆格：进入时结算一次冲击（伤害 / 击退由配置给出），之后<b>持续减速</b>直到超时落回常规。
    /// </summary>
    /// <remarks>
    /// <b>减速是"被查询"而不是"被推送"的：</b>本状态只回答 <see cref="SlowMultiplier"/>，
    /// 真正把它乘进速度的是踩在上面的角色（<c>EnemyLogic.SetSlowMultiplier</c>）。
    /// 由格子每帧去推给所有踩着的角色会引入"谁先跑"的顺序问题，而查询天然没有顺序。
    /// <para><b>Tick 只用来数时长：</b>本状态没有周期结算，所以 Tick 里除了累加 <c>DeltaTime</c>
    /// 就是再提交一次。用一次性 Tick 队列实现计时而不是协程，是为了让"暂停 = 时间冻结"
    /// 自动成立（<c>DeltaTime</c> 为 0 ⇒ 累加不动 ⇒ 泥浆不消失），并让整条时序可在 EditMode 里喂 dt 复现。</para>
    /// <para><b>时长来自配置行</b>：<c>duration ≤ 0</c> 表示永久，此时连 Tick 都不提交（零开销）。</para>
    /// </remarks>
    public sealed class MudTileState : ITileState
    {
        private readonly TileStateSpec _spec;

        /// <summary>已经持续了多久（秒）。每格一份实例，所以这个字段是每格独立的。</summary>
        private float _elapsed;

        /// <param name="spec">本状态的配置行（由 <c>SpecCatalog.TileState</c> 折算而来）。</param>
        public MudTileState(in TileStateSpec spec)
        {
            _spec = spec;
        }

        /// <inheritdoc />
        public TileStateType Id => _spec.Id;

        /// <inheritdoc />
        public float SlowMultiplier => _spec.SlowFactor;

        /// <inheritdoc />
        /// <remarks>进入即开始计时。已经有状态时不会走到这里（同状态不重入，见 <c>TileStateMachine</c>）。</remarks>
        public void OnEnter(in TileContext ctx)
        {
            _elapsed = 0f;

            if (_spec.Duration > 0f) ctx.Scheduler.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        public void OnTick(in TileContext ctx)
        {
            if (_spec.Duration <= 0f) return;   // 永久状态：不该被调度到，防配置被改成永久后旧请求还在飞

            _elapsed += ctx.DeltaTime;

            if (_elapsed < _spec.Duration)
            {
                ctx.Scheduler.ScheduleTick(ctx.Cell);   // 还想再算：一次性请求必须重新提交
                return;
            }

            // 到期：请求落回常规。转换在**本帧 Tick 循环之后**统一结算，
            // 所以这里不会立刻重入 OnExit（同帧递归防护）。
            ctx.Scheduler.Transition(ctx.Cell, TileStateType.Normal);
        }

        /// <inheritdoc />
        /// <remarks>什么都不做：落地件的显隐由表现层订阅状态变化事件处理，逻辑层不碰视觉。</remarks>
        public void OnExit(in TileContext ctx)
        {
        }
    }
}
