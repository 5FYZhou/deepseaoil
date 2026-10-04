using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 格子系统的逻辑门面：持有全部格子状态、调度 Tick、处理落地与状态转换、对格上目标结算伤害。
    /// </summary>
    /// <remarks>
    /// <b>三个不变量：</b>
    /// <list type="number">
    /// <item><b>球不伤害敌人。</b>落地只做一件事：把落点格切到该球种对应的状态。
    /// 伤害全部由格子产生（<see cref="Deal"/>），来源是状态的配置行。</item>
    /// <item><b>同帧递归不存在。</b>Tick 请求进双缓冲队列（下帧消费），状态转换进待处理表
    /// （本帧 Tick 循环跑完后统一结算）。</item>
    /// <item><b>常规格不占常驻内存。</b>只有非 <see cref="TileStateType.Normal"/> 的格才有状态机；
    /// 落回常规时整台机器被丢掉。</item>
    /// </list>
    /// <para><b>谁调它：</b>组合根每渲染帧调一次 <see cref="Tick"/>（<c>dt</c> 用 <c>Time.deltaTime</c>，
    /// 于是 <c>timeScale = 0</c> 时格子整体冻结）；落地与查询随时可调。</para>
    /// <para><b>它不知道 Tilemap、不知道物理、不知道 Time</b>：几何由组合根从 Tilemap 折算进来
    /// （见 <see cref="GridGeometry"/>），于是整套格子行为可以在 EditMode 里喂 dt 复现。</para>
    /// </remarks>
    public sealed class GridLogic : ITileScheduler, IDamageDealer
    {
        /// <summary>格子的几何（世界 ↔ 格）。</summary>
        public GridGeometry Geometry => _geometry;

        /// <summary>敌人归属表；由组合根与敌人共享（敌人登记自己，格子系统查人）。</summary>
        public EnemyCellRegistry Registry => _registry;

        /// <summary>当前有状态的格数（诊断用；正常状态下 0 表示场上没有泥浆之类的东西）。</summary>
        public int ActiveStateCount => _machines.Count;

        /// <summary>已登记的合法格数（诊断用）。</summary>
        public int CellCount => _cells.Count;

        private readonly GridGeometry _geometry;
        private readonly Dictionary<TileStateType, TileStateSpec> _specs = new();
        private readonly Func<TileStateType, ITileState> _stateFactory;
        private readonly EnemyCellRegistry _registry;

        /// <summary>合法格：只有登记过的格能被转换、被减速。来源是表现层从 Tilemap 枚举出的地板。</summary>
        private readonly HashSet<Vector3Int> _cells = new();

        /// <summary>有状态的格。</summary>
        private readonly Dictionary<Vector3Int, TileStateMachine> _machines = new();

        private readonly TileTickQueue _queue = new();

        /// <summary>待生效的状态转换（本帧提交、本帧 Tick 循环之后统一结算，同格后者胜）。</summary>
        private readonly Dictionary<Vector3Int, TileStateType> _pending = new();
        private readonly List<KeyValuePair<Vector3Int, TileStateType>> _pendingScratch = new();

        /// <summary>结算时的目标快照缓冲：受害方可能在结算里死亡并注销自己。</summary>
        private readonly List<IDamageable> _dealScratch = new();

        /// <summary>最近一次 <see cref="Tick"/> 的时间；状态回调里读到的是它。</summary>
        private float _now;
        private float _deltaTime;

        /// <param name="geometry">格子几何（由组合根从 Tilemap 折算）。</param>
        /// <param name="stateSpecs">全部状态配置行。</param>
        /// <param name="stateFactory">状态工厂：给 ID 造一个新实例；返回 <c>null</c> 表示该 ID 没有实现。</param>
        /// <param name="registry">敌人归属表；<c>null</c> 时自建一个。</param>
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

        // ─────────────────────────────────────────────
        // 格子集合
        // ─────────────────────────────────────────────

        /// <summary>登记一个合法格（表现层从 Tilemap 枚举地板后灌入）。幂等。</summary>
        public void RegisterCell(Vector3Int cell)
        {
            _cells.Add(cell);
        }

        /// <summary>本格是否合法（存在地板）。不合法时落地不产生任何效果。</summary>
        public bool HasCell(Vector3Int cell)
        {
            return _cells.Contains(cell);
        }

        /// <summary>世界坐标 → 格。</summary>
        public Vector3Int WorldToCell(Vector2 world)
        {
            return _geometry.WorldToCell(world);
        }

        /// <summary>本格当前状态；没有状态时是 <see cref="TileStateType.Normal"/>。</summary>
        public TileStateType StateOf(Vector3Int cell)
        {
            return _machines.TryGetValue(cell, out TileStateMachine machine)
                ? machine.CurrentId
                : TileStateType.Normal;
        }

        /// <summary>清空全部格子与状态（切场景）。</summary>
        public void Clear()
        {
            _cells.Clear();
            _machines.Clear();
            _queue.Clear();
            _pending.Clear();
            _registry.Clear();
        }

        // ─────────────────────────────────────────────
        // 加载 / 每帧
        // ─────────────────────────────────────────────

        /// <summary>
        /// 按关卡数据灌入初始状态。
        /// </summary>
        /// <remarks>
        /// <b>刻意不产生伤害</b>：开局就站在泥浆上的敌人不该凭空掉血 ——
        /// 伤害的语义是"状态<b>发生了转换</b>"，而加载是"本来就是这样"。
        /// <para>不合法（没有地板）的格会被忽略，不报错：关卡数据与场景不一致是常见的手工事故，
        /// 表现应该是"那片泥没出现"，而不是启动失败。</para>
        /// </remarks>
        public int LoadInitialStates(IReadOnlyList<TileInitialSpec> states)
        {
            if (states == null) return 0;

            int applied = 0;

            for (int i = 0; i < states.Count; i++)
            {
                TileInitialSpec state = states[i];

                if (state.State == TileStateType.Normal) continue;

                var cell = new Vector3Int(state.CellX, state.CellY, 0);

                if (!_cells.Contains(cell)) continue;

                if (SwitchState(cell, state.State, applyEnterImpact: false)) applied++;
            }

            return applied;
        }

        /// <summary>推进一个渲染帧。</summary>
        public void Tick(float now, float deltaTime)
        {
            _now = now;
            _deltaTime = deltaTime;

            // ① 翻页：本帧处理的是"上一帧提交的"请求。
            _queue.Swap();

            IReadOnlyList<Vector3Int> cells = _queue.Current;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];

                if (!_machines.TryGetValue(cell, out TileStateMachine machine)) continue;

                machine.Tick(BuildContext(cell));
            }

            // ② 本帧 Tick 循环跑完之后才结算状态转换：同帧递归因此不可能发生。
            DrainPendingTransitions();
        }

        // ─────────────────────────────────────────────
        // 落地
        // ─────────────────────────────────────────────

        /// <summary>
        /// 球落地：把落点格切到该球种对应的状态，并结算一次该状态的"进入冲击"。
        /// </summary>
        /// <param name="cell">落点格。</param>
        /// <param name="ball">球种配置行。</param>
        /// <returns>真的作用到了格子上为 <c>true</c>（落在地板外为 <c>false</c>）。</returns>
        /// <remarks>
        /// <b>为什么"已经是该状态"也要结算一次冲击：</b>白模的行为是"每一颗水球落地都对这一格的敌人
        /// 造成一次伤害"，与那一格是不是已经是泥浆无关。把冲击挂在"状态转换"上会静默改掉伤害节奏
        /// （打同一格的第一颗球有用、后面几颗全被丢掉），而"不重入状态机"保证了泥浆计时不被刷新 ——
        /// 与白模逐条一致：<b>伤害照给，计时不重置</b>。
        /// </remarks>
        public bool OnBallHit(Vector3Int cell, in BallSpec ball)
        {
            if (!_cells.Contains(cell)) return false;

            SwitchState(cell, ball.TileState, applyEnterImpact: false);
            ApplyEnterImpact(cell, ball.TileState);

            return true;
        }

        // ─────────────────────────────────────────────
        // 查询
        // ─────────────────────────────────────────────

        /// <summary>世界坐标处的速度系数（<c>1</c> = 不减速）。</summary>
        public float GetSlowMultiplier(Vector2 world)
        {
            if (!_geometry.IsValid) return 1f;

            return GetSlowMultiplierAt(_geometry.WorldToCell(world));
        }

        /// <summary>本格的速度系数（<c>1</c> = 不减速）。</summary>
        public float GetSlowMultiplierAt(Vector3Int cell)
        {
            return _machines.TryGetValue(cell, out TileStateMachine machine) ? machine.SlowMultiplier : 1f;
        }

        // ─────────────────────────────────────────────
        // ITileScheduler
        // ─────────────────────────────────────────────

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

        // ─────────────────────────────────────────────
        // IDamageDealer
        // ─────────────────────────────────────────────

        /// <inheritdoc />
        public void Deal(Vector3Int cell, float amount, float knockback, DamageSource source)
        {
            if (amount <= 0f && knockback <= 0f) return;

            if (!_registry.TryGetIn(cell, out List<IDamageable> targets) || targets.Count == 0) return;

            // 快照：受害方可能在 TakeDamage 里死亡并把自己从登记表摘掉，
            // 直接在原列表上遍历会改到正在遍历的集合。
            _dealScratch.Clear();
            _dealScratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);

            for (int i = 0; i < _dealScratch.Count; i++)
            {
                IDamageable target = _dealScratch[i];

                if (target == null || target.IsDead) continue;

                // IsDead 必须先判：已销毁的 MonoBehaviour 一碰 transform 就抛。
                target.TakeDamage(Damage.At(center, target.Position, amount, source, knockback));
            }
        }

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        /// <summary>
        /// 切换某格的状态。同状态时是 no-op（不重入、不重置计时）。
        /// </summary>
        /// <param name="cell">格子。</param>
        /// <param name="next">目标状态。</param>
        /// <param name="applyEnterImpact">是否结算目标状态的"进入冲击"。</param>
        /// <returns>真的发生了切换为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>切换与冲击是两件事，所以分成两个参数：</b>球的落地是"切换（可能没切）+ 每次都给冲击"，
        /// 而状态自己发起的转换（泥浆到期落回常规）是"切换 + 给冲击"，
        /// 开局加载是"只切换、绝不给冲击"。三种组合都在这里表达，不需要三份代码。
        /// </remarks>
        public bool SwitchState(Vector3Int cell, TileStateType next, bool applyEnterImpact)
        {
            if (!_cells.Contains(cell)) return false;

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

        /// <summary>把"进入某状态"的冲击（伤害 / 击退）结算给站在该格上的目标。</summary>
        private void ApplyEnterImpact(Vector3Int cell, TileStateType state)
        {
            if (!_specs.TryGetValue(state, out TileStateSpec spec)) return;

            if (spec.EnterDamage <= 0f && spec.EnterKnockback <= 0f) return;

            Deal(cell, spec.EnterDamage, spec.EnterKnockback, DamageSource.Tile);
        }

        private void DrainPendingTransitions()
        {
            if (_pending.Count == 0) return;

            // 先搬走再处理：处理过程中（事件订阅方 / 状态回调）发起的新请求留到下一帧，
            // 于是"一帧内能发生的连锁"有确定上界。
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
