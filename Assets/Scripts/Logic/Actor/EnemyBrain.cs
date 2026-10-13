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

        public readonly bool HasAggro;

        public EnemyBrainContext(Vector2 self, Vector2 target, bool hasTarget, bool hasAggro = false)
        {
            Self = self;
            Target = target;
            HasTarget = hasTarget;
            HasAggro = hasAggro;
        }

        public static EnemyBrainContext WithoutTarget(Vector2 self)
        {
            return new EnemyBrainContext(self, default, false, false);
        }
    }

    public sealed class ChaseBrain : IEnemyBrain
    {
        private readonly EnemySpec _enemy;

        public ChaseBrain(EnemySpec spec)
        {
            _enemy = spec;
        }

        public EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            // 仇恨由 AggroState 给：删掉这个判据，追逐就不再受视野约束
            if (!ctx.HasTarget || !ctx.HasAggro) return EnemyIntent.Idle;

            Steering steering = Steering.Resolve(
                ctx.Self,
                ctx.Target,
                _enemy.StopDistance,
                _enemy.ChaseRange,
                _enemy.MaxSpeed);

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
