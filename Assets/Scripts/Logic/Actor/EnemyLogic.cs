using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人逻辑层：账本＋状态层＋移动层＋大脑</summary>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly StatusGroup _status;
        private readonly EnemyMoveGroup _move;

        // 绕障网格，null = 不绕障
        private GridLogic _grid;

        private readonly List<Vector3Int> _pathFrontier = new(32);
        private readonly Queue<Vector3Int> _pathQueue = new(32);
        private readonly HashSet<Vector3Int> _pathVisited = new(64);
        private readonly Dictionary<Vector3Int, Vector3Int> _pathCameFrom = new(64);

        private System.Func<Vector3Int, bool> _isWalkable;

        private int _pathMaxSteps;
        private int _pathRecalcFrames;

        private int _pathCooldown;
        private Vector2 _pathDirection;

        private Vector2? _target;

        /// <remarks>冲量必须挂起，不能当场写账本：账本在帧首清累积区，两次 Tick 之间写会被下一固定帧清掉</remarks>
        private Vector2 _pendingKnockback;

        public EnemyLogic(IActorMotor motor, EnemySpec spec)
            : this(motor, spec, EnemyBrainFactory.Create(in spec))
        {
        }

        /// <summary>注入大脑；brain=null 按 spec 造默认</summary>
        public EnemyLogic(IActorMotor motor, EnemySpec spec, IEnemyBrain brain)
            : base(motor, spec.Config)
        {
            Brain = brain ?? EnemyBrainFactory.Create(in spec);
            _status = new StatusGroup(this);
            _move = new EnemyMoveGroup(this, motor);
        }

        public IEnemyBrain Brain { get; }

        /// <summary>本帧意图，由 Brain.Decide 产出；状态层唯一读口</summary>
        public EnemyIntent Intent { get; private set; }

        public StatusGroup Status => _status;

        public EnemyMoveGroup MoveGroup => _move;

        public bool IsHurt => _status.IsHurt;

        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        public void AttachGrid(GridLogic grid)
        {
            _grid = grid;

            // 委托只构造一次：写进 ResolveObstacle 会每帧分配
            _isWalkable = IsWalkable;

            _pathMaxSteps = ConfigModule.PathMaxSteps;
            _pathRecalcFrames = ConfigModule.PathRecalcFrames;
        }

        public void ForceLoseAggro()
        {
        }

        /// <summary>绕障方向：被挡时给 BFS 第一步，找不到就原方向</summary>
        public Vector2 ResolveObstacle(Vector2 direction)
        {
            if (_grid == null || !_grid.Geometry.IsValid) return direction;

            if (direction.sqrMagnitude <= 0f) return direction;

            if (_pathCooldown-- > 0) return _pathDirection.sqrMagnitude > 0f ? _pathDirection : direction;

            _pathCooldown = _pathRecalcFrames;

            Vector2 position = Motor.Position;

            Vector2 ahead = position + direction.normalized * (_grid.Geometry.CellSize * 0.6f);

            if (!_grid.IsObstacle(_grid.WorldToCell(ahead)))
            {
                _pathDirection = direction;

                return direction;
            }

            if (!_target.HasValue)
            {
                _pathDirection = direction;

                return direction;
            }

            bool found = PathProbe.TryFindStep(
                _grid.Geometry,
                _grid.WorldToCell(position),
                _grid.WorldToCell(_target.Value),
                _pathMaxSteps,
                _isWalkable,
                _pathFrontier,
                _pathQueue,
                _pathVisited,
                _pathCameFrom,
                out Vector3Int step);

            if (!found)
            {
                _pathDirection = direction;

                return direction;
            }

            Vector2 toStep = _grid.Geometry.CellCenter(step) - position;

            _pathDirection = toStep.sqrMagnitude > 0f ? toStep.normalized : direction;

            return _pathDirection;
        }

        private bool IsWalkable(Vector3Int cell)
        {
            return _grid != null && _grid.HasCell(cell) && !_grid.IsObstacle(cell);
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
        }
    }
}
