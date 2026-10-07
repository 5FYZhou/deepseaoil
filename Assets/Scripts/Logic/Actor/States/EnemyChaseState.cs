using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 追击：按意图给出的方向与速度推进，意图的速度里已经乘过减速系数（不要在此再乘一次）。
    /// </summary>
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
            EnemyIntent intent = _logic.Brain.Intent;

            Host.MoveTowards(intent.Direction, intent.Speed);
        }

        /// <summary>意图说"不动"就结束，让位给基础态（站立 / 滑停）。</summary>
        public override bool IsDone(LogicContext ctx)
        {
            return _logic.Brain.Intent.IsIdle;
        }
    }
}
