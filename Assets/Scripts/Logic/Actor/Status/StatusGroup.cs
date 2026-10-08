using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>受击/硬直这类角色效果的状态层，玩家与敌人共用</summary>
    /// <remarks>门禁串行第一层：本层→战斗层→移动层，写速度的权限只有移动层有。帧外递交的击退只能挂起，本层下一帧开头消费成一次状态进入。</remarks>
    public sealed class StatusGroup : StateGroup<StatusStateTag, LogicContext>
    {
        private readonly HurtState _hurt;

        private float _slowScale = 1f;

        private float _slowRemaining;

        /// <summary>本拍是否刚被续过一次减速：跨帧续命靠它活过当拍（见 Tick）</summary>
        private bool _slowRenewed;

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

        /// <remarks>续一次减速修饰（格子执行者按格施加）；速度乘数 1=不减速、非数按 1 处理，seconds≤0 忽略</remarks>
        public void ApplySlow(float speedScale, float seconds)
        {
            if (seconds <= 0f) return;

            _slowScale = float.IsNaN(speedScale) ? 1f : Mathf.Clamp01(speedScale);
            _slowRemaining = seconds;
            _slowRenewed = true;
        }

        /// <remarks>受击时产出 Forced 门禁，不带乘数：外力滑停不该被地面减速拖短</remarks>
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

        public void Tick(in LogicContext ctx, Vector2 pendingKnockback)
        {
            // 刚续过的一拍不扣时：格上减速把秒数设成"本帧 Δt"（泥浆每帧提交一次），而扣时按物理 Δt，
            // 两者相等时恰好归零 ⇒ SlowScale 读成 1f，减速与减速色一起消失（编译器不拦、测试不红，只错手感）。
            if (_slowRenewed) _slowRenewed = false;
            else if (_slowRemaining > 0f) _slowRemaining -= ctx.deltaTime;

            EnterHurtIfPending(in ctx, pendingKnockback);

            TickStates(in ctx);
        }

        private void EnterHurtIfPending(in LogicContext ctx, Vector2 knockback)
        {
            // 逐分量精确比较：Vector2 的 != 带 1e-10 容差，小冲量会被吞掉
            if (knockback.x == 0f && knockback.y == 0f) return;

            _hurt.Configure(knockback);

            ChangeState(StatusStateTag.Hurt, in ctx);
        }
    }
}
