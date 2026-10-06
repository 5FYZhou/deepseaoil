using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的逻辑层：持有速度账本 ＋ <b>状态效果层 ＋ 移动层</b>，与玩家同构；
    /// 唯一差别是"输入从哪来" —— 玩家来自 <c>InputBuffer</c>，敌人来自 <see cref="EnemyBrain"/>。
    /// </summary>
    /// <remarks>
    /// <b>它是敌人速度的唯一写者。</b>击退不是另外去写 <c>Rigidbody2D</c>，而是
    /// <see cref="ApplyKnockback"/> 往账本里挂一次冲量 —— 于是"每帧只有一个速度写者"
    /// 这条不变量在敌人身上也成立（玩家那侧由 <c>PlayerLogic</c> 保证）。
    /// <para><b>帧内顺序与玩家逐条对齐：</b>
    /// <code>
    /// ① 大脑决策（意图 = 方向 ＋ 速度）    // 玩家侧对应"读输入快照"
    /// ② StatusGroup.Tick(ctx, 挂起的击退)  // 产出本帧门禁
    /// ③ EnemyMoveGroup.Tick(ctx, 门禁)     // 唯一写速度
    /// </code>
    /// <b>决策放在 <c>Tick</c> 而不是 <c>OnTick</c> 里做</b>：意图必须先进入上下文
    /// （移动层的基础态判据就是它），而上下文在驱动之前组装一次。</para>
    /// <para><b>它不查世界</b>：目标位置由表现层的组合根每帧喂入（<see cref="SetTarget"/>）；
    /// 减速不再由组合根喂系数 —— 它由格状态提交、由状态效果层持有（见 <c>StatusGroup.ApplySlow</c>），
    /// 本类完全不需要知道"脚下是不是泥浆"。</para>
    /// </remarks>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly EnemySpec _enemy;
        private readonly EnemyBrain _brain;
        private readonly StatusGroup _status;
        private readonly EnemyMoveGroup _move;

        /// <summary>追击目标（玩家位置）。没有目标时不提交任何移动。</summary>
        private Vector2? _target;

        /// <summary>
        /// 已递交、但还没被状态效果层消费的击退冲量。
        /// </summary>
        /// <remarks>
        /// <b>为什么冲量必须"挂着"而不是当场写进账本：</b><c>ActorLogic.FixedTick</c> 在<b>帧首</b>
        /// 就把 <c>_delta</c> 清零了，而那一刻在 <c>OnTick</c> 之前。于是"在两次 Tick 之间调
        /// <c>AddImpulse</c>"会被下一次固定帧的开头<b>静默抹掉</b> —— 冲量从头到尾没到过执行器，
        /// 而账本看起来一切正常。
        /// <para>这不是测试才有的问题：格子结算在渲染帧、敌人 Tick 在物理帧，两者之间插着帧边界。
        /// 现象是"打中了但敌人纹丝不动"，且没有任何报错。</para>
        /// </remarks>
        private Vector2 _pendingKnockback;

        /// <summary>本帧大脑给出的意图（移动层的速度来源，也是调试与测试的读数）。</summary>
        private EnemyIntent _intent;

        /// <param name="motor">移动执行器。参数类型是<b>接口</b>而不是具体 MonoBehaviour：
        /// 逻辑层不该知道"实现挂在物体上"，而测试要能塞一个不碰引擎的探针进来。</param>
        /// <param name="spec">敌人的表值（追击 / 耐久）。</param>
        /// <param name="characterConfig">折算后的角色运动参数（由 <c>EnemyCharacterFactory</c> 给出）。</param>
        public EnemyLogic(IMovementMotor motor, in EnemySpec spec, CharacterConfig characterConfig)
            : base(motor, characterConfig)
        {
            _enemy = spec;
            _brain = new EnemyBrain(in spec);
            _status = new StatusGroup(this);
            _move = new EnemyMoveGroup(this);
        }

        /// <summary>本种类敌人数值（只读，供组合根取半径 / 闪烁频率等）。</summary>
        public EnemySpec Spec => _enemy;

        /// <summary>状态效果层（受击 / 平常）。</summary>
        public StatusGroup Status => _status;

        /// <summary>移动层（追击 / 站立）。</summary>
        public EnemyMoveGroup MoveGroup => _move;

        /// <summary>当前移动状态（与玩家同一套标签）。</summary>
        public MovementStateTag CurrentState => _move.Current;

        /// <summary>本帧的意图。</summary>
        public EnemyIntent Intent => _intent;

        /// <summary>是否正处于受击（视效用它决定闪不闪）。</summary>
        public bool IsHurt => _status.IsHurt;

        /// <summary>设置追击目标；传 <c>null</c> 表示"没有目标"（敌人随即滑停）。</summary>
        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        /// <summary>
        /// 施加一次击退冲量（速度的瞬时变化）。<b>可以调用在帧外</b> —— 它只挂起，不动账本。
        /// </summary>
        /// <remarks>同一帧内多次调用会累加（被两处同时结算就是两股冲量）。</remarks>
        public void ApplyKnockback(float impulse, Vector2 direction)
        {
            if (impulse <= 0f) return;

            _pendingKnockback += direction * impulse;
        }

        /// <summary>
        /// 推进一个物理帧。
        /// </summary>
        /// <param name="now">驱动方的时间（<c>Time.fixedTime</c>）。</param>
        /// <param name="deltaTime">驱动方的步长（<c>Time.fixedDeltaTime</c>）。</param>
        /// <remarks>
        /// <c>worldInfo</c> 给默认值即语义正确 —— 敌人有刚体，阻挡由物理解算，逻辑层不钳它。
        /// </remarks>
        public void Tick(float now, float deltaTime)
        {
            // ① 大脑先决策：意图要进上下文（移动层的基础态判据就是它）。
            //    方向直接进 inputSnapshot.Move —— 它是"本帧的移动指令"，量纲不影响消费者
            //    （MoveTowards 内部归一化）。
            var brainContext = _target.HasValue
                ? new EnemyBrain.Context(Motor.Position, _target.Value, true)
                : EnemyBrain.Context.WithoutTarget(Motor.Position);

            _intent = _brain.Decide(in brainContext);

            var snapshot = new InputSnapshot(_intent.Direction, false, false, false);
            var context = new LogicContext(now, deltaTime, default, snapshot);

            FixedTick(context);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // ② 状态效果层：帧外挂起的击退在这里变成一次"进入受击"，并产出本帧门禁
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            // ③ 移动层：唯一写速度的地方；上层门禁在这里落地
            _move.Tick(in ctx, _status.Gates);
        }
    }
}
