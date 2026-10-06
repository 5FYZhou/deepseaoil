using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 平常：没有任何效果在生效，门禁为空，<b>永不结束</b>。
    /// </summary>
    /// <remarks>
    /// 它是状态效果层的基础态（<c>Fallback</c> 的归宿）。做成一个显式状态而不是"没有状态"，
    /// 是为了让"当前是第几层、正在什么状态"这个问题在任何时刻都有答案 ——
    /// 也让 <c>StatusGroup.Current</c> 不必区分"null 与 Normal"两种空。
    /// </remarks>
    public sealed class NormalState : StateBase<StatusStateTag>
    {
        public NormalState(ActorLogic logic, CharacterConfig config) : base(logic, config)
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
