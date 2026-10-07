using DeepseaOil.Foundation;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 平常态：没有任何效果在生效，门禁为空，永不结束；它是状态效果层的基础态（<c>Fallback</c> 的归宿）。
    /// </summary>
    public sealed class NormalState : StateBase<StatusStateTag, LogicContext>
    {
        public NormalState(IStateHost host) : base(host)
        {
        }

        public override StatusStateTag StateTag => StatusStateTag.Normal;

        public override void Enter(LogicContext ctx)
        {
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
        }

        public override bool IsDone(LogicContext ctx)
        {
            return false;
        }
    }
}
