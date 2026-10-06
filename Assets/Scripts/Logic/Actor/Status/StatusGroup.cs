using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// <b>角色状态效果层</b>：受击 / 硬直这类"作用在角色身上的效果"，产出门禁交给移动层。
    /// <b>玩家与敌人共用同一个类</b>，差别只在各自 <c>CharacterConfig</c> 里的数值。
    /// </summary>
    /// <remarks>
    /// 串行门禁的第一层：<b>本层 → 战斗层 → 移动层</b>。每层只向下层输出门禁与锁，
    /// 只有移动层拥有写速度的权限（不变量：角色速度只有一个写者）。
    /// <para><b>挂起的冲量在帧首消费</b>：世界侧的结算跑在角色那一帧之后（顺序由 <c>GameRoot</c> 的
    /// <c>Order</c> 固定），所以帧外递交的击退只能"挂起"，由本层在下一帧的开头变成一次状态进入。</para>
    /// <para>本层<b>不写速度</b>：它只回答"这一帧该被外力推成什么样"。</para>
    /// </remarks>
    public sealed class StatusGroup : StateGroup<StatusStateTag>
    {
        private readonly HurtState _hurt;

        /// <param name="logic">宿主角色的账本（玩家与敌人都适用）。</param>
        public StatusGroup(ActorLogic logic)
        {
            _hurt = new HurtState(logic, logic.Config);

            AddState(new NormalState(logic, logic.Config));
            AddState(_hurt);
        }

        protected override StatusStateTag EmptyTag => StatusStateTag.Empty;

        protected override StatusStateTag Fallback(in LogicContext ctx) => StatusStateTag.Normal;

        /// <summary>受击状态实例（供调试面板与测试读它的剩余速度）。</summary>
        public HurtState Hurt => _hurt;

        /// <summary>本帧提交给移动层的门禁。</summary>
        /// <remarks>只有受击期间非空：那一帧的"该被推成什么样"就是本层对下层的全部输出。</remarks>
        public MoveGates Gates => Current == StatusStateTag.Hurt
            ? new MoveGates(_hurt.ForcedVelocity)
            : MoveGates.None;

        /// <summary>是否正处于受击中（视效用它决定闪不闪）。</summary>
        public bool IsHurt => Current == StatusStateTag.Hurt;

        /// <summary>推进一个物理帧。</summary>
        /// <param name="pendingKnockback">帧外挂起的击退冲量（速度向量）；零表示没有。</param>
        public void Tick(in LogicContext ctx, Vector2 pendingKnockback)
        {
            EnterHurtIfPending(in ctx, pendingKnockback);

            TickStates(in ctx);
        }

        private void EnterHurtIfPending(in LogicContext ctx, Vector2 knockback)
        {
            // 逐分量精确比较：Vector2 的 != 带 1e-10 的平方容差，小冲量会被静默吞掉
            if (knockback.x == 0f && knockback.y == 0f) return;

            _hurt.Configure(knockback);

            ChangeState(StatusStateTag.Hurt, in ctx);
        }
    }
}
