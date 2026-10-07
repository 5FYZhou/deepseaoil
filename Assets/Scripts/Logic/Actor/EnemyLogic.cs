using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人的逻辑层：持有<b>速度账本 ＋ 状态效果层 ＋ 移动层 ＋ 大脑</b>，与玩家同构；唯一差别是"输入从哪来" —— 玩家来自 <c>InputBuffer</c>，敌人来自 <see cref="EnemyBrain"/>。帧序见 ActorLogic。</summary>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly StatusGroup _status;
        private readonly EnemyMoveGroup _move;

        private Vector2? _target;

        /// <remarks>冲量必须"挂着"，不能当场写账本：账本在<b>帧首</b>清零累积区，而那一刻在 <c>OnTick</c> 之前 —— 在两次 Tick 之间写会被下一次固定帧的开头<b>静默抹掉</b>。格子结算在渲染帧、敌人 Tick 在物理帧，现象是"打中了但敌人纹丝不动"，不报错。</remarks>
        private Vector2 _pendingKnockback;

        public EnemyLogic(IActorMotor motor, EnemySpec spec)
            : base(motor, spec.Config)
        {
            Brain = new EnemyBrain(spec);
            _status = new StatusGroup(this);
            _move = new EnemyMoveGroup(this, motor);
        }

        public EnemyBrain Brain { get; }

        public StatusGroup Status => _status;

        public EnemyMoveGroup MoveGroup => _move;

        public bool IsHurt => _status.IsHurt;

        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        /// <remarks>同一帧内多次调用会累加（被两处同时结算就是两股冲量）。</remarks>
        public void ApplyKnockback(float impulse, Vector2 direction)
        {
            if (impulse <= 0f) return;

            _pendingKnockback += direction * impulse;
        }

        public void Tick(float now, float deltaTime)
        {
            // 意图必须先进入上下文（移动层的基础态判据就是它），所以大脑决策放在这里而不是 OnTick 里；方向直接进 inputSnapshot.Move —— 它是"本帧的移动指令"，量纲不影响消费者（MoveTowards 内部归一化）。
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
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            // 移动层：唯一写速度的地方；上层门禁在这里落地
            _move.Tick(in ctx, _status.Gates);
        }
    }
}
