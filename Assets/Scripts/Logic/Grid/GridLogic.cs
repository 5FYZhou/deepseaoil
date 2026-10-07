using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子系统的逻辑门面：持有格子状态、调度 Tick、处理落地与状态转换、对格上目标结算伤害。</summary>
    /// <remarks>① 球不伤害敌人：落地只把落点格切成对应状态，伤害全部由格子产生（<see cref="Deal"/>），来源是状态的配置行。
    /// ② <b>同帧递归不存在</b>：Tick 请求进双缓冲队列（<b>本帧提交、下帧消费</b>），状态转换进待处理表（本帧 Tick 循环跑完后统一结算）。谁调它：组合根每渲染帧调一次 <see cref="Tick"/>（<c>dt</c> 用 <c>Time.deltaTime</c> ⇒ <c>timeScale = 0</c> 时格子整体冻结）。</remarks>
    public sealed class GridLogic : ITileScheduler, ITileResolver
    {
        public GridGeometry Geometry => _geometry;

        public EnemyCellRegistry Registry => _registry;

        /// <summary>当前有状态的格数（诊断用；<c>0</c> = 场上没有泥浆之类的东西）。</summary>
        public int ActiveStateCount => _machines.Count;

        public int CellCount => _cells.Count;

        private readonly GridGeometry _geometry;
        private readonly Dictionary<TileStateType, TileStateSpec> _specs = new();
        private readonly Func<TileStateType, ITileState> _stateFactory;
        private readonly EnemyCellRegistry _registry;

        /// <summary>合法格：只有登记过的格能被转换、被减速。来源是表现层从 Tilemap 枚举出的地板。</summary>
        private readonly HashSet<Vector3Int> _cells = new();

        private readonly Dictionary<Vector3Int, TileStateMachine> _machines = new();

        private readonly TileTickQueue _queue = new();

        /// <summary>待生效的状态转换（本帧提交、本帧 Tick 循环之后统一结算，同格后者胜）。</summary>
        private readonly Dictionary<Vector3Int, TileStateType> _pending = new();
        private readonly List<KeyValuePair<Vector3Int, TileStateType>> _pendingScratch = new();

        /// <summary>结算时的目标快照缓冲：受害方可能在结算里死亡并注销自己。</summary>
        private readonly List<IDamageable> _dealScratch = new();

        private float _now;
        private float _deltaTime;

        public GridLogic(
            in GridGeometry geometry,
            IReadOnlyList<TileStateSpec> stateSpecs,
            Func<TileStateType, ITileState> stateFactory,
            EnemyCellRegistry registry = null)
        {
            _geometry = geometry;
            _stateFactory = stateFactory;
            _registry = registry ?? new EnemyCellRegistry();

            if (stateSpecs != null)
            {
                for (int i = 0; i < stateSpecs.Count; i++)
                {
                    _specs[stateSpecs[i].Id] = stateSpecs[i];
                }
            }
        }

        public void RegisterCell(Vector3Int cell)
        {
            _cells.Add(cell);
        }

        /// <summary>本格是否合法（存在地板）。不合法时落地不产生任何效果。</summary>
        public bool HasCell(Vector3Int cell)
        {
            return _cells.Contains(cell);
        }

        public Vector3Int WorldToCell(Vector2 world)
        {
            return _geometry.WorldToCell(world);
        }

        public TileStateType StateOf(Vector3Int cell)
        {
            return _machines.TryGetValue(cell, out TileStateMachine machine)
                ? machine.CurrentId
                : TileStateType.Normal;
        }

        /// <summary>按关卡数据灌入初始状态；<b>刻意不产生伤害</b>（伤害的语义是"状态发生了转换"）；没有地板的格被忽略、不报错。</summary>
        public int LoadInitialStates(IReadOnlyList<TileInitial> states)
        {
            if (states == null) return 0;

            int applied = 0;

            for (int i = 0; i < states.Count; i++)
            {
                TileInitial state = states[i];

                if (state.StateId == TileStateType.Normal) continue;

                var cell = new Vector3Int(state.CellX, state.CellY, 0);

                if (!_cells.Contains(cell)) continue;

                if (SwitchState(cell, state.StateId, applyEnterImpact: false)) applied++;
            }

            return applied;
        }

        public void Tick(float now, float deltaTime)
        {
            _now = now;
            _deltaTime = deltaTime;

            // 翻页：本帧处理的是上一帧提交的请求。
            _queue.Swap();

            IReadOnlyList<Vector3Int> cells = _queue.Current;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];

                if (!_machines.TryGetValue(cell, out TileStateMachine machine)) continue;

                machine.Tick(BuildContext(cell));
            }

            // 本帧 Tick 循环跑完之后才结算状态转换：同帧递归因此不可能发生。
            DrainPendingTransitions();
        }

        /// <summary>一次球落地：把落点格切成指定状态并结算"进入冲击"；落在地板外、或状态没变时为 <c>false</c> —— 反复砸同一格因此不是额外伤害的来源。</summary>
        public bool OnBallHit(Vector3Int cell, TileStateType next)
        {
            return SwitchState(cell, next, applyEnterImpact: true);
        }

        /// <inheritdoc />
        public void ScheduleTick(Vector3Int cell)
        {
            _queue.Schedule(cell);
        }

        /// <inheritdoc />
        /// <remarks>同格在一帧里被请求多次时<b>后者胜</b>：状态的决策以最后一次为准。</remarks>
        public void Transition(Vector3Int cell, TileStateType next)
        {
            _pending[cell] = next;
        }

        /// <inheritdoc />
        /// <remarks>按目标分流：伤害走归属表逐个结算（方向按"格心 → 受害者"各算一份），减速只施加给实现了 <see cref="ISlowEffectTarget"/> 的目标。<b>玩家不在归属表里</b>，所以"泥浆会减速玩家"这条行为不存在。</remarks>
        public void Apply(Vector3Int cell, in TileEffect effect)
        {
            switch (effect.Kind)
            {
                case TileEffectKind.Damage:
                    Deal(cell, in effect);
                    return;

                case TileEffectKind.Slow:
                    ApplySlow(cell, effect.SpeedScale, effect.Seconds);
                    return;
            }
        }

        /// <summary>续一次减速修饰；<b>不做快照</b>（施加修饰不会让目标死亡或注销自己）。离开泥浆 ⇒ 不再续命 ⇒ 修饰自然过期；暂停 ⇒ 格子不 Tick ⇒ 恢复后立刻续上。</summary>
        private void ApplySlow(Vector3Int cell, float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IDamageable> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ISlowEffectTarget target) target.ApplySlow(speedScale, seconds);
            }
        }

        private void Deal(Vector3Int cell, in TileEffect effect)
        {
            if (effect.Amount <= 0f && effect.Knockback <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IDamageable> targets) || targets.Count == 0) return;

            // 快照：受害方可能在 TakeDamage 里死亡并把自己从登记表摘掉，直接在原列表上遍历会改到正在遍历的集合。
            _dealScratch.Clear();
            _dealScratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);

            for (int i = 0; i < _dealScratch.Count; i++)
            {
                IDamageable target = _dealScratch[i];

                // null 必须先判：已销毁的 MonoBehaviour 一碰 transform 就抛。
                if (target == null) continue;

                // 走 IAlivable：已死但还没被销毁的目标不该被重复结算；只实现 IDamageable 的目标没有"死活"概念。
                if (target is IAlivable livable && !livable.IsAlive) continue;

                target.TakeDamage(Damage.At(center, target.Position, effect.Amount, effect.Source, effect.Knockback));
            }
        }

        /// <summary>切换某格的状态；同状态时是 no-op（不重入、不重置计时）。"切换"与"冲击"分两个参数：球落地与状态自己发起的转换都是"切换 + 给冲击"，开局加载是"只切换、绝不给冲击"。</summary>
        public bool SwitchState(Vector3Int cell, TileStateType next, bool applyEnterImpact)
        {
            if (!_cells.Contains(cell)) return false;

            // 没有实现的状态（配置里没有这一行）不算一次转换：否则"转换"会凭空发生一次（一条事件 + 一次冲击），而场上什么都没变。
            if (next != TileStateType.Normal && !_specs.ContainsKey(next)) return false;

            if (!_machines.TryGetValue(cell, out TileStateMachine machine))
            {
                // 常规 → 常规：什么都没发生。**不建状态机**，否则"土球砸地板"每一下都留一份垃圾。
                if (next == TileStateType.Normal) return false;

                machine = new TileStateMachine();

                RegisterStateFactories(machine);
            }

            bool changed = machine.SwitchTo(next, BuildContext(cell));

            if (!changed) return false;

            // 落回常规的格不再保留状态机：常规格零常驻。
            if (machine.CurrentId == TileStateType.Normal) _machines.Remove(cell);
            else _machines[cell] = machine;

            EventBus<TileStateChanged>.Publish(new TileStateChanged(cell, next));

            if (applyEnterImpact) ApplyEnterImpact(cell, next);

            return true;
        }

        private void ApplyEnterImpact(Vector3Int cell, TileStateType state)
        {
            if (!_specs.TryGetValue(state, out TileStateSpec spec)) return;

            if (spec.EnterDamage <= 0f && spec.EnterKnockback <= 0f) return;

            Apply(cell, TileEffect.Damage(spec.EnterDamage, spec.EnterKnockback, DamageSource.Tile));
        }

        private void DrainPendingTransitions()
        {
            if (_pending.Count == 0) return;

            // 先搬走再处理：处理过程中（事件订阅方 / 状态回调）发起的新请求留到下一帧，于是"一帧内能发生的连锁"有确定上界。
            _pendingScratch.Clear();

            foreach (KeyValuePair<Vector3Int, TileStateType> pair in _pending)
            {
                _pendingScratch.Add(pair);
            }

            _pending.Clear();

            for (int i = 0; i < _pendingScratch.Count; i++)
            {
                KeyValuePair<Vector3Int, TileStateType> pair = _pendingScratch[i];

                SwitchState(pair.Key, pair.Value, applyEnterImpact: true);
            }

            _pendingScratch.Clear();
        }

        private void RegisterStateFactories(TileStateMachine machine)
        {
            if (_stateFactory == null) return;

            // 只有配置里出现过的 ID 才注册工厂：没配置的状态即使有实现也不该被切进去。
            foreach (KeyValuePair<TileStateType, TileStateSpec> pair in _specs)
            {
                TileStateType id = pair.Key;

                machine.Register(id, () => _stateFactory(id));
            }
        }

        private TileContext BuildContext(Vector3Int cell)
        {
            return new TileContext(cell, _now, _deltaTime, this, this);
        }
    }
}
