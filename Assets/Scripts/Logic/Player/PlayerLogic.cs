using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>玩家逻辑：账本+三层状态机（状态效果/战斗/移动）</summary>
    /// <remarks>受击唯一入口 TakeDamage（世界侧只通知）；击退必须挂起，帧外 AddImpulse 会被帧首静默抹掉；驱动顺序 状态效果→战斗→移动，只有移动层写速度</remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerStats _stats;
        private readonly InputBuffer _buffer;
        private readonly StatusGroup _status;
        private readonly CombatGroup _combat;
        private readonly MoveGroup _moveGroup;

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

        /// <summary>世界侧唯一受伤入口</summary>
        /// <remarks>被挡掉时不扣血、不推、不写无敌</remarks>
        public bool TakeDamage(in Damage damage, float now)
        {
            if (!damage.HasDamage && !damage.HasKnockback) return false;

            if (damage.HasDamage && !_stats.ApplyDamage(damage.Amount, now)) return false;

            if (damage.HasKnockback)
            {
                Vector2 impulse = damage.Direction * damage.Impulse;
                float limit = _stats.Spec.KnockbackSpeedLimit;

                // 上限非法（0/非数）按不限：ClampMagnitude 遇 0 吃掉整个冲量
                _pendingKnockback = limit > 0f && !float.IsNaN(limit)
                    ? Vector2.ClampMagnitude(impulse, limit)
                    : impulse;
            }

            return true;
        }

        /// <summary>瞬移到指定位置并回满血</summary>
        /// <remarks>走物理体位置（transform.position 会被积分覆盖⇒弹回去）；速度硬清零，否则复活后滑一段</remarks>
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
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            _moveGroup.Tick(in ctx, _status.Gates);
        }
    }
}
