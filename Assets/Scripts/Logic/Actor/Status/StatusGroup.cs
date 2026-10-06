using DeepseaOil.Foundation;
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
    public sealed class StatusGroup : StateGroup<StatusStateTag, LogicContext>
    {
        private readonly HurtState _hurt;

        /// <summary>当前减速修饰的乘数（1 = 没有修饰）。</summary>
        private float _slowScale = 1f;

        /// <summary>减速修饰还剩多久过期（秒）。</summary>
        private float _slowRemaining;

        /// <param name="logic">宿主角色的账本（玩家与敌人都适用）。</param>
        public StatusGroup(ActorLogic logic)
        {
            _hurt = new HurtState(logic);

            AddState(new NormalState(logic));
            AddState(_hurt);
        }

        protected override StatusStateTag EmptyTag => StatusStateTag.Empty;

        protected override StatusStateTag Fallback(in LogicContext ctx) => StatusStateTag.Normal;

        /// <summary>受击状态实例（供调试面板与测试读它的剩余速度）。</summary>
        public HurtState Hurt => _hurt;

        /// <summary>当前生效的减速乘数（<c>1</c> = 没有减速修饰）。</summary>
        public float SlowScale => _slowRemaining > 0f ? _slowScale : 1f;

        /// <summary>
        /// 续一次减速修饰（由格子的执行者按格施加）。
        /// </summary>
        /// <param name="speedScale">速度乘数（<c>1</c> = 不减速）。</param>
        /// <param name="seconds">存活时长（秒）；<c>≤ 0</c> 时忽略这次提交。</param>
        /// <remarks>
        /// <b>为什么是"续命"而不是"设一个开关"：</b>施加方（格状态）不持有、也不查询"格上的目标"，
        /// 它只在自己每次 Tick 时续一次 —— 于是"谁摘掉这个修饰"这个问题不存在：
        /// 离开泥浆 ⇒ 不再续命 ⇒ 修饰自然过期；暂停 ⇒ 格子不 Tick ⇒ 恢复后立刻续上。
        /// <para><b>它住在本层（状态效果）而不是移动层</b>：这是"作用在角色身上的效果"，
        /// 移动层只接收"这一帧速度乘多少"的门禁。</para>
        /// <para>非数按"不起作用"（1）处理：非数一旦进入速度，角色会带着非数坐标消失，且不报错。</para>
        /// </remarks>
        public void ApplySlow(float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            _slowScale = float.IsNaN(speedScale) ? 1f : Mathf.Clamp01(speedScale);
            _slowRemaining = seconds;
        }

        /// <summary>本帧提交给移动层的门禁。</summary>
        /// <remarks>
        /// 两件事合成一个门禁：受击期间的"该被推成什么样"（强制速度），
        /// 与减速修饰的"这一帧速度乘多少"（速度乘数）。<b>受击时不带乘数</b>：
        /// 外力滑停不该被地面减速拖短。
        /// </remarks>
        public MoveGates Gates
        {
            get
            {
                if (Current == StatusStateTag.Hurt) return MoveGates.Forced(_hurt.ForcedVelocity);

                float scale = SlowScale;

                return scale < 1f ? MoveGates.Scaled(scale) : MoveGates.None;
            }
        }

        /// <summary>是否正处于受击中（视效用它决定闪不闪）。</summary>
        public bool IsHurt => Current == StatusStateTag.Hurt;

        /// <summary>推进一个物理帧。</summary>
        /// <param name="pendingKnockback">帧外挂起的击退冲量（速度向量）；零表示没有。</param>
        public void Tick(in LogicContext ctx, Vector2 pendingKnockback)
        {
            // 修饰的计时走 Δt：暂停时 dt = 0 ⇒ 不过期（"暂停 = 时间冻结"自动成立）。
            if (_slowRemaining > 0f) _slowRemaining -= ctx.deltaTime;

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
