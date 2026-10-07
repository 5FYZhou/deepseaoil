using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Element;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>格子系统的逻辑门面：持有格子状态、调度 Tick、处理落地与状态转换、对格上目标结算效果。</summary>
    /// <remarks>
    /// ① 球不伤害敌人：落地只把落点格切成反应结果的状态，伤害全部由格子产生（<see cref="Apply"/>），来源是状态的配置行。
    /// ② <b>同帧递归不存在</b>：Tick 请求进双缓冲队列（<b>本帧提交、下帧消费</b>），状态转换进待处理表（本帧 Tick 循环跑完后统一结算）。谁调它：组合根每渲染帧调一次 <see cref="Tick"/>（<c>dt</c> 用 <c>Time.deltaTime</c> ⇒ <c>timeScale = 0</c> 时格子整体冻结）。
    /// ③ <b>它不知道 Tilemap、不知道物理、不知道 Time，也不知道 <c>ConfigModule</c></b>：表数据（状态 / 规则 / 效果）全部由构造注入，元素合成与规则匹配在元素层（<see cref="IElementReactor"/>）——
    /// 于是 EditMode 里喂参数就能直测格子行为，而"反应怎么算"不会腐化成本类内部的一大段判定（§13 D8）。
    /// </remarks>
    public sealed class GridLogic : ITileScheduler, ITileResolver
    {
        /// <summary>击退衰减率（1/秒）：<c>EnemySpec.KnockbackDecay</c> 的实现值，只用于"格 → 冲量"的换算。</summary>
        private const float KnockbackDecayPerSecond = 10f;

        public GridGeometry Geometry => _geometry;

        public EnemyCellRegistry Registry => _registry;

        /// <summary>当前有状态的格数（诊断用；<c>0</c> = 场上没有泥浆之类的东西）。</summary>
        public int ActiveStateCount => _machines.Count;

        public int CellCount => _cells.Count;

        private readonly GridGeometry _geometry;
        private readonly Dictionary<TileStateType, TileStateSpec> _specs = new();
        private readonly Dictionary<TileEffectType, TileEffectSpec> _effectSpecs = new();
        private readonly Func<TileStateType, ITileState> _stateFactory;
        private readonly EnemyCellRegistry _registry;
        private readonly IElementReactor _element;

        /// <summary>合法格：只有登记过的格能被转换、被结算。来源是表现层从 Tilemap 枚举出的地板。</summary>
        private readonly HashSet<Vector3Int> _cells = new();

        private readonly Dictionary<Vector3Int, TileStateMachine> _machines = new();

        private readonly TileTickQueue _queue = new();

        /// <summary>待生效的状态转换（本帧提交、本帧 Tick 循环之后统一结算，同格后者胜）。</summary>
        private readonly Dictionary<Vector3Int, TileStateType> _pending = new();
        private readonly List<KeyValuePair<Vector3Int, TileStateType>> _pendingScratch = new();

        /// <summary>结算时的目标快照缓冲：受害方可能在结算里死亡并注销自己。类型是 <see cref="IEffectTarget"/>（不只是"能受伤的人"）。</summary>
        private readonly List<IEffectTarget> _dealScratch = new();

        private float _now;
        private float _deltaTime;

        /// <param name="geometry">格子几何。</param>
        /// <param name="stateSpecs">全部地块状态（<c>tile_state</c> 表的包装件）。</param>
        /// <param name="stateFactory">按 ID 造状态实例；返回 <c>null</c> = "这个 ID 没有实现"。</param>
        /// <param name="element">元素层（合成 ＋ 规则匹配 ＋ 每格元素）；为 <c>null</c> 时落地不产生任何反应。</param>
        /// <param name="TileEffectValues">地块效果表（<c>tile_effect</c> 的包装件）。用途只有一处：连续伤害的<b>扣血节奏</b>与伤害值以本表为准，让"调 DoT 数值"不必重导状态表。</param>
        /// <param name="registry">格上目标归属表；为 <c>null</c> 时自建一份（单测直接读 <see cref="Registry"/>）。</param>
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

        /// <summary>登记一个合法格；<b>同时按该格"原本是什么地"从表里灌一次元素</b>。</summary>
        /// <remarks>
        /// 这是 D9 的另一半：D9 只说"切进某状态时把状态的元素刷到格子上"，于是<b>常规格从来没人刷过</b> —— 它的元素恒为空（全零）。
        /// 后果不是"少一点味道"，而是<b>整条反应链判据不同</b>：水球（<c>tags=无</c>）砸空地（<c>tags=无</c>）合成出来仍是"无标签"，
        /// 命中的是"基础水地块"那条规则而不是"湿土 → 泥浆"那条 —— 而基础水地块没绑贴图，表现就是"投了球，地上什么都没变"。
        /// 把地面认成土（表里 <c>空地</c> 与 <c>基础土地块</c> 本来就都是"地"，只是后者的 <c>tags</c> 有值）才是表的本意。
        /// <para>初值来自 <c>tile_state</c> 的行，所以改表就能改"这片地是什么脾性"，代码里没有写死的标签。</para>
        /// <para>默认状态由关卡初始数据给（<see cref="LoadInitialStates"/>）：先登记后灌初始是正常顺序，所以登记时先只记下"这片地原本是什么"。<b>两个入口都登记时后到者优先</b>。</para>
        /// </remarks>
        public void RegisterCell(Vector3Int cell, TileStateType initial = TileStateType.Normal)
        {
            if (!_cells.Add(cell)) return;

            SeedElement(cell, initial);
        }

        /// <summary>灌一次"这一格原本的元素"。<b>只在这一格还没有状态机时做</b>（有状态机说明它已经有状态，元素由那次切换刷过了）。</summary>
        private void SeedElement(Vector3Int cell, TileStateType initial)
        {
            if (_element == null) return;

            if (_machines.ContainsKey(cell)) return;

            if (!_specs.TryGetValue(initial, out TileStateSpec spec)) return;

            _element.FlushStateElement(cell, in spec);
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
        /// <remarks>初始状态同样走一遍 D9（把状态的元素四件刷到格子上）—— 开局就是火池的格子，其元素必须在第一次反应之前就位。</remarks>
        public int LoadInitialStates(IReadOnlyList<TileInitial> states)
        {
            if (states == null) return 0;

            int applied = 0;

            for (int i = 0; i < states.Count; i++)
            {
                TileInitial state = states[i];

                var cell = new Vector3Int(state.CellX, state.CellY, 0);

                if (!_cells.Contains(cell)) continue;

                // 这一格"原本是什么地"由关卡数据说了算：先登记默认状态，元素初值才会从这一行取（先登记后灌的格会在这里补种）。
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

        /// <summary>一次球落地：合成球元素与该格地形元素 → 查反应规则 → 切到结果状态并提交效果清单。</summary>
        /// <param name="cell">落点格。</param>
        /// <param name="ballElement">球的元素。</param>
        /// <returns>是否真的发生了一次状态转换；落在地板外、规则不给状态（<c>None</c>）、或结果就是当前状态时为 <c>false</c>。</returns>
        /// <remarks><b>没切状态时把元素改动收回去</b>：不这么做，规则表里那些兜底行（结果 <c>None</c>）会把合成结果留在格子上 —— "砸了没反应"却留下温度，下一颗球凭空吃到它。
        /// 上游把合成结果当局部变量，没有这个问题；本侧按 D9 把元素落在格子上，就必须自己收尾。</remarks>
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
        /// <remarks>按目标的能力分流：能受伤的吃伤害、能减速的吃减速、能击退的吃击退、能被麻痹的吃麻痹。
        /// <b>玩家不在归属表里</b>，所以"泥浆会减速玩家"这条行为不存在（D7）。</remarks>
        public void Apply(Vector3Int cell, in TileEffectValue effect)
        {
            switch (effect.Kind)
            {
                case TileEffectKind.InstantDamage:
                    Deal(cell, effect.Amount);
                    return;

                case TileEffectKind.DamageOverTime:
                    // 数值与节奏以 tile_effect 表为准（表是权威）；取不到就退回效果的定值。
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

                // Slide：本轮未实现（需要"让敌人滑行"的能力接口，§10 的接口上限里没有它）。
                // None / Skid / Block / Fixed：§6 本轮不做。
                default:
                    return;
            }
        }

        /// <inheritdoc />
        /// <remarks>只改 <paramref name="cell"/> 自身（D4：状态实现不许碰别的格，跨格由执行者做）。温湿度继承与清除植物都走这里。</remarks>
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

                    // 只清本格：表里的"范围"是十字，但跨格改写需要一条"对邻居下命令"的通道，那是另一件事（D4）。
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

        /// <inheritdoc />
        public void SetCellElement(Vector3Int cell, in ElementValue element)
        {
            _element?.SetElement(cell, in element);
        }

        /// <summary>续一次减速修饰；<b>不做快照</b>（施加修饰不会让目标死亡或注销自己）。离开泥浆 ⇒ 不再续命 ⇒ 修饰自然过期；暂停 ⇒ 格子不 Tick ⇒ 恢复后立刻续上。</summary>
        private void ApplySlow(Vector3Int cell, float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ISlowable target) target.ApplySlow(speedScale, seconds);
            }
        }

        /// <summary>按"格数"击退：冲量 = 格数 × 格边长 × 击退衰减率。</summary>
        /// <remarks>表里的 <c>value1</c> 单位是<b>格</b>，而 <c>IKnockBackable.ApplyKnockback</c> 收的是速度（单位/秒）：
        /// 衰减是指数的，总位移 ≈ 冲量 / 衰减率，所以把衰减率乘回去才换算成"最终真的退了几格"。换算放在执行者，能力接口那边只认速度。</remarks>
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

                // null 必须先判：已销毁的 MonoBehaviour 一碰 transform 就抛。
                if (target == null) continue;

                if (!target.IsAlive) continue;

                if (target is not IKnockBackable knockbackable) continue;

                Vector2 delta = target.Position - center;

                // 正好站在格心时方向为零：与 Damage.At 同一条纪律，给"上"，不让它退不动。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                knockbackable.ApplyKnockback(direction * magnitude);
            }
        }

        /// <summary>麻痹：把时长交给目标，进不进移动门禁由目标自己决定（本轮只到"提交"这一层，见报告的待决问题）。</summary>
        private void ApplyStun(Vector3Int cell, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is IStunnable target) target.ApplyStun(seconds);
            }
        }

        /// <summary>对格上目标结算一次伤害（方向按"格心 → 受害者"各算一份）。</summary>
        private void Deal(Vector3Int cell, float amount)
        {
            if (amount <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            // 快照：受害方可能在 TakeDamage 里死亡并把自己从登记表摘掉，直接在原列表上遍历会改到正在遍历的集合。
            _dealScratch.Clear();
            _dealScratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);

            for (int i = 0; i < _dealScratch.Count; i++)
            {
                IEffectTarget target = _dealScratch[i];

                if (target == null) continue;

                // 已死但还没被销毁的目标不该被重复结算；生命体征经 IEffectTarget 继承来的 IAlivable 问。
                if (!target.IsAlive) continue;

                // 不能受伤的目标（将来的可推动物 / 纯装饰物）照样能被减速与击退，只是不吃伤害。
                if (target is not IDamageable damageable) continue;

                damageable.TakeDamage(Damage.At(center, target.Position, amount, DamageSource.Tile));
            }
        }

        /// <summary>切换某格的状态；同状态时是 no-op（不重入、不重置计时）。"切换"与"冲击"分两个参数：球落地与状态自己发起的转换都是"切换 + 给冲击"，开局加载是"只切换、绝不给冲击"。</summary>
        /// <param name="cell">目标格。</param>
        /// <param name="next">目标状态。</param>
        /// <param name="applyEnterImpact">是否提交这一次进格的效果清单。</param>
        /// <param name="enterEffects">本次转换要提交的效果清单；<c>null</c> = 用状态自带的（<c>spec.EnterEffects</c>）。</param>
        public bool SwitchState(
            Vector3Int cell,
            TileStateType next,
            bool applyEnterImpact,
            IReadOnlyList<TileEffectValue> enterEffects = null)
        {
            if (!_cells.Contains(cell)) return false;

            // 配置里没有这一行：不算一次转换。否则"转换"会凭空发生一次（一条事件 + 一次冲击），而场上什么都没变。
            if (next != TileStateType.Normal && !_specs.ContainsKey(next)) return false;

            if (!_machines.TryGetValue(cell, out TileStateMachine machine))
            {
                // 常规 → 常规：什么都没发生。**不建状态机**，否则"土球砸地板"每一下都留一份垃圾。
                if (next == TileStateType.Normal) return false;

                machine = new TileStateMachine();

                RegisterStateFactories(machine);
            }

            bool changed = machine.SwitchTo(next, BuildContext(cell));

            if (!changed)
            {
                // 空状态机要摘掉：它要么是刚建出来、要么是切换途中失败的产物，留着只会让 StateOf 读作常规、
                // 而 _machines 里多一份查不出用途的条目（诊断时的"这格到底有没有状态"就再也说不准了）。
                if (machine.Current == null) _machines.Remove(cell);

                return false;
            }

            // 落回常规的格不再保留状态机：常规格零常驻。
            if (machine.CurrentId == TileStateType.Normal) _machines.Remove(cell);
            else _machines[cell] = machine;

            // D9：切进某状态时把该状态的元素四件刷到格子上作初值（落回常规 = 摘掉这一格的元素记录）。
            if (_element != null && _specs.TryGetValue(next, out TileStateSpec spec))
            {
                _element.FlushStateElement(cell, in spec);
            }

            EventBus<TileStateChanged>.Publish(new TileStateChanged(cell, next));

            if (applyEnterImpact) ApplyEnterImpact(cell, next, enterEffects);

            return true;
        }

        /// <summary>提交"进格效果"：默认取状态自带的清单；反应命中时传入的是<b>规则行</b>的清单。</summary>
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
