using DeepseaOil.Foundation;

namespace DeepseaOil.Logic
{
    /// <summary>平常态：无效果、门禁空、永不结束的基础态</summary>
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
