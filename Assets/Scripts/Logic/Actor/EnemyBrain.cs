using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic
{
    public interface IEnemyBrain
    {
        EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    /// <remarks>无目标走 WithoutTarget</remarks>
    public readonly struct EnemyBrainContext
    {
        public readonly Vector2 Self;

        public readonly Vector2 Target;

        public readonly bool HasTarget;

        public EnemyBrainContext(Vector2 self, Vector2 target, bool hasTarget)
        {
            Self = self;
            Target = target;
            HasTarget = hasTarget;
        }

        public static EnemyBrainContext WithoutTarget(Vector2 self)
        {
            return new EnemyBrainContext(self, default, false);
        }
    }

    public sealed class ChaseBrain : IEnemyBrain
    {
        /// <summary>停靠环外沿滞回（世界单位）：环上 stop/start 会变成抖动</summary>
        private const float StopHysteresis = 0.15f;

        private readonly EnemySpec _enemy;
        private bool _stopped;

        public ChaseBrain(EnemySpec spec)
        {
            _enemy = spec;
        }

        public EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (!ctx.HasTarget)
            {
                _stopped = false;

                return EnemyIntent.Idle;
            }

            float distance = (ctx.Target - ctx.Self).magnitude;

            if (distance <= _enemy.StopDistance)
            {
                _stopped = true;

                return EnemyIntent.Idle;
            }

            if (_stopped && distance <= _enemy.StopDistance + StopHysteresis) return EnemyIntent.Idle;

            _stopped = false;

            Steering steering = Steering.Resolve(
                ctx.Self,
                ctx.Target,
                _enemy.StopDistance,
                _enemy.MaxSpeed,
                _enemy.Config.moveAcceleration);

            return new EnemyIntent(steering.Direction, steering.Speed);
        }
    }

    /// 打靶木桩：恒零速度
    public sealed class StandBrain : IEnemyBrain
    {
        public EnemyIntent Decide(in EnemyBrainContext ctx) => EnemyIntent.Idle;
    }

    public static class EnemyBrainFactory
    {
        public static IEnemyBrain Create(in EnemySpec spec)
        {
            return new ChaseBrain(spec);
        }

        public static IEnemyBrain CreateStand()
        {
            return new StandBrain();
        }
    }
}
