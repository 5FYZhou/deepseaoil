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
        /// <summary>八邻域，不含自身；脱困要能沿对角挤出，故与 PathProbe 的四邻域不同</summary>
        private static readonly Vector3Int[] Neighbors8 =
        {
            new Vector3Int(-1, -1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(1, 1, 0),
        };

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

        private Vector3Int _currentStepCell;
        private bool _isSteeringActive;

        private float _bodyRadius;

        /// <summary>低速线：物理把速度压到零才算卡住，慢速蹭墙不算</summary>
        private const float StuckSpeedSqr = 0.01f;
        private const int StuckFramesMax = 10;
        private const int EscapeFrames = 8;

        private int _stuckFrames;
        private int _escapeFrames;

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
            _bodyRadius = spec.Radius;
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

            // 委托只构造一次：每帧 new 会持续分配
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

            Vector2 position = Motor.Position;
            Vector2 dirNormalized = direction.normalized;

            if (_escapeFrames > 0)
            {
                _escapeFrames--;
                _isSteeringActive = false;
                _pathCooldown = 0;

                Vector2 escape = EscapeDirection(position, dirNormalized);

                // 八面皆墙时也要给一个非零方向：零向量会被账本当"停住"，卡死判定就再也解不开
                return escape.sqrMagnitude > 0f ? escape : new Vector2(-dirNormalized.y, dirNormalized.x);
            }

            // 迟滞锁：锁定期内不重评探针，走到格心（0.2 米）或冷却耗尽才解锁，防逐帧翻转抽搐
            if (_isSteeringActive && _pathCooldown > 0)
            {
                _pathCooldown--;

                Vector2 toLocked = _grid.Geometry.CellCenter(_currentStepCell) - position;
                if (toLocked.sqrMagnitude > 0.04f) return _pathDirection;

                _isSteeringActive = false;
            }

            _pathCooldown = _pathRecalcFrames;

            if (!IsBlockedAhead(position, dirNormalized))
            {
                _isSteeringActive = false;
                _pathDirection = direction;
                return direction;
            }

            if (!_target.HasValue)
            {
                _isSteeringActive = false;
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
                _isSteeringActive = false;
                _pathDirection = direction;
                return direction;
            }

            _currentStepCell = step;
            _isSteeringActive = true;

            Vector2 toStep = _grid.Geometry.CellCenter(step) - position;
            _pathDirection = toStep.sqrMagnitude > 0f ? toStep.normalized : direction;

            return _pathDirection;
        }

        private bool IsBlockedAhead(Vector2 position, Vector2 dirNormalized)
        {
            float cellSize = _grid.Geometry.CellSize;

            // 探针 0.85 格 / 侧翼 0.45 格：探针须盖过身体半径，否则墙角擦碰漏判
            float probeDistance = cellSize * 0.85f;
            float sideOffset = cellSize * 0.45f;

            Vector2 normal = new Vector2(-dirNormalized.y, dirNormalized.x) * sideOffset;

            // 身体跨格：只探一个格心会漏判，须覆盖到身体半径触达的格
            int reach = 1 + (int)(_bodyRadius / cellSize);

            for (int i = 0; i < reach; i++)
            {
                Vector2 centerAhead = position + dirNormalized * (probeDistance + i * cellSize);

                if (IsCellBlocked(_grid.WorldToCell(centerAhead))) return true;
                if (IsCellBlocked(_grid.WorldToCell(centerAhead + normal))) return true;
                if (IsCellBlocked(_grid.WorldToCell(centerAhead - normal))) return true;
            }

            return false;
        }

        /// <summary>脱困方向：背离周围阻挡格，四周无阻挡时先原路退回</summary>
        private Vector2 EscapeDirection(Vector2 position, Vector2 dirNormalized)
        {
            Vector3Int origin = _grid.WorldToCell(position);
            Vector2 away = Vector2.zero;

            for (int i = 0; i < Neighbors8.Length; i++)
            {
                Vector3Int neighbor = origin + Neighbors8[i];

                // 只认已登记且阻挡的格：格外的空处不该算墙，否则贴地图边缘时八邻域全"阻挡"而无处可退
                if (!_grid.HasCell(neighbor) || !_grid.IsObstacle(neighbor)) continue;

                away.x -= Neighbors8[i].x;
                away.y -= Neighbors8[i].y;
            }

            if (away.sqrMagnitude > 0f) return away.normalized;

            // 反向若仍贴着同一面墙就横着走，靠正交方向脱出
            Vector2 reverse = -dirNormalized;

            return IsBlockedAhead(position, reverse)
                ? new Vector2(-reverse.y, reverse.x)
                : reverse;
        }

        /// <summary>卡死判定：物理把速度压到零而大脑仍在追，连续满帧即脱困</summary>
        private void DetectStuck()
        {
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

            if (++_stuckFrames < StuckFramesMax) return;

            _stuckFrames = 0;
            _escapeFrames = EscapeFrames;
        }

        private bool IsCellBlocked(Vector3Int cell)
        {
            return !_grid.HasCell(cell) || _grid.IsObstacle(cell);
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

            // 必须晚于移动层：否则读到的是上一帧的已解算速度
            DetectStuck();
        }
    }
}
