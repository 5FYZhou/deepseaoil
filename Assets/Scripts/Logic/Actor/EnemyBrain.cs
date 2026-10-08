using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人大脑，吃世界信息吐一帧意图；不查世界，位置与系数由 EnemyLogic 组装成 Context 喂入，可喂假数据复现</summary>
    public sealed class EnemyBrain
    {
        public readonly struct Context
        {
            public readonly Vector2 Self;

            public readonly Vector2 Target;

            public readonly bool HasTarget;

            public Context(Vector2 self, Vector2 target, bool hasTarget)
            {
                Self = self;
                Target = target;
                HasTarget = hasTarget;
            }

            public static Context WithoutTarget(Vector2 self)
            {
                return new Context(self, default, false);
            }
        }

        private readonly EnemySpec _enemy;

        public EnemyIntent Intent { get; private set; }

        public Steering LastSteering { get; private set; }

        public EnemyBrain(EnemySpec spec)
        {
            _enemy = spec;
            Intent = EnemyIntent.Idle;
        }

        public EnemyIntent Decide(in Context ctx)
        {
            if (!ctx.HasTarget)
            {
                LastSteering = default;
                Intent = EnemyIntent.Idle;

                return Intent;
            }

            LastSteering = Steering.Resolve(
                ctx.Self,
                ctx.Target,
                _enemy.StopDistance,
                _enemy.ChaseRange,
                _enemy.MaxSpeed);

            Intent = new EnemyIntent(LastSteering.Direction, LastSteering.Speed);

            return Intent;
        }
    }
}
