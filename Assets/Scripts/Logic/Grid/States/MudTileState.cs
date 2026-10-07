using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Grid.States
{
    /// <summary>一个<b>表驱动</b>的地块状态：它的一切行为都来自 <see cref="TileStateSpec"/> —— 效果清单已在数据层解析成定值，这里只负责"按节拍提交"。泥浆（减速）是它的第一个使用者。</summary>
    /// <remarks>
    /// <b>减速是"推"而不是"被查询"的：</b><see cref="OnEnter"/> 与每次 <see cref="OnTick"/> 只<b>提交</b>清单里的效果，找人与施加由结算口完成（见 <see cref="ITileResolver"/>）；
    /// 减速修饰靠每次 Tick 续命，不续就等于"离开泥浆"，所以永久（<c>duration &lt;= 0</c>）的泥浆也必须每帧被调度到。
    /// <para><b>没有"只在进入那一刻生效"的第二个通道</b>：清单在进入时提交一次、此后按 <c>TickInterval</c> 重复提交；<c>interval = 0</c> 的效果就是每帧触发
    /// （泥浆的减速靠"每帧续命"表达持续，而不是靠一个"续 8 秒"的时长参数）。表里那些<b>一次性</b>效果（如"进格伤害 1 次即销毁"）本轮没有区分口 —— 见报告的待决问题。</para>
    /// <para><b>DoT 的扣血节奏在这里自己累加</b>（D12 二次决策）：格子本身每帧就在 Tick，天然有钟；给每个中毒格挂一个全局计时器只会让格子数变成 timer 数。
    /// 于是本类在 <c>GridLogic</c> 的依赖之外<b>零新增依赖</b>：不吃 <c>ConfigModule</c>、不吃计时器、不吃 <c>Time</c>。</para>
    /// <para>计时用一次性 Tick 队列而不是协程：于是"暂停 = 时间冻结"自动成立（<c>DeltaTime</c> 为 0 ⇒ 累加不动），整条时序可在 EditMode 里喂 dt 复现。</para>
    /// </remarks>
    public sealed class MudTileState : ITileState
    {
        private readonly TileStateSpec _spec;

        /// <summary>本状态累计存在了多久（判 <c>Duration</c> 到期）。</summary>
        private float _elapsed;

        /// <summary>效果清单的节拍累加（<c>TickInterval &gt; 0</c> 时才有意义）。</summary>
        private float _tickAccumulator;

        /// <summary>连续伤害自己的扣血累加（与上面的节拍分开：一件是"状态多久提交一次清单"，一件是"这一格多久扣一次血"）。</summary>
        private float _dotAccumulator;

        public MudTileState(TileStateSpec spec)
        {
            _spec = spec;
        }

        /// <inheritdoc />
        public TileStateType Id => _spec.Id;

        /// <inheritdoc />
        /// <remarks>进入即开始计时。已经有状态时不会走到这里（同状态不重入，见 <c>TileStateMachine</c>）。</remarks>
        public void OnEnter(in TileContext ctx)
        {
            _elapsed = 0f;
            _tickAccumulator = 0f;
            _dotAccumulator = 0f;

            SubmitAll(in ctx);

            ctx.Scheduler?.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        public void OnTick(in TileContext ctx)
        {
            // 先结算再判到期：到期那一帧也该把这一格的效果算上（状态切换发生在本帧 Tick 循环之后）。
            ApplyTick(in ctx);

            if (_spec.Duration > 0f)
            {
                _elapsed += ctx.DeltaTime;

                if (_elapsed >= _spec.Duration)
                {
                    // 到期：请求落回常规。转换在**本帧 Tick 循环之后**统一结算，所以这里不会立刻重入 OnExit（同帧递归防护）。
                    ctx.Scheduler?.Transition(ctx.Cell, TileStateType.Normal);

                    return;
                }
            }

            ctx.Scheduler?.ScheduleTick(ctx.Cell);
        }

        /// <inheritdoc />
        public void OnExit(in TileContext ctx)
        {
        }

        /// <summary>按节拍提交效果清单。清单为空时仍然每帧提交 Tick —— "这个状态暂时没有效果"不该让它悄悄停摆。</summary>
        private void ApplyTick(in TileContext ctx)
        {
            float interval = _spec.TickInterval;

            if (interval <= 0f)
            {
                SubmitAll(in ctx);
                return;
            }

            _tickAccumulator += ctx.DeltaTime;

            // while 而不是 if：一帧跨多个节拍时按次数补齐（掉帧不吞效果）。
            while (_tickAccumulator >= interval)
            {
                _tickAccumulator -= interval;

                SubmitAll(in ctx);
            }
        }

        /// <summary>把清单里的效果逐个交给结算口；连续伤害按自己的 <c>Interval</c> 攒够才提交。</summary>
        /// <remarks><b>没有执行者时静默跳过</b>（<c>ctx.Resolver</c> 为 <c>null</c>）：那是"逻辑层单独跑测试"的场合，格子状态不该为此报错，也不该自己去 new 一个执行者。</remarks>
        private void SubmitAll(in TileContext ctx)
        {
            ITileResolver resolver = ctx.Resolver;

            if (resolver == null) return;

            for (int i = 0; i < _spec.EnterEffects.Count; i++)
            {
                TileEffect effect = _spec.EnterEffects[i];

                if (effect.Kind == TileEffectKind.DamageOverTime)
                {
                    if (ShouldFire(in effect, ctx.DeltaTime, ref _dotAccumulator))
                        resolver.Apply(ctx.Cell, in effect);

                    continue;
                }

                resolver.Apply(ctx.Cell, in effect);
            }
        }

        /// <summary>连续伤害的累加：攒够 <c>Interval</c> 就返回 <c>true</c>（一帧跨多拍只提交一次，不重复扣）。</summary>
        /// <param name="effect">连续伤害效果。</param>
        /// <param name="deltaTime">本帧时长。</param>
        /// <param name="accumulator">该效果的累加器（按效果分开持有，暂停时 <c>deltaTime</c> 为 0 ⇒ 不推进）。</param>
        private static bool ShouldFire(in TileEffect effect, float deltaTime, ref float accumulator)
        {
            if (deltaTime <= 0f) return false;

            float interval = effect.Interval > 0f ? effect.Interval : deltaTime;

            accumulator += deltaTime;

            if (accumulator < interval) return false;

            accumulator = 0f;

            return true;
        }
    }
}
