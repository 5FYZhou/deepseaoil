using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic.Movement.States
{
    /// <summary>
    /// 冲刺：进入时给一次定时恒速，到时即结束。
    /// </summary>
    /// <remarks>
    /// 俯视角语义：<b>沿朝向 8 向冲刺</b>，不是只沿 x 轴。方向由状态组在切换前喂入
    /// （见 <c>MoveGroup.TryCommitPreempt</c>：有输入用输入方向，无输入用最近朝向）。
    /// 进入时直接 <c>SnapVelocity</c> 接管两个分量，天然不受外力影响；重力已移除，
    /// 因此不需要"冲刺期间屏蔽重力"那类开关，<c>Exit</c> 也不必还原任何重力状态。
    /// </remarks>
    public sealed class DashState : TimedStateBase<MovementStateTag>
    {
        private Vector2 _direction = Vector2.up;

        public DashState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Dash;

        /// <summary>由状态组在切换前喂入方向（世界方向；给零向量表示保持上次方向）。</summary>
        /// <remarks>
        /// <b>归一化在本方法里做</b>：这是入场方向的唯一写入口，把"必须是单位向量"这条约束
        /// 收在一处，<see cref="Enter"/> 就不必防 √2 倍。宿主传未归一化的斜向 (1,1) 也只会得到
        /// 正确的 <c>dashSpeed</c>，而不是快 41% 的冲刺。
        /// </remarks>
        public void Configure(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            _direction = direction.normalized;
        }

        /// <summary>冲刺方向（已归一化），供调试面板显示。</summary>
        public Vector2 Direction => _direction;

        public override void Enter(LogicContext ctx)
        {
            base.Enter(ctx);

            Logic.SnapVelocity(_direction * Config.dashSpeed);   // 沿朝向 8 向，不再是固定 x 轴
        }

        /// <summary>离开冲刺：<b>无清理动作</b>。</summary>
        /// <remarks>
        /// 平台跳跃时代这里要 <c>SetGravity(true)</c> 还原 <c>Enter</c> 关掉的重力；重力已移除，
        /// 故本方法留空——但它是 <see cref="StateBase{TStateTag}"/> 的抽象成员，必须显式实现，
        /// 不能因为空就删掉。冲刺速度是靠 <c>SnapVelocity</c> 一次性接管的，退出去由下一个状态自己覆盖。
        /// </remarks>
        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
        }

        protected override float GetDuration(LogicContext ctx)
        {
            return Config.dashDuration;
        }
    }
}
