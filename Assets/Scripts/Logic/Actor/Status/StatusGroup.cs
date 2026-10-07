using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>受击 / 硬直这类"作用在角色身上的效果"的状态层：玩家与敌人共用。</summary>
    /// <remarks>门禁串行第一层：本层 → 战斗层 → 移动层；写速度的权限只有移动层有。帧外递交的击退只能挂起，由本层在下一帧开头消费成一次状态进入。帧序见 ActorLogic。</remarks>
    public sealed class StatusGroup : StateGroup<StatusStateTag, LogicContext>
    {
        private readonly HurtState _hurt;

        private float _slowScale = 1f;

        private float _slowRemaining;

        public StatusGroup(ActorLogic logic)
        {
            _hurt = new HurtState(logic);

            AddState(new NormalState(logic));
            AddState(_hurt);
        }

        protected override StatusStateTag EmptyTag => StatusStateTag.Empty;

        protected override StatusStateTag Fallback(in LogicContext ctx) => StatusStateTag.Normal;

        public HurtState Hurt => _hurt;

        public float SlowScale => _slowRemaining > 0f ? _slowScale : 1f;

        /// <param name="speedScale">速度乘数（<c>1</c> = 不减速，非数按 <c>1</c> 处理）。</param>
        /// <param name="seconds">存活时长（秒）；<c>≤ 0</c> 时忽略这次提交。</param>
        /// <remarks>续一次减速修饰，由格子的执行者按格施加：只在自己 Tick 时续，不续命即自然过期。非数进入速度会让角色带非数坐标消失，且不报错。</remarks>
        public void ApplySlow(float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            _slowScale = float.IsNaN(speedScale) ? 1f : Mathf.Clamp01(speedScale);
            _slowRemaining = seconds;
        }

        /// <remarks>受击时产出 Forced 门禁（速度被外力接管），不带乘数：外力滑停不该被地面减速拖短。</remarks>
        public MoveGates Gates
        {
            get
            {
                if (Current == StatusStateTag.Hurt) return MoveGates.Forced(_hurt.ForcedVelocity);

                float scale = SlowScale;

                return scale < 1f ? MoveGates.Scaled(scale) : MoveGates.None;
            }
        }

        public bool IsHurt => Current == StatusStateTag.Hurt;

        /// <param name="pendingKnockback">帧外挂起的击退冲量（速度向量）；零向量表示没有。</param>
        public void Tick(in LogicContext ctx, Vector2 pendingKnockback)
        {
            // Δt 计时：暂停时 dt = 0 ⇒ 修饰不过期（暂停 = 时间冻结）。
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
