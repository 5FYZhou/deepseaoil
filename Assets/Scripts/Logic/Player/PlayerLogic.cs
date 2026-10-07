using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>玩家逻辑：持有账本（血量 ＋ 水球）与三层领域状态机（状态效果 / 战斗 / 移动），并回答"我现在能不能做某件事"。</summary>
    /// <remarks><b>受击的唯一入口是 <see cref="TakeDamage"/></b>：世界侧只能"通知"，数据由玩家侧自己改。
    /// <b>击退必须"挂起"</b>：账本帧首清累加区，帧外直接 <c>AddImpulse</c> 会被下一帧帧首静默抹掉 ⇒"被撞了但纹丝不动"，且不报错。驱动顺序：状态效果 → 战斗 → 移动，只有移动层能写速度。</remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerStats _stats;
        private readonly InputBuffer _buffer;
        private readonly StatusGroup _status;
        private readonly CombatGroup _combat;
        private readonly MoveGroup _moveGroup;

        /// <summary>已递交、还没被状态效果层消费的击退冲量（可在帧外累加）。</summary>
        private Vector2 _pendingKnockback;

        public PlayerLogic(IActorMotor motor, PlayerSpec spec, InputBuffer buffer)
            : base(motor, spec.Config)
        {
            _stats = new PlayerStats(spec);
            _buffer = buffer;

            _status = new StatusGroup(this);
            _combat = new CombatGroup(this);
            _moveGroup = new MoveGroup(this, spec, motor, buffer);
        }

        public PlayerStats Stats => _stats;

        public MoveGroup MoveGroup => _moveGroup;

        public StatusGroup Status => _status;

        public CombatGroup Combat => _combat;

        public bool IsAlive => _stats.IsAlive;

        public void ConfigureAim(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _combat.Configure(in geometry, maxThrowDistance, sink);
        }

        public void UpdateAim(Vector2 aimWorld, float now)
        {
            _combat.UpdateAim(Motor.Position, aimWorld, now);
        }

        public void ClearAim()
        {
            _combat.ClearAim();
        }

        public bool RequestThrow(BallType ball, float now)
        {
            return _combat.RequestThrow(ball, now);
        }

        /// <summary>结算一次伤害：<b>世界侧唯一的受伤入口</b>（接触、陷阱、将来的技能都走这里）。</summary>
        /// <remarks><b>被挡掉时三件事一起不发生</b>：不扣血、不推、也不写无敌时间；冲量只是<b>挂起</b>，真正的接管在下一次 <see cref="OnTick"/>。</remarks>
        public bool TakeDamage(in Damage damage, float now)
        {
            if (!damage.HasDamage && !damage.HasKnockback) return false;

            if (damage.HasDamage && !_stats.ApplyDamage(damage.Amount, now)) return false;

            if (damage.HasKnockback)
            {
                Vector2 impulse = damage.Direction * damage.Impulse;
                float limit = _stats.Spec.KnockbackSpeedLimit;

                // 上限非法（0 / 非数）时按"不限"处理：ClampMagnitude 遇到 0 会把冲量整个吃掉
                _pendingKnockback = limit > 0f && !float.IsNaN(limit)
                    ? Vector2.ClampMagnitude(impulse, limit)
                    : impulse;
            }

            return true;
        }

        /// <summary>把角色瞬移到给定位置（重生 / 传送用），并把血量恢复满、停住、清掉挂起的击退。</summary>
        /// <remarks>走执行器的物理体位置（<c>transform.position</c> 会被位置积分覆盖 ⇒ "瞬移了一下又弹回去"）；<b>速度要硬清零</b>：账本 <c>StopMove</c> 只管"本帧该提交什么"，物理体上的残留速度会让复活后自己滑一段。</remarks>
        public void RespawnTo(Vector2 position)
        {
            _stats.ResetToFull();

            Motor.StopMove();

            _pendingKnockback = Vector2.zero;

            Motor.Move(Vector2.zero);
            Motor.SetPosition(position);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // ① 状态效果层（帧外挂起的击退在这里变成一次"进入受击"并产出本帧门禁）→ ③ 移动层（唯一写速度处）；② 战斗层在物理帧没有职责
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            _moveGroup.Tick(in ctx, _status.Gates);
        }
    }
}
