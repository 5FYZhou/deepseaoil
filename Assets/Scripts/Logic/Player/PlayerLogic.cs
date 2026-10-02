using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
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
