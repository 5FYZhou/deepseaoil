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

        /// <summary>绕障与转向；AttachGrid 之前为 null，此时只走大脑的直线意图</summary>
        private readonly EnemyNavigator _navigator;

        /// <summary>低速线：物理把速度压到零才算卡住，慢速蹭墙不算</summary>
        private const float StuckSpeedSqr = 0.01f;

        private int _stuckFrames;
        private int _stuckFramesMax;

        private Vector2? _target;

        /// <remarks>冲量必须挂起，不能当场写账本：账本在帧首清累积区，两次 Tick 之间写会被下一固定帧清掉</remarks>
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
            _navigator = new EnemyNavigator(spec.Radius);
        }

        public IEnemyBrain Brain { get; }

        /// <summary>本帧意图，由 Brain.Decide 产出；状态层唯一读口</summary>
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

        /// <summary>本帧可执行的方向与速度：流场跟随、入弯降速、软分离、卡死换步都在转向器里落地</summary>
        public Steering ResolveSteering()
        {
            return _navigator.Resolve(Intent, Motor.Position, ((IMovementMotor)Motor).Velocity, Motor.Motion.MoveAcceleration);
        }

        /// <summary>卡死判定：物理把速度压到零而大脑仍在追，连续满帧即换步</summary>
        /// <remarks>没接线网格时整套绕障都不存在，卡死判定也就无从谈起（换步方向要让场给）。</remarks>
        private void DetectStuck()
        {
            if (_navigator == null || _stuckFramesMax <= 0) return;

            // 走 IMovementMotor 面取速度：直接取 Motor.Velocity 与 IActorLedger.Velocity 二义
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
            // 意图必须先入上下文，故大脑决策放此处
            var brainContext = _target.HasValue
                ? new EnemyBrainContext(Motor.Position, _target.Value, true)
                : EnemyBrainContext.WithoutTarget(Motor.Position);

            Intent = Brain.Decide(in brainContext);

            var snapshot = new InputSnapshot(Intent.Direction, false, false);
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
