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
    /// <summary>格子系统门面：持有状态与调度</summary>
    /// <remarks>伤害全由格子产生（Apply）。Tick 请求双缓冲：本帧提交、下帧消费；状态转换进待处理表、Tick 后结算。</remarks>
    public sealed class GridLogic : ITileScheduler, ITileResolver
    {
        public GridGeometry Geometry => _geometry;

        public EnemyCellRegistry Registry => _registry;

        /// <summary>净空图：场景碰撞体的距离场，由表现层烘焙；null = 没烘焙</summary>
        public NavMap Nav { get; private set; }

        public NavFieldSet NavFields => _navFields;

        public int ActiveStateCount => _machines.Count;

        public int CellCount => _cells.Count;

        private readonly GridGeometry _geometry;
        private readonly Dictionary<TileStateType, TileStateSpec> _specs = new();
        private readonly Func<TileStateType, ITileState> _stateFactory;
        private readonly EnemyCellRegistry _registry;

        /// <summary>二级元素反应查询表；null 时不发生二级反应</summary>
        private readonly DuoReactionCatalog _duo;

        /// <summary>合法格；表现层登记，未登记不参与转换</summary>
        private readonly HashSet<Vector3Int> _cells = new();

        /// <summary>导航流场集合；目标格一变、阻挡一变就整体作废</summary>
        private readonly NavFieldSet _navFields = new();

        private readonly Dictionary<Vector3Int, TileStateMachine> _machines = new();

        private readonly TileTickQueue _queue = new();

        /// <summary>待生效转换，Tick 后结算，后者胜</summary>
        private readonly Dictionary<Vector3Int, TileStateType> _pending = new();
        private readonly List<KeyValuePair<Vector3Int, TileStateType>> _pendingScratch = new();

        /// <summary>目标快照：受害方可能在结算中死亡注销</summary>
        private readonly List<IEffectTarget> _dealScratch = new();

        /// <summary>径向冲击格缓冲，复用避免热路径分配</summary>
        private readonly List<Vector3Int> _blastCells = new();

        /// <summary>铺地貌格缓冲，不复用 _blastCells</summary>
        private readonly List<Vector3Int> _spreadCells = new();

        private readonly List<Vector3Int> _crossCells = new(4);

        private readonly List<Vector3Int> _restoreCells = new();

        private float _now;
        private float _deltaTime;

        /// <remarks>stateFactory 返回 null=未实现；duo=null 无二级反应；registry=null 自建。数值不在此注入：tile_state / element_rule 已是最终值。</remarks>
        public GridLogic(
            in GridGeometry geometry,
            IReadOnlyList<TileStateSpec> stateSpecs,
            Func<TileStateType, ITileState> stateFactory,
            DuoReactionCatalog duo = null,
            EnemyCellRegistry registry = null)
        {
            _geometry = geometry;
            _stateFactory = stateFactory;
            _duo = duo;
            _registry = registry ?? new EnemyCellRegistry();

            if (stateSpecs != null)
            {
                for (int i = 0; i < stateSpecs.Count; i++)
                {
                    _specs[stateSpecs[i].Id] = stateSpecs[i];
                }
            }
        }

        /// <summary>登记为合法格；地貌由 SwitchTileState 改</summary>
        /// <remarks>新增合法格会改变流场的可走集合，故登记即作废旧场</remarks>
        public void RegisterCell(Vector3Int cell)
        {
            if (!_cells.Add(cell)) return;

            _navFields.MarkDirty();
        }

        public bool HasCell(Vector3Int cell)
        {
            return _cells.Contains(cell);
        }

        /// <summary>已登记格的枚举口；供表现层一次性烘焙场景碰撞体</summary>
        public IEnumerable<Vector3Int> Cells => _cells;

        /// <summary>装入烘焙好的净空图；换图即作废全部流场</summary>
        public void SetNavMap(NavMap map)
        {
            Nav = map;

            _navFields.MarkDirty();
        }

        /// <summary>喂本帧的目标格与皮厚；由组合根每物理帧调一次，未变则不作废</summary>
        /// <remarks>只作废不重建：真正重建发生在 NavFieldFor 或紧接的 EnsureNavFields，成本出现在调用点而不是散在每只怪里。</remarks>
        public void RefreshNavFields(Vector3Int goalCell, float skin)
        {
            _navFields.SetGoal(goalCell, skin);

            _navFields.RebuildStale(this, Nav);
        }

        /// <summary>取该体半径的流场；该半径首次出现时建一张（每种怪一次）</summary>
        public NavField NavFieldFor(float radius, float skin)
        {
            return _navFields.Get(radius, skin, this, Nav);
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

        /// <summary>该格是否作为网格连锁的导通体；无表行返回 false</summary>
        public bool IsConductor(Vector3Int cell)
        {
            TileStateType state = StateOf(cell);

            return _specs.TryGetValue(state, out TileStateSpec spec) && spec.IsConductor;
        }

        /// <summary>该格是否物理阻挡地貌：只看 tile_state.is_obstacle，无表行返回 false</summary>
        /// <remarks>静态碰撞体不进这里（它们进 NavMap 的净空），否则绕障会把"贴墙但能过"的格也封死</remarks>
        public bool IsObstacle(Vector3Int cell)
        {
            TileStateType state = StateOf(cell);

            return _specs.TryGetValue(state, out TileStateSpec spec) && spec.IsObstacle;
        }

        /// <summary>该格地貌的每秒伤害（dot_damage）；无表行返回 0</summary>
        public int DotDamageOf(Vector3Int cell)
        {
            TileStateType state = StateOf(cell);

            return _specs.TryGetValue(state, out TileStateSpec spec) ? spec.DotDamage : 0;
        }

        /// <summary>该格地貌的麻痹秒数（touch_stun）；无表行返回 0</summary>
        public float TouchStunOf(Vector3Int cell)
        {
            TileStateType state = StateOf(cell);

            return _specs.TryGetValue(state, out TileStateSpec spec) ? spec.TouchStun : 0f;
        }

        /// <summary>灌入关卡初始状态，不产生伤害</summary>
        public int LoadInitialStates(IReadOnlyList<TileInitial> states)
        {
            if (states == null) return 0;

            int applied = 0;

            for (int i = 0; i < states.Count; i++)
            {
                TileInitial state = states[i];

                var cell = new Vector3Int(state.CellX, state.CellY, 0);

                if (!_cells.Contains(cell)) continue;

                if (state.StateId == TileStateType.Normal) continue;

                if (SwitchTileState(cell, state.StateId, 0f)) applied++;
            }

            return applied;
        }

        /// <summary>导出非空地地貌与剩余秒数，-1 = 无倒计时</summary>
        public List<TileSaveData> ExportTiles()
        {
            var result = new List<TileSaveData>(_machines.Count);

            foreach (KeyValuePair<Vector3Int, TileStateMachine> pair in _machines)
            {
                ITileState state = pair.Value.Current;

                if (state == null) continue;

                float remaining = state.RemainingDuration;

                // 本帧到期的格不存：回填会变回满时长
                if (remaining == 0f) continue;

                result.Add(new TileSaveData
                {
                    cellX = pair.Key.x,
                    cellY = pair.Key.y,
                    stateId = (int)state.Id,
                    remaining = remaining
                });
            }

            return result;
        }

        /// <summary>覆盖重建地貌；先全清回空地再落档</summary>
        public void RestoreTiles(List<TileSaveData> list)
        {
            _restoreCells.Clear();

            foreach (Vector3Int cell in _machines.Keys) _restoreCells.Add(cell);

            for (int i = 0; i < _restoreCells.Count; i++)
            {
                SwitchTileState(_restoreCells[i], TileStateType.Normal);
            }

            _restoreCells.Clear();

            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                TileSaveData data = list[i];

                if (data == null) continue;

                var cell = new Vector3Int(data.cellX, data.cellY, 0);

                var next = (TileStateType)data.stateId;

                if (next == TileStateType.Normal) continue;

                SwitchTileState(cell, next, data.remaining > 0f ? data.remaining : 0f);
            }
        }

        public void Tick(float now, float deltaTime)
        {
            _now = now;
            _deltaTime = deltaTime;

            // 火海蔓延前沿先推：它的时间基准与本节拍一致
            TileChainReactor.Tick(this, now, deltaTime);

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

        /// <summary>球落地：纯函数裁决 → 原子提交地貌 → 当帧结算冲击 → 按需触发连锁</summary>
        /// <remarks>返回 false：地板外、或地貌与冲击都没发生的空过。首跳伤害在此当帧打出，绝不等待下一次 Tick。</remarks>
        public bool OnBallHit(Vector3Int cell, BallType ball)
        {
            if (!_cells.Contains(cell)) return false;

            TileStateType current = StateOf(cell);

            // 二级先判且用未改写状态：一级先改格子，配对（如植物×火）就凑不齐
            bool duoTriggered = TileChainReactor.TriggerDuo(this, cell, current, _duo);

            ReactionOutcome outcome = ReactionResolver.Resolve(current, ball);

            // 二级已质变地貌时跳过一级覆盖，否则火海会被反手顶掉
            bool changed = !duoTriggered
                && outcome.NextTile != current
                && SwitchTileState(cell, outcome.NextTile, 0f);

            bool impacted = ApplyImpact(cell, in outcome);

            if (current == TileStateType.BasicFlower && ball == BallType.Water)
            {
                GrowCrossGrass(cell);
            }

            if (changed || impacted)
            {
                ScheduleTick(cell);

                return true;
            }

            return false;
        }

        private void GrowCrossGrass(Vector3Int center)
        {
            GridQuery.GetNeighbors4(center, _crossCells);

            for (int i = 0; i < _crossCells.Count; i++)
            {
                Vector3Int neighbor = _crossCells[i];

                if (_cells.Contains(neighbor) && StateOf(neighbor) == TileStateType.Normal)
                {
                    SwitchTileState(neighbor, TileStateType.BasicPlant, 0f);
                }
            }
        }

        /// <summary>落地冲击的唯一出口：单格与广域 AoE</summary>
        public bool ApplyImpact(Vector3Int cell, in ReactionOutcome outcome)
        {
            bool any = false;

            if (outcome.ImpactRadius > 1.0f)
            {
                if (outcome.InstantDamage > 0 || outcome.KnockbackCells > 0f)
                {
                    ApplyBlast(cell, outcome.ImpactRadius, outcome.InstantDamage, outcome.KnockbackCells);
                    any = true;
                }

                if (outcome.StunSeconds > 0f)
                {
                    ApplyStunCell(cell, outcome.StunSeconds);
                    any = true;
                }
            }
            else // 单格判定：<= 1.0 格走原版精准单格
            {
                if (outcome.InstantDamage > 0 || outcome.KnockbackCells > 0f)
                {
                    DealCell(cell, outcome.InstantDamage, outcome.KnockbackCells);
                    any = true;
                }

                if (outcome.StunSeconds > 0f)
                {
                    ApplyStunCell(cell, outcome.StunSeconds);
                    any = true;
                }

                if (outcome.NextTile == TileStateType.Steam)
                {
                    EventBus<GridBlastOccurred>.Publish(new GridBlastOccurred(_geometry.CellCenter(cell), _geometry.CellSize, outcome.NextTile));
                }
            }

            if (outcome.TriggerChain)
            {
                ScheduleTick(cell);
                TileChainReactor.Trigger(this, cell, outcome.NextTile);
                any = true;
            }

            return any;
        }

        public void ScheduleTick(Vector3Int cell)
        {
            _queue.Schedule(cell);
        }

        public void Transition(Vector3Int cell, TileStateType next)
        {
            _pending[cell] = next;
        }

        /// <summary>对格上目标造成一次伤害；不带击退</summary>
        public void DealCell(Vector3Int cell, float amount)
        {
            DealCell(cell, amount, 0f);
        }

        /// <summary>对格上目标结算一次：元素默认按该格当前状态定，强制定色由 element 覆盖</summary>
        public void DealCell(Vector3Int cell, float amount, float knockbackCells, ElementKind element = ElementKind.Physical, bool forceElement = false)
        {
            if (amount <= 0f && knockbackCells <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            _dealScratch.Clear();
            _dealScratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);

            ElementKind resolved = forceElement ? element : Damage.ElementFromTile(StateOf(cell));

            for (int i = 0; i < _dealScratch.Count; i++)
            {
                DealTarget(_dealScratch[i], center, amount, knockbackCells, resolved);
            }
        }

        /// <summary>对单个目标结算一次：伤害与击退都只走这一份 Damage</summary>
        private static void DealTarget(IEffectTarget target, Vector2 center, float amount, float knockbackCells, ElementKind element)
        {
            if (target == null || !target.IsAlive) return;

            Damage damage = Damage.At(center, target.Position, amount, DamageSource.Tile, knockbackCells, element);

            // 格层只管算格数与方向：击退位移要由受击者按自身阻尼折算成初速度
            if (target is IDamageable damageable) damageable.TakeDamage(in damage);
        }

        /// <summary>对格上目标续一次减速；状态实现按帧调，seconds 是本帧窗口</summary>
        /// 续减速不做快照，离开泥浆自然过期；替换成一次性大窗口会短于物理帧被扣穿
        public void ApplySlowCell(Vector3Int cell, float speedScale, float seconds)
        {
            if (speedScale >= 1f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ISlowable target) target.ApplySlow(speedScale, seconds);
            }
        }

        /// <summary>对格上目标施麻痹</summary>
        public void ApplyStunCell(Vector3Int cell, float seconds)
        {
            if (seconds <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is IStunnable target) target.ApplyStun(seconds);
            }
        }

        /// <summary>连锁电击：伤害 + 麻痹一次结算，供 TileChainReactor 逐格调</summary>
        public void ApplyInstantShock(Vector3Int cell, int damage, float stunSeconds)
        {
            // 连锁电击不看格状态：导电源也可能是水或荆棘，元素恒为电
            DealCell(cell, damage, 0f, ElementKind.Electric, forceElement: true);

            ApplyStunCell(cell, stunSeconds);

            bool isFlame = stunSeconds <= 0f;

            EventBus<GridPointVfxOccurred>.Publish(new GridPointVfxOccurred(_geometry.CellCenter(cell), isFlame));
        }

        /// <summary>径向范围冲击：半径内每格结算一次伤害与击退，用于二级反应的波及</summary>
        /// <remarks>半径按格心欧氏距离判、含中心格；一次反应同格只结算一次。</remarks>
        public void ApplyBlast(Vector3Int center, float radiusCells, int damage, float knockbackCells)
        {
            if (radiusCells < 0f) radiusCells = 0f;

            int reach = Mathf.CeilToInt(radiusCells);
            float radiusSqr = radiusCells * radiusCells;

            Vector2 centerPoint = _geometry.CellCenter(center);

            _blastCells.Clear();

            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var cell = new Vector3Int(center.x + dx, center.y + dy, center.z);

                    if (!_cells.Contains(cell)) continue;

                    if (dx != 0 || dy != 0)
                    {
                        Vector2 delta = _geometry.CellCenter(cell) - centerPoint;

                        if (delta.sqrMagnitude > radiusSqr) continue;
                    }

                    _blastCells.Add(cell);
                }
            }

            for (int i = 0; i < _blastCells.Count; i++)
            {
                DealCell(_blastCells[i], damage, knockbackCells);
            }

            // 二级反应激发广域冲击时广播事实
            TileStateType centerState = StateOf(center);
            EventBus<GridBlastOccurred>.Publish(new GridBlastOccurred(centerPoint, radiusCells * _geometry.CellSize, centerState));
        }

        /// <summary>半径内铺 resultTile；永久地貌跳过（Normal 例外），保护玩家铺的水/土</summary>
        public void SpreadTileInRadius(Vector3Int center, float radiusCells, TileStateType resultTile, float durationOverride = 0f)
        {
            if (radiusCells < 0f) radiusCells = 0f;

            int reach = Mathf.CeilToInt(radiusCells);
            float radiusSqr = radiusCells * radiusCells;

            Vector2 centerPoint = _geometry.CellCenter(center);

            _spreadCells.Clear();

            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var cell = new Vector3Int(center.x + dx, center.y + dy, center.z);

                    if (!_cells.Contains(cell)) continue;

                    if (dx != 0 || dy != 0)
                    {
                        Vector2 delta = _geometry.CellCenter(cell) - centerPoint;

                        if (delta.sqrMagnitude > radiusSqr) continue;
                    }

                    _spreadCells.Add(cell);
                }
            }

            for (int i = 0; i < _spreadCells.Count; i++)
            {
                Vector3Int cell = _spreadCells[i];

                TileStateType state = StateOf(cell);

                if (state == resultTile) continue;

                if (state != TileStateType.Normal
                    && _specs.TryGetValue(state, out TileStateSpec spec)
                    && spec.Duration <= 0f) continue;

                SwitchTileState(cell, resultTile, durationOverride);
            }
        }

        /// <summary>切换某格状态；同状态 no-op，未登记的格与无表行的状态一律拒绝</summary>
        /// <remarks>durationOverride &gt; 0 覆盖表里的 duration（二级反应结果存续用它）；贴图由 EventBus 订阅者刷。</remarks>
        public bool SwitchTileState(Vector3Int cell, TileStateType next, float durationOverride = 0f)
        {
            if (!_cells.Contains(cell)) return false;

            if (next != TileStateType.Normal && !_specs.ContainsKey(next)) return false;

            // 地貌翻转会改变导航可走集合，且只有这一处能看见"翻转"这件事
            bool wasObstacle = IsObstacle(cell);

            if (!_machines.TryGetValue(cell, out TileStateMachine machine))
            {
                if (next == TileStateType.Normal) return false;

                machine = new TileStateMachine();

                RegisterStateFactories(machine);
            }

            TileStateType previous = machine.CurrentId;
            bool changed = machine.SwitchTo(next, BuildContext(cell), durationOverride);

            if (!changed)
            {
                if (machine.Current == null) _machines.Remove(cell);

                return false;
            }

            if (machine.CurrentId == TileStateType.Normal) _machines.Remove(cell);
            else _machines[cell] = machine;

            EventBus<TileStateChanged>.Publish(new TileStateChanged(cell, previous, next));

            if (IsObstacle(cell) != wasObstacle) _navFields.MarkDirty();

            return true;
        }

        /// <summary>取某状态的包装件，诊断用</summary>
        public bool TryGetSpec(TileStateType state, out TileStateSpec spec)
        {
            return _specs.TryGetValue(state, out spec);
        }

        /// <summary>目标跨格进入：只给该目标补"进格一下"</summary>
        /// <remarks>进格一次性代价＝伤害＋麻痹，持续伤害由状态的 DoT 承担；同格不再进不重复结算。</remarks>
        public void OnActorEnterCell(Vector3Int cell, IEffectTarget target)
        {
            if (target == null || target.IsAlive == false) return;

            if (!_cells.Contains(cell)) return;

            if (!_specs.TryGetValue(StateOf(cell), out TileStateSpec spec)) return;

            // 麻痹不吃 DotDamage 的提前返回：dot_damage 可为 0 也要麻
            if (spec.TouchStun > 0f) ApplyStunCell(cell, spec.TouchStun);

            if (spec.DotDamage <= 0) return;

            // DoT 的首跳放在这：走进去那一下要立刻痛，不能等满一秒
            DealTarget(target, _geometry.CellCenter(cell), spec.DotDamage, 0f, Damage.ElementFromTile(StateOf(cell)));
        }

        private void DrainPendingTransitions()
        {
            if (_pending.Count == 0) return;

            // 先搬走再处理：处理中的新请求留到下一帧，连锁有上界。
            _pendingScratch.Clear();

            foreach (KeyValuePair<Vector3Int, TileStateType> pair in _pending)
            {
                _pendingScratch.Add(pair);
            }

            _pending.Clear();

            for (int i = 0; i < _pendingScratch.Count; i++)
            {
                KeyValuePair<Vector3Int, TileStateType> pair = _pendingScratch[i];

                SwitchTileState(pair.Key, pair.Value);
            }

            _pendingScratch.Clear();
        }

        private void RegisterStateFactories(TileStateMachine machine)
        {
            if (_stateFactory == null) return;

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
