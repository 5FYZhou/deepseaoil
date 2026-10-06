using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 受击：速度被外力接管，输入暂时失效；<b>滑停到零就结束</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么它是状态而不是一个"击退计时器"：</b>判据来自审查 —— "冷却/受击期间有没有行为"。
    /// 受击期间确实有行为：不能自己走、速度由外力决定、退出条件是"滑停"。这些正是状态的语义。
    /// <para><b>为什么不写速度：</b>速度只有一个写者（移动层）。本状态只暴露
    /// <see cref="ForcedVelocity"/>，由状态组包成门禁交给移动层执行。</para>
    /// <para><b>退出条件是"速度归零"而不是"时长到"：</b>冲量大小会变（表值、将来的技能），
    /// 固定时长要么在被撞得轻时留一段"站着不动"的空转，要么在被撞得重时半路收回控制权。
    /// 用同一份加速度把速度滑到零，两种情况下手感一致。</para>
    /// <para><b>减速用 <c>moveAcceleration</c></b>（角色共用的运动参数，与走路加速同一个数）：
    /// 于是"被撞出去多远"= 冲量² / (2 × 加速度)，不需要为一个新机制再引一个调参字段。
    /// 填 0 表示零惯性配置：此时速度当帧归零，受击退化为"一帧的位移"。</para>
    /// </remarks>
    public sealed class HurtState : StateBase<StatusStateTag>
    {
        private Vector2 _direction = Vector2.up;
        private float _speed;

        /// <summary>本帧是不是"刚进入"的那一帧（那一帧不衰减）。</summary>
        private bool _justEntered;

        public HurtState(ActorLogic logic, CharacterConfig config) : base(logic, config)
        {
        }

        public override StatusStateTag StateTag => StatusStateTag.Hurt;

        /// <summary>要求移动层接管的速度（世界向量，单位/秒）。</summary>
        public Vector2 ForcedVelocity => _direction * _speed;

        /// <summary>剩余速度（诊断 / 测试读数）。</summary>
        public float RemainingSpeed => _speed;

        /// <summary>
        /// 由状态组在切换前喂入一次性冲量（速度向量，单位/秒）。
        /// </summary>
        /// <remarks>零向量表示"保持上次方向"——与 <c>DashState.Configure</c> 同一条纪律：
        /// 入场参数的写入口只有这一个。</remarks>
        public void Configure(Vector2 impulse)
        {
            if (impulse.sqrMagnitude <= 0f) return;

            _direction = impulse.normalized;
            _speed = impulse.magnitude;
        }

        public override void Enter(LogicContext ctx)
        {
            // 冲量是瞬时量：进入的那一帧速度就是冲量本身，不衰减。
            // 少了这一条，"被撞开的第一帧"会比冲量小一个 Δt 的量，而那个差额在低加速度下肉眼可见。
            _justEntered = true;
        }

        public override void Exit()
        {
        }

        public override void Tick(LogicContext ctx)
        {
            if (_justEntered)
            {
                _justEntered = false;
                return;
            }

            if (_speed <= 0f) return;

            float acceleration = Config.moveAcceleration;

            _speed = acceleration > 0f
                ? Mathf.Max(0f, _speed - acceleration * ctx.deltaTime)
                : 0f;
        }

        public override bool IsDone(LogicContext ctx)
        {
            return _speed <= 0f;
        }
    }
}
