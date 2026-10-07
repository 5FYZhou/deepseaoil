using DeepseaOil.Data;
using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Logic.Grid.States
{
    /// <remarks><b>减速是"推"而不是"被查询"的：</b><see cref="OnEnter"/> 与每次 <see cref="OnTick"/> 只<b>提交</b>一句"这一格续一次减速"，找人与施加由结算口完成（见 <see cref="ITileResolver"/>）；修饰靠每次 Tick 续命，不续就等于"离开泥浆"，所以永久（<c>duration ≤ 0</c>）的泥浆也必须每帧被调度到。
    /// 计时用一次性 Tick 队列而不是协程：于是"暂停 = 时间冻结"自动成立（<c>DeltaTime</c> 为 0 ⇒ 累加不动），整条时序可在 EditMode 里喂 dt 复现。</remarks>
    public sealed class MudTileState : ITileState
    {
        /// <summary>减速修饰的续命间隔（秒）：0.1 ≈ 60fps 下 6 个渲染帧，也 ≥ 一个物理步（0.02 秒）的 5 倍；副作用是"离开泥浆后最多再慢 0.1 秒"，只有帧率极低（&lt; 10fps）时才暴露成一个空档。<b>它不是玩法数值</b>（玩法数值是配置里的 <c>slow_factor</c>），而是"格子跑渲染帧、角色跑物理帧"这条相位差的补偿；真要调它，应当去表里加一列。</summary>
        private const float SlowRefreshSeconds = 0.1f;

        private readonly TileStateSpec _spec;

        private float _elapsed;

        public MudTileState(TileStateSpec spec)
        {
            _spec = spec;
        }

        /// <inheritdoc />
        public TileStateType Id => _spec.Id;

        /// <inheritdoc />
<<<<<<< HEAD
=======
        /// <remarks>进入即开始计时。已经有状态时不会走到这里（同状态不重入，见 <c>TileStateMachine</c>）。</remarks>
>>>>>>> main
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
                    // 到期：请求落回常规。转换在**本帧 Tick 循环之后**统一结算，所以这里不会立刻重入 OnExit（同帧递归防护）。
                    ctx.Scheduler.Transition(ctx.Cell, TileStateType.Normal);

                    return;
                }
            }

            ctx.Scheduler.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        public void OnExit(in TileContext ctx)
        {
        }

        /// <remarks><b>没有执行者时静默跳过</b>（<c>ctx.Resolver</c> 为 <c>null</c>）：那是"逻辑层单独跑测试"的场合，格子状态不该为此报错，也不该自己去 new 一个执行者。</remarks>
        private void SubmitSlow(in TileContext ctx)
        {
            ctx.Resolver?.Apply(ctx.Cell, TileEffect.Slow(_spec.SlowFactor, SlowRefreshSeconds));
        }
    }
}
