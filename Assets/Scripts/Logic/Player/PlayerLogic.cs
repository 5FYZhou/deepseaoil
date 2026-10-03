using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation;
using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家逻辑：持有冲刺余额与计时，回答"有没有资格"，并驱动移动状态组。
    /// </summary>
    /// <remarks>
    /// 不做状态转移决策、不知道任何具体状态类；速度与外力的写入口见基类 <see cref="ActorLogic"/>。
    /// 俯视角下跳跃/二段跳/蹬墙跳已移除（相应状态类也不存在），保留的是冲刺——
    /// 它的"资格"由冷却 ＋ <see cref="InputBuffer"/> 窗口共同决定，是状态组唯一会查询的余额。
    /// 外力：玩家<b>不主动施加外力</b>（<c>ApplyExtraForce</c> 无调用），击退/水流/吸附等由施加方决定，
    /// 需要时在 <see cref="OnTick"/> 里加一行即可。
    /// </remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerConfig _player;
        private readonly InputBuffer _buffer;
        private readonly MoveGroup _moveGroup;

        private float _lastDashAt = float.NegativeInfinity;

        /// <summary>最近一次非零输入方向（<b>已归一化</b>）；零输入时保持不变，供冲刺取向与后续技能使用。</summary>
        /// <remarks>
        /// 初始值是 <c>Vector2.right</c>：尚未有任何输入时按"朝右"冲刺，而不是把方向判成零向量
        /// （零向量会让 <c>DashState.Configure</c> 保持它自己的初值，行为不直观）。
        /// 存归一化值而不是原始输入：本属性的消费者（冲刺取向、后续技能）要的是"方向"，
        /// 把"归一化"留给每个消费者各做一次，迟早会漏掉一处。
        /// </remarks>
        private Vector2 _direction = Vector2.right;

        public PlayerLogic(IMovementMotor motor, PlayerConfig config, InputBuffer buffer) : base(motor, config)
        {
            _player = config;
            _buffer = buffer;
            _moveGroup = new MoveGroup(this);
        }

        /// <summary>当前移动状态。</summary>
        public MovementStateTag CurrentState => _moveGroup.Current;

        /// <summary>移动状态组，供调试面板与测试查看状态实例（只读用途）。</summary>
        public MoveGroup MoveGroup => _moveGroup;

        /// <summary>最近一次非零输入方向（已归一化）；供冲刺取向与调试面板使用。</summary>
        public Vector2 Direction => _direction;

        /// <summary>
        /// 满速档位：配置速度与冲刺速度里的较大者。
        /// </summary>
        /// <remarks>
        /// <b>它是"受击速度上限"的基准</b>：外因（击退、水流、吸附）只被允许把速度往"当前档位"里补，
        /// 补满为止，不允许把角色推到比它自己能力更快。于是"连续挨打会不会越推越快"有了确定答案。
        /// <para>返回的是只读派生值、不是缓存字段：缓存了就要在每个写入速度的地方同步它，
        /// 而那正是"两个速度真值"的开端。</para>
        /// </remarks>
        public float MaximumSpeed => Mathf.Max(Config.moveSpeed, _player.dashSpeed);

        /// <summary>
        /// 累加一次冲量（一次性的速度变化，单位/秒）—— <b>本类仍是玩家速度的唯一写者</b>。
        /// </summary>
        /// <remarks>
        /// 基类的方法是 <c>protected</c>，这里用 <c>new</c> 提升成公开访问点（首个消费者是敌人接触击退）。
        /// 调用方<b>不要</b>因此去写 <c>Rigidbody2D.velocity</c>：速度必须经本类账本，
        /// 否则帧末写出会覆盖掉外力，表现为"被推了一下又弹回去"。
        /// <para>冲量本身不乘 Δt（它是速度变化量）；本帧的提交由下一次固定帧一次写出。
        /// 若同帧还要限制上限，顺序必须是"先累加冲量、后限速"——
        /// 限速按当帧速度整体覆盖，顺序反了冲量会被整个吃掉且不报错。</para>
        /// <para><b>用 <c>new</c> 而不是包一层方法：</b>包一层会让两个同签名成员同时存在，
        /// 编译期报 CS0108（隐藏继承成员），而"隐藏"这件事本身是缺陷的信号 ——
        /// 将来基类给 <c>AddImpulse</c> 加上参数或改变语义时，这里的覆盖会静默失效。</para>
        /// </remarks>
        public new void AddImpulse(Vector2 deltaVelocity)
        {
            base.AddImpulse(deltaVelocity);
        }

        /// <summary>
        /// 把角色瞬移到给定位置（重生 / 传送用）。
        /// </summary>
        /// <remarks>
        /// 走执行器的物理体位置而不是 <c>transform.position</c>：后者会被刚体的位置积分覆盖掉，
        /// 表现为"瞬移了一下又弹回去"。它<b>不</b>是"移动"：不经过状态机、不改朝向、不产生提交，
        /// 所以想真正停住要另调 <see cref="ActorLogic.StopMove"/>。
        /// </remarks>
        public void ResetTo(Vector2 position)
        {
            Motor.SetPosition(position);
        }

        /// <summary>是否可冲刺：冷却已过，且缓冲里有窗口内的按下。纯查询，不消费。</summary>
        public bool CanDash(float now)
        {
            return CanDashNow(now) && _buffer.CanConsume(InputType.Dash, now, _player.dashBufferTime);
        }

        /// <summary>消费冲刺缓冲；冷却不足时拒绝。</summary>
        public bool TryConsumeDash(float now)
        {
            if (!CanDashNow(now)) return false;
            if (!_buffer.TryConsume(InputType.Dash, now, _player.dashBufferTime)) return false;

            _lastDashAt = now;
            return true;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // 此处是外力唯一入口，但玩家**刻意不施外力**（零惯性 ＋ 无重力）：
            // 这不是"还没写"，是设计意图——第一个真实消费者预计是敌人、击退、水流或吸附。
            // 要用时形如 ApplyExtraForce(in ctx, new Vector2(knockbackX, knockbackY)); 再按需 ClampSpeed(...)，
            // 注意顺序必须先外力后钳制（钳制会整体覆盖当帧速度）。

            Vector2 move = ctx.inputSnapshot.Move;
            if (move.sqrMagnitude > 0f) _direction = move.normalized;

            _moveGroup.Tick(in ctx);
        }

        /// <summary>冲刺冷却是否已过。</summary>
        /// <remarks>
        /// 平台跳跃时代的条件还含"在地面 或 空中余额未用完"；俯视角没有明确的空中/地面之分，
        /// 故只按冷却。若将来要做"空中只能冲一次"，需要先引入 Airborne 语义（见 Docs 待确认项）。
        /// </remarks>
        private bool CanDashNow(float now)
        {
            return now - _lastDashAt >= _player.dashCooldown;
        }
    }
}
