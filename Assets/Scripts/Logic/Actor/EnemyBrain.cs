using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人的<b>大脑</b>：吃"世界信息（自己在哪、目标在哪）"，吐一帧意图（<see cref="EnemyIntent"/>）。</summary>
    /// <remarks>它不查世界：目标位置、自身位置、减速系数都由调用方（<c>EnemyLogic</c>）组装成 <see cref="Context"/> 喂进来 —— 决策要能在 EditMode 里喂假数据复现。</remarks>
    public sealed class EnemyBrain
    {
        /// <summary>一帧的世界信息（由 <c>EnemyLogic</c> 组装）。</summary>
        public readonly struct Context
        {
            public readonly Vector2 Self;

            /// <summary>目标（玩家）位置；<see cref="HasTarget"/> 为假时无意义。</summary>
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

        /// <summary>本帧意图（<see cref="Decide"/> 的产物）；由大脑自己持有，消费者直接来取。</summary>
        public EnemyIntent Intent { get; private set; }

        public Steering LastSteering { get; private set; }

        public EnemyBrain(EnemySpec spec)
        {
            _enemy = spec;
            Intent = EnemyIntent.Idle;
        }

        /// <summary>算一帧意图。<b>纯函数式</b>：只读参数与自己的表值，不持任何跨帧状态。</summary>
        /// <returns>方向与目标速度；不该动时两者都表示"不动"。</returns>
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
