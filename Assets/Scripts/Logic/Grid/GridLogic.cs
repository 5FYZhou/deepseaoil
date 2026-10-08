using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.dso;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子系统逻辑门面，持有格子状态、调度 Tick、处理落地与状态转换、对格上目标结算效果</summary>
    /// <remarks>伤害全由格子产生（Apply），来源是状态配置行。Tick 请求双缓冲：本帧提交、下帧消费；状态转换进待处理表，Tick 循环后统一结算。表数据全部构造注入，元素合成与规则匹配在元素层，本类不碰 Tilemap/物理/Time/ConfigModule。</remarks>
    public sealed class GridLogic : ITileScheduler, ITileResolver
    {
        /// <summary>击退衰减率（1/秒），同 EnemySpec.KnockbackDecay，只用于格→冲量换算</summary>
        private const float KnockbackDecayPerSecond = 10f;

        public GridGeometry Geometry => _geometry;

        public EnemyCellRegistry Registry => _registry;

        /// <summary>当前有状态的格数（诊断用，0=场上无泥浆之类）</summary>
        public int ActiveStateCount => _machines.Count;

        public int CellCount => _cells.Count;

        private readonly GridGeometry _geometry;
        private readonly Dictionary<TileStateType, TileStateSpec> _specs = new();
        private readonly Dictionary<TileEffectType, TileEffectSpec> _effectSpecs = new();
        private readonly Func<TileStateType, ITileState> _stateFactory;
        private readonly EnemyCellRegistry _registry;
        private readonly IElementReactor _element;

        /// <summary>合法格，只有登记过的格能被转换/结算；表现层从 Tilemap 枚举地板登记</summary>
        private readonly HashSet<Vector3Int> _cells = new();

        private readonly Dictionary<Vector3Int, TileStateMachine> _machines = new();

        private readonly TileTickQueue _queue = new();

        /// <summary>待生效状态转换，Tick 循环后统一结算，同格后者胜</summary>
        private readonly Dictionary<Vector3Int, TileStateType> _pending = new();
        private readonly List<KeyValuePair<Vector3Int, TileStateType>> _pendingScratch = new();

        /// <summary>结算时目标快照缓冲，受害方可能在结算里死亡并注销自己</summary>
        private readonly List<IEffectTarget> _dealScratch = new();

        private float _now;
        private float _deltaTime;

        /// <remarks>stateFactory 返回 null=该 ID 没有实现；element=null 时落地不产生反应；TileEffectValues=tile_effect 表包装件，连续伤害的扣血节奏与伤害值以它为准；registry=null 时自建。</remarks>
        public GridLogic(
            in GridGeometry geometry,
            IReadOnlyList<TileStateSpec> stateSpecs,
            Func<TileStateType, ITileState> stateFactory,
            IElementReactor element = null,
            IReadOnlyList<TileEffectSpec> TileEffectValues = null,
            EnemyCellRegistry registry = null)
        {
            _geometry = geometry;
            _stateFactory = stateFactory;
            _element = element;
            _registry = registry ?? new EnemyCellRegistry();

            if (stateSpecs != null)
            {
                for (int i = 0; i < stateSpecs.Count; i++)
                {
                    _specs[stateSpecs[i].Id] = stateSpecs[i];
                }
            }

            if (TileEffectValues != null)
            {
                for (int i = 0; i < TileEffectValues.Count; i++)
                {
                    _effectSpecs[TileEffectValues[i].Id] = TileEffectValues[i];
                }
            }
        }

        /// <summary>登记一个合法格，同时按该格原本是什么地从 tile_state 表灌一次元素</summary>
        /// <remarks>D9 只覆盖切状态，常规格没人刷过、元素恒为空，整条反应链判据会不同。默认状态由 LoadInitialStates 给，两处都登记时后到者优先。</remarks>
        public void RegisterCell(Vector3Int cell, TileStateType initial = TileStateType.Normal)
        {
            if (!_cells.Add(cell)) return;

            SeedElement(cell, initial);
        }

        private void SeedElement(Vector3Int cell, TileStateType initial)
        {
            if (_element == null) return;

            if (_machines.ContainsKey(cell)) return;

            if (!_specs.TryGetValue(initial, out TileStateSpec spec)) return;

            _element.FlushStateElement(cell, in spec);
        }

        /// <summary>本格是否合法（存在地板），不合法时落地无效果</summary>
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

        /// <summary>按关卡数据灌入初始状态，刻意不产生伤害，无地板的格忽略不报错</summary>
        public int LoadInitialStates(IReadOnlyList<TileInitial> states)
        {
            if (states == null) return 0;

            int applied = 0;

            for (int i = 0; i < states.Count; i++)
            {
                TileInitial state = states[i];

                var cell = new Vector3Int(state.CellX, state.CellY, 0);

                if (!_cells.Contains(cell)) continue;

                RegisterCell(cell, state.StateId);

                if (state.StateId == TileStateType.Normal) continue;

                if (SwitchState(cell, state.StateId, applyEnterImpact: false)) applied++;
            }

            return applied;
        }

        public void Tick(float now, float deltaTime)
        {
            _now = now;
            _deltaTime = deltaTime;

            _queue.Swap();

            IReadOnlyList<Vector3Int> cells = _queue.Current;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];

                if (!_machines.TryGetValue(cell, out TileStateMachine machine)) continue;

                machine.Tick(BuildContext(cell));
            }

            DrainPendingTransitions();
        }

        /// <summary>一次球落地：合成球元素与地形元素→查反应规则→切结果状态并提交效果清单</summary>
        /// <remarks>返回 false：落在地板外、规则不给状态（None）或结果就是当前状态。没切状态时把元素改动收回去，否则兜底行（结果 None）会把合成结果留在格子上。</remarks>
        public bool OnBallHit(Vector3Int cell, in ElementValue ballElement)
        {
            if (!_cells.Contains(cell)) return false;

            if (_element == null) return false;

            TileStateType current = StateOf(cell);

            if (!_specs.TryGetValue(current, out TileStateSpec currentSpec)) return false;

            ElementReaction reaction = _element.React(cell, in ballElement, in currentSpec);

            if (reaction.Next == TileStateType.None)
            {
                _element.FlushStateElement(cell, in currentSpec);

                return false;
            }

            bool changed = SwitchState(cell, reaction.Next, applyEnterImpact: true, reaction.Effects);

            if (!changed) _element.FlushStateElement(cell, in currentSpec);

            return changed;
        }

        public void ScheduleTick(Vector3Int cell)
        {
            _queue.Schedule(cell);
        }

        /// <remarks>同格一帧内被请求多次时后者胜。</remarks>
        public void Transition(Vector3Int cell, TileStateType next)
        {
            _pending[cell] = next;
        }

        /// <remarks>按目标能力分流：受伤/减速/击退/麻痹各吃各的；玩家不在归属表里（D7），泥浆不减速玩家。</remarks>
        public void Apply(Vector3Int cell, in TileEffectValue effect)
        {
            switch (effect.Kind)
            {
                case TileEffectKind.InstantDamage:
                    Deal(cell, effect.Amount);
                    return;

                case TileEffectKind.DamageOverTime:
                    // 数值与节奏以 tile_effect 表为准，取不到用效果定值。
                    if (_effectSpecs.TryGetValue(TileEffectType.DamageOverTime, out TileEffectSpec dot))
                    {
                        TileEffectValue table = dot.GetEffect(1);

                        if (table.Kind == TileEffectKind.DamageOverTime)
                        {
                            Deal(cell, table.PerTick);
                            return;
                        }
                    }

                    Deal(cell, effect.PerTick);
                    return;

                case TileEffectKind.Slow:
                    ApplySlow(cell, effect.Scale, effect.Seconds);
                    return;

                case TileEffectKind.KnockBack:
                    ApplyKnockback(cell, effect.Cells);
                    return;

                case TileEffectKind.Numbness:
                    ApplyStun(cell, effect.Seconds);
                    return;

                // Slide 本轮未实现（缺滑行能力接口）；None/Skid/Block/Fixed 本轮不做。
                default:
                    return;
            }
        }

        /// <remarks>只改 cell 自身（D4：状态实现不许碰别的格）；温湿度继承与清除植物都走这里。</remarks>
        public void ApplyToCell(Vector3Int cell, in TileEffectValue effect)
        {
            if (_element == null) return;

            switch (effect.Kind)
            {
                case TileEffectKind.InheritElement:
                {
                    ElementValue current = _element.GetElement(cell);

                    _element.SetElement(cell, new ElementValue(
                        current.Type,
                        current.Tags,
                        Mathf.RoundToInt(current.Temperature * effect.TemperatureRatio),
                        Mathf.RoundToInt(current.Wet * effect.WetRatio),
                        Mathf.RoundToInt(current.Conductivity * effect.ConductivityRatio)));

                    return;
                }

                case TileEffectKind.ClearPlants:
                {
                    ElementValue current = _element.GetElement(cell);

                    // 只清本格：表里范围是十字，跨格需要"对邻居下命令"的通道（D4）。
                    _element.SetElement(cell, new ElementValue(
                        current.Type,
                        current.Tags & ~ElementTag.Plant,
                        current.Temperature,
                        current.Wet,
                        current.Conductivity));

                    return;
                }

                default:
                    return;
            }
        }

        public void SetCellElement(Vector3Int cell, in ElementValue element)
        {
            _element?.SetElement(cell, in element);
        }

        /// <summary>续一次减速修饰，不做快照；离开泥浆⇒不再续命⇒修饰自然过期。</summary>
        private void ApplySlow(Vector3Int cell, float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ISlowable target) target.ApplySlow(speedScale, seconds);
            }
        }

        /// <summary>按格数击退：冲量 = 格数 × 格边长 × 击退衰减率</summary>
        /// <remarks>value1 单位是格，ApplyKnockback 收速度（单位/秒）；总位移 ≈ 冲量/衰减率，故乘回衰减率。换算在执行者。</remarks>
        private void ApplyKnockback(Vector3Int cell, float cells)
        {
            if (cells <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            float magnitude = cells * Mathf.Max(_geometry.CellSize, 0f) * KnockbackDecayPerSecond;

            if (magnitude <= 0f) return;

            Vector2 center = _geometry.CellCenter(cell);

            for (int i = 0; i < targets.Count; i++)
            {
                IEffectTarget target = targets[i];

                // null 先判：已销毁的 MonoBehaviour 碰 transform 会抛。
                if (target == null) continue;

                if (!target.IsAlive) continue;

                if (target is not IKnockBackable knockbackable) continue;

                Vector2 delta = target.Position - center;

                // 格心方向为零：与 Damage.At 同纪律，给"上"。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                knockbackable.ApplyKnockback(direction * magnitude);
            }
        }

        /// <summary>麻痹：时长交给目标，是否进门禁由目标决定（本轮只到"提交"层）。</summary>
        private void ApplyStun(Vector3Int cell, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is IStunnable target) target.ApplyStun(seconds);
            }
        }

        /// <summary>对格上目标结算一次伤害（方向按格心→受害者各算一份）</summary>
        private void Deal(Vector3Int cell, float amount)
        {
            if (amount <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            _dealScratch.Clear();
            _dealScratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);

            for (int i = 0; i < _dealScratch.Count; i++)
            {
                IEffectTarget target = _dealScratch[i];

                if (target == null) continue;

                if (!target.IsAlive) continue;

                // 不能受伤的目标照样能被减速与击退，只是不吃伤害。
                if (target is not IDamageable damageable) continue;

                damageable.TakeDamage(Damage.At(center, target.Position, amount, DamageSource.Tile));
            }
        }

        /// <summary>切换某格状态，同状态时 no-op（不重入、不重置计时）；applyEnterImpact=是否给进格冲击：球落地与状态自发起给，开局加载绝不给</summary>
        /// <remarks>enterEffects=null 用状态自带的 spec.EnterEffects。</remarks>
        public bool SwitchState(
            Vector3Int cell,
            TileStateType next,
            bool applyEnterImpact,
            IReadOnlyList<TileEffectValue> enterEffects = null)
        {
            if (!_cells.Contains(cell)) return false;

            // 配置里没有这一行就不算转换。
            if (next != TileStateType.Normal && !_specs.ContainsKey(next)) return false;

            if (!_machines.TryGetValue(cell, out TileStateMachine machine))
            {
                // 常规→常规不建状态机。
                if (next == TileStateType.Normal) return false;

                machine = new TileStateMachine();

                RegisterStateFactories(machine);
            }

            bool changed = machine.SwitchTo(next, BuildContext(cell));

            if (!changed)
            {
                // 空状态机要摘掉。
                if (machine.Current == null) _machines.Remove(cell);

                return false;
            }

            if (machine.CurrentId == TileStateType.Normal) _machines.Remove(cell);
            else _machines[cell] = machine;

            // D9：切状态时把该状态元素四件刷到格子上作初值。
            if (_element != null && _specs.TryGetValue(next, out TileStateSpec spec))
            {
                _element.FlushStateElement(cell, in spec);
            }

            EventBus<TileStateChanged>.Publish(new TileStateChanged(cell, next));

            if (applyEnterImpact) ApplyEnterImpact(cell, next, enterEffects);

            return true;
        }

        /// <summary>提交进格效果：默认取状态自带清单，反应命中时传规则行清单</summary>
        private void ApplyEnterImpact(Vector3Int cell, TileStateType state, IReadOnlyList<TileEffectValue> enterEffects)
        {
            IReadOnlyList<TileEffectValue> effects = enterEffects;

            if (effects == null && _specs.TryGetValue(state, out TileStateSpec spec)) effects = spec.EnterEffects;

            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                TileEffectValue effect = effects[i];

                if (effect.Kind == TileEffectKind.None) continue;

                Apply(cell, in effect);
            }
        }

        private void DrainPendingTransitions()
        {
            if (_pending.Count == 0) return;

            // 先搬走再处理：处理中发起的新请求留到下一帧，连锁有确定上界。
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

            // 只有配置里出现过的 ID 才注册工厂。
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
