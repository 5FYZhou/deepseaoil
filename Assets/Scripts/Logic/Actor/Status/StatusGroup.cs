using DeepseaOil.Foundation;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// 受击/硬直状态层，玩家与敌人共用
    /// 门禁串行第一层：本层→战斗层→移动层；帧外击退挂起，下一帧消费成状态进入
    public sealed class StatusGroup : StateGroup<StatusStateTag, LogicContext>
    {
        private readonly HurtState _hurt;

        /// 续命窗口兜底拍数：窗口短于消费者一拍就会被扣穿
        private const int SlowFloorTicks = 3;

        private float _slowScale = 1f;

        private float _slowRemaining;

        private float _slowGivenSeconds;

        private bool _slowRenewed;

        private float _stunRemaining;

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

        /// 减速修饰（格子按格施加）；乘数 1=不减速、非数按 1；seconds≤0=表里没填，按续命处理
        public void ApplySlow(float speedScale, float seconds)
        {
            _slowScale = float.IsNaN(speedScale) ? 1f : Mathf.Clamp01(speedScale);
            _slowGivenSeconds = seconds;
            _slowRenewed = true;
        }

        /// 麻痹取较长者
        public void ApplyStun(float seconds)
        {
            if (seconds <= 0f) return;

            if (seconds > _stunRemaining) _stunRemaining = seconds;
        }

        /// 门禁优先级：麻痹归零 > 受击推力 > 减速乘数；外力不叠减速乘数
        public MoveGates Gates
        {
            get
            {
                if (_stunRemaining > 0f) return MoveGates.Forced(Vector2.zero);

                if (Current == StatusStateTag.Hurt) return MoveGates.Forced(_hurt.ForcedVelocity);

                float scale = SlowScale;

                return scale < 1f ? MoveGates.Scaled(scale) : MoveGates.None;
            }
        }

        public bool IsHurt => Current == StatusStateTag.Hurt;

        public bool IsStunned => _stunRemaining > 0f;

        public void Tick(in LogicContext ctx, Vector2 pendingKnockback)
        {
            TickSlow(in ctx);

            if (_stunRemaining > 0f) _stunRemaining -= ctx.deltaTime;

            // 麻痹期间丢推力并退出硬直：否则解除帧补放成滑步
            if (_stunRemaining > 0f)
            {
                if (Current == StatusStateTag.Hurt) ChangeState(StatusStateTag.Normal, in ctx);

                TickStates(in ctx);

                return;
            }

            EnterHurtIfPending(in ctx, pendingKnockback);

            TickStates(in ctx);
        }

        /// 续命窗口来自渲染帧而扣时按物理 Δt，一拍即扣穿 ⇒ 兜底到 SlowFloorTicks 拍
        private void TickSlow(in LogicContext ctx)
        {
            if (_slowRenewed)
            {
                _slowRenewed = false;

                float window = Mathf.Max(_slowGivenSeconds, SlowFloorTicks * ctx.deltaTime);

                if (window > _slowRemaining) _slowRemaining = window;

                return;
            }

            if (_slowRemaining > 0f) _slowRemaining -= ctx.deltaTime;
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
