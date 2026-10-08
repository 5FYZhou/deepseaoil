using DeepseaOil.Data;
using cfg.dso;

namespace DeepseaOil.Logic.Grid.States
{
    /// <summary>表驱动地块状态：行为全来自 TileStateSpec，效果清单在数据层已解析成定值，这里只按节拍提交并到点到期；所有状态共用这一个实现</summary>
    /// <remarks>减速/伤害是推而非被查询：OnEnter 与每次 OnTick 只提交清单里的效果，找人与施加由结算口完成；interval=0 的效果即每帧触发，泥浆靠每帧续命表达持续，不续就等于离开。清单在进入时提交一次、此后按 TickInterval 重复提交，表里一次性效果本轮无区分口。DoT 扣血节奏在本类累加（格子每帧 Tick），除 GridLogic 外零新增依赖。元素由元素层在状态切换时刷，本类只读表值。计时用一次性 Tick 队列而非协程：暂停时 DeltaTime 为 0 ⇒ 累加不动。</remarks>
    public sealed class TableTileState : ITileState
    {
        private readonly TileStateSpec _spec;

        /// <summary>本状态累计存在时长，判 Duration 到期</summary>
        private float _elapsed;

        /// <summary>效果清单的节拍累加，TickInterval>0 时才有意义</summary>
        private float _tickAccumulator;

        /// <summary>DoT 自身扣血累加，与节拍累加分开</summary>
        private float _dotAccumulator;

        public TableTileState(TileStateSpec spec)
        {
            _spec = spec;
        }

        public TileStateType Id => _spec.Id;

        /// <remarks>进入即开始计时；同状态不重入，已有状态时不会走到这里</remarks>
        public void OnEnter(in TileContext ctx)
        {
            _elapsed = 0f;
            _tickAccumulator = 0f;
            _dotAccumulator = 0f;

            SubmitAll(in ctx);

            ctx.Scheduler?.ScheduleTick(ctx.Cell);
        }

        public void OnTick(in TileContext ctx)
        {
            // 先结算再判到期：到期那一帧也要算上本格效果
            ApplyTick(in ctx);

            if (_spec.Duration > 0f)
            {
                _elapsed += ctx.DeltaTime;

                if (_elapsed >= _spec.Duration)
                {
                    // 到期落回常规；转换在本帧 Tick 循环之后统一结算，不会立刻重入 OnExit
                    ctx.Scheduler?.Transition(ctx.Cell, TileStateType.Normal);

                    return;
                }
            }

            ctx.Scheduler?.ScheduleTick(ctx.Cell);
        }

        public void OnExit(in TileContext ctx)
        {
        }

        /// <summary>按节拍提交效果清单；清单为空也每帧提交 Tick，状态还要判到期</summary>
        private void ApplyTick(in TileContext ctx)
        {
            float interval = _spec.TickInterval;

            if (interval <= 0f)
            {
                SubmitAll(in ctx);
                return;
            }

            _tickAccumulator += ctx.DeltaTime;

            // while 而非 if：一帧跨多拍按次数补齐，掉帧不吞效果
            while (_tickAccumulator >= interval)
            {
                _tickAccumulator -= interval;

                SubmitAll(in ctx);
            }
        }

        /// <summary>清单效果逐个交给结算口；DoT 按 Interval 攒够才提交</summary>
        /// <remarks>ctx.Resolver 为 null（逻辑层单跑测试）时静默跳过，不报错也不自己 new 执行者</remarks>
        private void SubmitAll(in TileContext ctx)
        {
            ITileResolver resolver = ctx.Resolver;

            if (resolver == null) return;

            for (int i = 0; i < _spec.EnterEffects.Count; i++)
            {
                TileEffectValue effect = _spec.EnterEffects[i];

                if (effect.Kind == TileEffectKind.DamageOverTime)
                {
                    if (ShouldFire(in effect, ctx.DeltaTime, ref _dotAccumulator))
                        resolver.Apply(ctx.Cell, in effect);

                    continue;
                }

                resolver.Apply(ctx.Cell, in effect);
            }
        }

        /// <summary>DoT 累加：攒够 Interval 返回 true，一帧跨多拍只提交一次；accumulator 按效果分开持有，暂停时 deltaTime 为 0 不推进</summary>
        private static bool ShouldFire(in TileEffectValue effect, float deltaTime, ref float accumulator)
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
