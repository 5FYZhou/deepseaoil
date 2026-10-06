using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Grid.States
{
    /// <summary>
    /// 泥浆格：进入时结算一次冲击（伤害 / 击退由配置给出），之后<b>持续减速</b>直到超时落回常规。
    /// </summary>
    /// <remarks>
    /// <b>减速是"推"而不是"被查询"的：</b>本状态在 <see cref="OnEnter"/> 与每次
    /// <see cref="OnTick"/> 里只<b>提交</b>一句"这一格续一次减速"，找人与施加由执行者完成
    /// （见 <c>ITileSlowApplier</c>）。收口前它是"被查询的系数"（<c>SlowMultiplier</c>），
    /// 于是每个踩在格上的角色都要自己去问一次格子 —— 那是"谁先跑"的顺序问题的温床，
    /// 也把"目标在不在这一格"的知识复制到了每个角色身上。
    /// <para><b>Tick 只用来数时长与续命：</b>本状态没有周期结算，但<b>必须每帧被调度到</b> ——
    /// 减速修饰靠每次 Tick 续命，不续就等于"离开泥浆"。所以永久（<c>duration ≤ 0</c>）的泥浆
    /// 也会一直 Tick（代价是每帧一次队列提交，换取"永久减速"这条配置真的成立）。</para>
    /// <para><b>用一次性 Tick 队列实现计时而不是协程</b>，是为了让"暂停 = 时间冻结"自动成立
    /// （<c>DeltaTime</c> 为 0 ⇒ 累加不动 ⇒ 泥浆不消失），并让整条时序可在 EditMode 里喂 dt 复现。</para>
    /// </remarks>
    public sealed class MudTileState : ITileState
    {
        /// <summary>
        /// 减速修饰的续命间隔（秒）。
        /// </summary>
        /// <remarks>
        /// 0.1 秒 ≈ 60fps 下 6 个渲染帧，也 ≥ 一个物理步（0.02 秒）的 5 倍。
        /// 它的副作用是"离开泥浆后最多再慢 0.1 秒"——肉眼不可见；只有帧率极低（&lt; 10fps）时
        /// 才会暴露成一个空档。<b>它不是玩法数值</b>（玩法数值是配置里的 <c>slow_factor</c>），
        /// 而是"格子跑渲染帧、角色跑物理帧"这条相位差的补偿；真要调它，应当去表里加一列。
        /// </remarks>
        private const float SlowRefreshSeconds = 0.1f;

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
        /// <remarks>进入即开始计时并<b>立刻续一次减速</b>：站在上面的目标不该等到下一帧才被影响。</remarks>
        public void OnEnter(in TileContext ctx)
        {
            _elapsed = 0f;

            SubmitSlow(in ctx);

            ctx.Scheduler.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        public void OnTick(in TileContext ctx)
        {
            // 先续命再判到期：到期那一帧也应当把修饰续上（状态切换发生在本帧 Tick 循环之后）。
            SubmitSlow(in ctx);

            if (_spec.Duration > 0f)
            {
                _elapsed += ctx.DeltaTime;

                if (_elapsed >= _spec.Duration)
                {
                    // 到期：请求落回常规。转换在**本帧 Tick 循环之后**统一结算，
                    // 所以这里不会立刻重入 OnExit（同帧递归防护）。
                    ctx.Scheduler.Transition(ctx.Cell, TileStateType.Normal);

                    return;
                }
            }

            // 还想再算（永久状态也一样：减速要靠续命才活着）。
            ctx.Scheduler.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        /// <remarks>什么都不做：修饰会自己过期（不再被续命），而落地的显隐由表现层订阅事件处理。</remarks>
        public void OnExit(in TileContext ctx)
        {
        }

        /// <summary>
        /// 提交一次减速修饰。
        /// </summary>
        /// <remarks><b>没有执行者时静默跳过</b>（<c>ctx.Slow</c> 为 <c>null</c>）：那是"逻辑层单独跑测试"
        /// 的场合，格子状态不该为此报错，也不该自己去 new 一个执行者。</remarks>
        private void SubmitSlow(in TileContext ctx)
        {
            ctx.Slow?.ApplySlow(ctx.Cell, _spec.SlowFactor, SlowRefreshSeconds);
        }
    }
}
