using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic
{
    /// <summary>按意图推进，不乘减速</summary>
    public sealed class EnemyChaseState : StateBase<MovementStateTag, LogicContext>
    {
        private readonly EnemyLogic _logic;

        public EnemyChaseState(EnemyLogic logic) : base(logic)
        {
            _logic = logic;
        }

        public override MovementStateTag StateTag => MovementStateTag.Move;

        public override void Enter(LogicContext ctx)
        {
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            Steering steering = _logic.ResolveSteering();

            Host.MoveTowards(steering.Direction, steering.Speed);
        }

        /// <summary>不动就让位</summary>
        public override bool IsDone(LogicContext ctx)
        {
            return _logic.Intent.IsIdle;
        }
    }
}
