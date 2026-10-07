using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 追击：按<b>意图</b>给出的方向与速度推进（意图的速度里已经乘过减速系数）。
    /// </summary>
    /// <remarks>
    /// <b>为什么不复用玩家的 <c>MoveState</c>：</b><c>MoveState</c> 的速度取自配置
    /// （<c>Config.moveSpeed</c>），而敌人的速度是<b>大脑每帧算出来的</b>
    /// （含追击范围、停止距离）。控制律两边是同一份（<c>Host.MoveTowards</c> → <c>SteerTowards</c>），
    /// 差别只有"速度从哪来"这一处。
    /// <para><b>意图从大脑取，不经逻辑层转发</b>（审查："谁要数据就去 Brain 取"）：
    /// <c>EnemyLogic</c> 因此不再缓存一份本帧意图。</para>
    /// <para><b>状态标签取 <c>Move</c></b>：对外的读数与玩家同一套（"在走"），
    /// 调试面板与测试因此不必认识"敌人的走"这个新标签。</para>
    /// </remarks>
    public sealed class EnemyChaseState : StateBase<MovementStateTag, LogicContext>
    {
        private readonly EnemyLogic _logic;

        /// <param name="logic">宿主的组合件（本状态是敌人专用，类型收窄到 <see cref="EnemyLogic"/>）。</param>
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
