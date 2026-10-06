using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Movement.States;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的移动状态组：<b>唯一写速度的地方</b>，与玩家的 <c>MoveGroup</c> 同一套骨架。
    /// </summary>
    /// <remarks>
    /// 与玩家那一台的差别只有两条，都是"敌人没有"造成的：
    /// <list type="number">
    /// <item>没有冲刺 ⇒ 没有抢占链，也没有冲刺冷却；</item>
    /// <item>基础态的速度来自<b>大脑的意图</b>（<see cref="EnemyChaseState"/>），
    /// 而不是"输入方向 × 配置速度"。</item>
    /// </list>
    /// <para><b>共用的部分：</b><see cref="StateGroup{TStateTag}"/> 的两段式仲裁、
    /// <c>IdleState</c>（松手滑停）、<c>MoveGates</c> 门禁落地。门的语义与玩家完全一致：
    /// <b>状态先写速度、门禁最后统一施加</b> —— 否则"挨打了却纹丝不动"会以另一种形式回来。</para>
    /// </remarks>
    public sealed class EnemyMoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        private readonly EnemyLogic _logic;

        public EnemyMoveGroup(EnemyLogic logic)
        {
            _logic = logic;

            AddState(new IdleState(logic));
            AddState(new EnemyChaseState(logic));
        }

        protected override MovementStateTag EmptyTag => MovementStateTag.Empty;

        /// <summary>基础态：意图为零就站住（滑停），有方向就走。</summary>
        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            return ctx.inputSnapshot.Move.sqrMagnitude > 0f
                ? MovementStateTag.Move
                : MovementStateTag.Idle;
        }

        /// <summary>推进一个物理帧：速度乘数先交给账本、状态再按意图写速度、强制速度最后整条接管。</summary>
        /// <param name="gates">上层（状态效果层）提交的门禁；空门禁时本层完全按自己的状态走。</param>
        public void Tick(in LogicContext ctx, in MoveGates gates)
        {
            // 乘数落在"目标速度"上，所以必须赶在状态算速度之前交给账本（见 ActorLogic.SetSpeedScale）。
            _logic.SetSpeedScale(gates.SpeedScale);

            TickStates(in ctx);

            ApplyGates(in gates);
        }

        /// <summary>
        /// 把门禁落到速度上（与玩家移动组同一套：只有这里能按上层要求改速度）。
        /// </summary>
        /// <remarks><b>强制速度不叠加速度乘数</b>：受击滑停是外力，叠上地面减速会把它拖短。</remarks>
        private void ApplyGates(in MoveGates gates)
        {
            if (gates.HasForcedVelocity) _logic.SetVelocity(gates.ForcedVelocity);
        }
    }
}
