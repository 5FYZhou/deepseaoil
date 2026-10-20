using DeepseaOil.Data;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人逻辑层：账本＋状态层＋移动层＋大脑＋转向器</summary>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly StatusGroup _status;
        private readonly EnemyMoveGroup _move;

        /// <summary>绕障与转向；AttachGrid 前为 null，只走直线意图</summary>
        private readonly EnemyNavigator _navigator;

        /// <summary>低速线：物理把速度压到零才算卡住，慢速蹭墙不算</summary>
        private const float StuckSpeedSqr = 0.01f;

        private const float JamExemptRange = 1f;

        private int _stuckFrames;
        private int _stuckFramesMax;

        private Vector2? _target;

        /// <summary>本帧停住散开方向；非零时按 Move 走</summary>
        private Vector2 _spreadDirection;

        /// <remarks>冲量必须挂起：账本帧首清累积区，当场写会被下一固定帧清掉</remarks>
        private Vector2 _pendingKnockback;

        public EnemyLogic(IActorMotor motor, EnemySpec spec)
            : this(motor, spec, EnemyBrainFactory.Create(in spec))
        {
        }

        public EnemyLogic(IActorMotor motor, EnemySpec spec, IEnemyBrain brain)
            : base(motor, spec.Config)
        {
            Brain = brain ?? EnemyBrainFactory.Create(in spec);
            _status = new StatusGroup(this);
            _move = new EnemyMoveGroup(this, motor);
            _navigator = new EnemyNavigator(spec.Radius, spec.TurnRateLimit, spec.TurnSmoothSeconds);
        }

        public IEnemyBrain Brain { get; }

        /// <summary>本帧意图；状态层唯一读口</summary>
        public EnemyIntent Intent { get; private set; }

        public StatusGroup Status => _status;

        public EnemyMoveGroup MoveGroup => _move;

        public bool IsHurt => _status.IsHurt;

        /// <summary>转向器读数，诊断用</summary>
        public EnemyNavigator Navigator => _navigator;

        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        public void AttachGrid(GridLogic grid)
        {
            _navigator.AttachGrid(grid);

            _stuckFramesMax = ConfigModule.StuckFrames;
        }

        public void ForceLoseAggro()
        {
        }

        /// <summary>本帧方向与速度：流场/入弯/分离/换步都在转向器里落地</summary>
        public Steering ResolveSteering()
        {
            Vector2 velocity = ((IMovementMotor)Motor).Velocity;

            if (_spreadDirection.sqrMagnitude > 0f) return _navigator.ResolveSpread(_spreadDirection, velocity, Ctx.now);

            return _navigator.Resolve(Intent, Motor.Position, velocity, Motor.Motion.MoveAcceleration, Ctx.now);
        }

        /// <summary>卡死判定：物理把速度压到零而大脑仍在追，连续满帧即换步</summary>
        /// <remarks>没接线网格时整套绕障不存在，卡死判定无从谈起。</remarks>
        private void DetectStuck()
        {
            if (_navigator == null || _stuckFramesMax <= 0) return;

            // 取速度必须走 IMovementMotor 面：Motor.Velocity 与 IActorLedger.Velocity 二义
            if (((IMovementMotor)Motor).Velocity.sqrMagnitude > StuckSpeedSqr)
            {
                _stuckFrames = 0;
                return;
            }

            if (Intent.IsIdle || _pendingKnockback.sqrMagnitude > 0f)
            {
                _stuckFrames = 0;
                return;
            }

            // 目标贴脸：流场不认识目标身体，换步换的还是一张不认它的场 ⇒ 顶住/侧移会来回抖
            if (_target.HasValue && (_target.Value - Motor.Position).sqrMagnitude <= JamExemptRange * JamExemptRange)
            {
                _stuckFrames = 0;
                return;
            }

            if (++_stuckFrames < _stuckFramesMax) return;

            _stuckFrames = 0;
            _navigator.NotifyStuck();
        }

        public void ApplyKnockback(float impulse, Vector2 direction)
        {
            if (impulse <= 0f) return;

            _pendingKnockback += direction * impulse;
        }

        public void Tick(float now, float deltaTime)
        {
            // 意图必须先入上下文
            var brainContext = _target.HasValue
                ? new EnemyBrainContext(Motor.Position, _target.Value, true)
                : EnemyBrainContext.WithoutTarget(Motor.Position);

            Intent = Brain.Decide(in brainContext);

            // 停住也要散开：没有物理推挤时，不加这一步多只怪会精确叠在同一点
            _spreadDirection = Intent.IsIdle ? _navigator.IdleSpread(Motor.Position, _target) : Vector2.zero;

            Vector2 move = _spreadDirection.sqrMagnitude > 0f ? _spreadDirection : Intent.Direction;

            var snapshot = new InputSnapshot(move, false);
            var context = new LogicContext(now, deltaTime, default, snapshot);

            FixedTick(context);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            // 移动层唯一写速度处，门禁在此落地
            _move.Tick(in ctx, _status.Gates);

            // 必须晚于移动层：否则读到的是上一帧的已解算速度
            DetectStuck();
        }
    }
}
