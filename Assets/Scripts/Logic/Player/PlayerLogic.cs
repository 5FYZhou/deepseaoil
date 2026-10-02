using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation;
using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家逻辑：持有跳跃/冲刺的余额与计时，回答"有没有资格"，并驱动移动状态组。
    /// </summary>
    /// <remarks>不做状态转移决策、不知道任何具体状态类；速度与重力操作见基类 <see cref="ActorLogic"/>。</remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerConfig _player;
        private readonly InputBuffer _buffer;
        private readonly MoveGroup _moveGroup;

        private float _lastGroundedAt = float.NegativeInfinity;
        private float _lastDashAt = float.NegativeInfinity;
        private int _airJumpsUsed;
        private int _airDashesUsed;

        public PlayerLogic(IMovementMotor motor, PlayerConfig config, InputBuffer buffer) : base(motor, config)
        {
            _player = config;
            _buffer = buffer;
            _moveGroup = new MoveGroup(this);
        }

        /// <summary>当前移动状态。</summary>
        public MovementStateTag CurrentState => _moveGroup.Current;

        /// <summary>是否可起跳：在地面，或仍在土狼窗口内。</summary>
        public bool CanGroundJump(float now)
        {
            return IsGrounded || now - _lastGroundedAt <= _player.coyoteTime;
        }

        /// <summary>是否还有空中跳跃余额。</summary>
        public bool CanDoubleJump => _airJumpsUsed < _player.maxAirJumps;

        /// <summary>缓冲里是否有窗口内的跳跃按下。纯查询，不消费。</summary>
        public bool HasBufferedJump(float now)
        {
            return _buffer.CanConsume(InputType.Jump, now, _player.jumpBufferTime);
        }

        /// <summary>缓冲里的按下是否会按"地面跳"消费（决定是否要预扣土狼时间）。纯查询，不消费。</summary>
        public bool WouldJumpFromGround(float now)
        {
            return CanGroundJump(now) && HasBufferedJump(now);
        }

        /// <summary>是否可冲刺：冷却与空中余额都够，且缓冲里有按下。纯查询，不消费。</summary>
        public bool CanDash(float now)
        {
            return CanDashNow(now) && _buffer.CanConsume(InputType.Dash, now, _player.dashBufferTime);
        }

        /// <summary>消费跳跃缓冲；<paramref name="airJump"/> 为真时占用一次空中跳跃余额。</summary>
        public override bool TryConsumeJump(float now, bool airJump)
        {
            if (!_buffer.TryConsume(InputType.Jump, now, _player.jumpBufferTime)) return false;

            if (airJump) _airJumpsUsed++;
            else _lastGroundedAt = float.NegativeInfinity; // 预扣土狼时间，否则一次按下能连跳两次
            // 播放跳跃音效
            AudioManager.Instance.PlaySfx(AudioId.Jump);
            return true;
        }

        /// <summary>消费冲刺缓冲；冷却与空中余额不足时拒绝。</summary>
        public bool TryConsumeDash(float now)
        {
            if (!CanDashNow(now)) return false;
            if (!_buffer.TryConsume(InputType.Dash, now, _player.dashBufferTime)) return false;

            _lastDashAt = now;
            if (!IsGrounded) _airDashesUsed++;
            return true;
        }

        /// <summary>贴墙时走一次蹬墙跳：给一次斜向初速并锁定水平输入。</summary>
        public override bool TryWallJump(in LogicContext ctx)
        {
            if (!TryConsumeJump(ctx.now, airJump: false)) return false;

            DoWallJump(ctx.worldInfo.WallSide);
            StartMoveLock(ctx.now, _player.wallJumpLockTime);
            return true;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // 重力与状态各管一个分量，顺序本不敏感；但垂直钳制必须看到含重力的值，故重力仍先提交。
            ApplyGravity(in ctx, _buffer.IsJumpReleased());

            if (ctx.worldInfo.Grounded)
            {
                _airJumpsUsed = 0;
                _airDashesUsed = 0;
                _lastGroundedAt = ctx.now;
            }

            _moveGroup.Tick(in ctx);

            //MoveHorizontal(ctx.inputSnapshot.Move.x, Config.moveSpeed, Config.moveAcceleration);
        }

        /// <summary>蹬墙跳：按墙的相反方向给一次斜向速度。</summary>
        private void DoWallJump(int wallSide)
        {
            SnapVelocity(new Vector2(_player.wallJumpSpeedX * -wallSide, _player.wallJumpSpeedY));
        }

        private bool CanDashNow(float now)
        {
            if (now - _lastDashAt < _player.dashCooldown) return false;

            return IsGrounded || _airDashesUsed < _player.maxAirDashes;
        }
    }
}
