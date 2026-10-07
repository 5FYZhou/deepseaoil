using DeepseaOil.Foundation;
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
    /// <para><b>计时为什么写在本类里</b>（收口前是 <c>TimedStateBase</c>）：那个基类只有本类一个子类，
    /// 而它的"读 <c>ctx.now</c>"需要上下文暴露时间 —— 为一个子类发明一个时间接口、
    /// 或者让骨架认识具体上下文，都不如把三行计时留在这里（同一判据见 MonoMgr 的取舍）。</para>
    /// </remarks>
    public sealed class DashState : StateBase<MovementStateTag, LogicContext>
    {
        private Vector2 _direction = Vector2.up;

        /// <summary>进入时刻（秒）与持续时间（秒）。</summary>
        private float _enteredAt;
        private float _duration;

        /// <param name="host">宿主（移动层账本）。</param>
        public DashState(IStateHost host) : base(host)
        {
        }

        public override MovementStateTag StateTag => MovementStateTag.Dash;

        /// <summary>由状态组在切换前喂入方向（世界方向；给零向量表示保持上次方向）。</summary>
        /// <remarks>
        /// <b>归一化在本方法里做</b>：这是入场方向的唯一写入口，把"必须是单位向量"这条约束
        /// 收在一处，<c>Enter</c> 就不必防 √2 倍。宿主传未归一化的斜向 (1,1) 也只会得到
        /// 正确的 <c>dashSpeed</c>，而不是快 41% 的冲刺。
        /// </remarks>
        public void Configure(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            _direction = direction.normalized;
        }

        /// <summary>
        /// 冲刺方向（已归一化）。
        /// </summary>
        /// <remarks>
        /// <b>唯一的读者是测试</b>（钉"入场方向被喂成输入方向"这条契约）；
        /// 调试面板读的是它前面那条 <c>MoveGroup.Dash</c>，不是本属性。
        /// 留着它是因为它是"配置进来了没有"唯一可断言的口，而不是为了显示。
        /// </remarks>
        public Vector2 Direction => _direction;

        public override void Enter(LogicContext ctx)
        {
            _enteredAt = ctx.now;
            _duration = Motion.DashDuration;

            Host.SnapVelocity(_direction * Motion.DashSpeed);   // 沿朝向 8 向，不再是固定 x 轴
        }

        /// <summary>离开冲刺：<b>无清理动作</b>。</summary>
        /// <remarks>
        /// 平台跳跃时代这里要 <c>SetGravity(true)</c> 还原 <c>Enter</c> 关掉的重力；重力已移除，
        /// 故本方法留空——但它是 <see cref="StateBase{TStateTag, TContext}"/> 的抽象成员，
        /// 必须显式实现，不能因为空就删掉。冲刺速度是靠 <c>SnapVelocity</c> 一次性接管的，
        /// 退出去由下一个状态自己覆盖。
        /// </remarks>
        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
        }

        public override bool IsDone(LogicContext ctx)
        {
            return ctx.now - _enteredAt >= _duration;
        }
    }
}
