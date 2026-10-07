using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的逻辑层：持有<b>速度账本 ＋ 状态效果层 ＋ 移动层 ＋ 大脑</b>，与玩家同构；
    /// 唯一差别是"输入从哪来" —— 玩家来自 <c>InputBuffer</c>，敌人来自 <see cref="EnemyBrain"/>。
    /// </summary>
    /// <remarks>
    /// <b>它是组合件，不是转发层</b>（审查已定：让 Logic 退化为组合件，与玩家同风格）：
    /// 速度账本与控制律在 <see cref="ActorLogic.Motor"/> 上，意图在 <see cref="Brain"/> 上，
    /// 移动状态在 <see cref="MoveGroup"/> 上 —— 消费者直接去那几个件取，
    /// 本类不再为它们各开一个同名的转发属性。
    /// <para><b>帧内顺序与玩家逐条对齐：</b>
    /// <code>
    /// ① 大脑决策（意图 = 方向 ＋ 速度）    // 玩家侧对应"读输入快照"
    /// ② StatusGroup.Tick(ctx, 挂起的击退)  // 产出本帧门禁
    /// ③ EnemyMoveGroup.Tick(ctx, 门禁)     // 唯一写速度
    /// </code>
    /// <b>决策放在 <c>Tick</c> 而不是 <c>OnTick</c> 里做</b>：意图必须先进入上下文
    /// （移动层的基础态判据就是它），而上下文在驱动之前组装一次。</para>
    /// <para><b>它不查世界</b>：目标位置由表现层的组合根每帧喂入（<see cref="SetTarget"/>）；
    /// 减速不再由组合根喂系数 —— 它由格状态提交、由状态效果层持有（见 <c>StatusGroup.ApplySlow</c>）。</para>
    /// </remarks>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly StatusGroup _status;
        private readonly EnemyMoveGroup _move;

        /// <summary>追击目标（玩家位置）。没有目标时不提交任何移动。</summary>
        private Vector2? _target;

        /// <summary>
        /// 已递交、但还没被状态效果层消费的击退冲量。
        /// </summary>
        /// <remarks>
        /// <b>为什么冲量必须"挂着"而不是当场写进账本：</b>账本在<b>帧首</b>就把累积区清零了，
        /// 而那一刻在 <c>OnTick</c> 之前。于是"在两次 Tick 之间调 <c>AddImpulse</c>"会被下一次
        /// 固定帧的开头<b>静默抹掉</b> —— 冲量从头到尾没到过执行器，而账本看起来一切正常。
        /// <para>这不是测试才有的问题：格子结算在渲染帧、敌人 Tick 在物理帧，两者之间插着帧边界。
        /// 现象是"打中了但敌人纹丝不动"，且没有任何报错。</para>
        /// </remarks>
        private Vector2 _pendingKnockback;

        /// <param name="motor">移动执行器（账本 ＋ 控制律 ＋ 物理体读写；测试可塞纯 C# 探针）。</param>
        /// <param name="spec">敌人的取值边界（表行 ＋ 角色运动配置）。</param>
        public EnemyLogic(IActorMotor motor, EnemySpec spec)
            : base(motor, spec.Config)
        {
            Brain = new EnemyBrain(spec);
            _status = new StatusGroup(this);
            _move = new EnemyMoveGroup(this, motor);
        }

        /// <summary>大脑：<b>意图与转向的持有者</b>。谁要数据就来这里取（审查："把 Brain 开放"）。</summary>
        public EnemyBrain Brain { get; }

        /// <summary>状态效果层（受击 / 减速修饰）。</summary>
        public StatusGroup Status => _status;

        /// <summary>移动层（追击 / 站立）。<b>当前移动状态去这里取</b>（<c>MoveGroup.Current</c>）。</summary>
        public EnemyMoveGroup MoveGroup => _move;

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

            EnemyIntent intent = Brain.Decide(in brainContext);

            var snapshot = new InputSnapshot(intent.Direction, false, false);
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
