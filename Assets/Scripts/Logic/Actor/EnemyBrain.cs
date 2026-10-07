using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的<b>大脑</b>：吃"世界信息（自己在哪、目标在哪、这一帧多慢）"，吐一帧意图
    /// （<see cref="EnemyIntent"/>）。
    /// </summary>
    /// <remarks>
    /// <b>它独立于身体</b>：敌人的身体（账本 ＋ 状态组）与玩家同源，差别只在"输入从哪来" ——
    /// 玩家的输入来自 <c>InputBuffer</c>，敌人的输入来自本类。于是"敌人怎么想"只有这一个文件，
    /// 换 AI 不动身体。
    /// <para><b>它不查世界</b>：目标位置、自身位置、减速系数都由调用方（<c>EnemyLogic</c>）
    /// 组装成 <see cref="Context"/> 喂进来。理由与"逻辑层零引擎类型"同源 ——
    /// 决策要能在 EditMode 里喂假数据复现。</para>
    /// <para><b>最简 AI（审查已定）：直线走向玩家。</b>"跑得够远能脱离"与"贴太近不互相挤"
    /// 两条判据在 <c>Steering.Resolve</c> 里，数值来自表值。</para>
    /// </remarks>
    public sealed class EnemyBrain
    {
        /// <summary>一帧的世界信息（由 <c>EnemyLogic</c> 组装）。</summary>
        public readonly struct Context
        {
            /// <summary>自己的位置。</summary>
            public readonly Vector2 Self;

            /// <summary>目标（玩家）位置；<see cref="HasTarget"/> 为假时无意义。</summary>
            public readonly Vector2 Target;

            /// <summary>这一帧有没有目标。</summary>
            public readonly bool HasTarget;

            public Context(Vector2 self, Vector2 target, bool hasTarget)
            {
                Self = self;
                Target = target;
                HasTarget = hasTarget;
            }

            /// <summary>没有目标（玩家不见了 / 已死 / 场景里没有玩家）。</summary>
            public static Context WithoutTarget(Vector2 self)
            {
                return new Context(self, default, false);
            }
        }

        private readonly EnemySpec _enemy;

        /// <summary>本帧意图（<see cref="Decide"/> 的产物）。<b>由大脑自己持有</b>，消费者直接来取。</summary>
        /// <remarks>
        /// 收口前它缓存在 <c>EnemyLogic</c> 上再由一个转发属性出去 —— 那是"数据持有者"与"转发者"
        /// 分居两处的典型形态：加一个消费者就要在逻辑层再开一个同名的口子。
        /// </remarks>
        public EnemyIntent Intent { get; private set; }

        /// <summary>本帧的转向产物（方向 ＋ 目标速度）。调试与测试用它看"大脑算出了什么"。</summary>
        /// <remarks>名字不叫 <c>Steering</c>：那会与类型 <see cref="Logic.Steering"/> 同名，
        /// 于是类内部所有 <c>Steering.Resolve(...)</c> 都会被解析成这个属性（编译期错误，且不容易一眼看懂）。</remarks>
        public Steering LastSteering { get; private set; }

        /// <param name="spec">敌人取值边界（追击范围 / 停止距离 / 速度）。</param>
        public EnemyBrain(EnemySpec spec)
        {
            _enemy = spec;
            Intent = EnemyIntent.Idle;
        }

        /// <summary>算一帧意图。<b>纯函数式</b>：只读参数与自己的表值，不持任何跨帧状态。</summary>
        /// <returns>方向与目标速度；不该动时两者都表示"不动"。</returns>
        /// <remarks>
        /// <b>这里曾经有一段"快停下来时重新压上去"的补救，已删：</b>它用<b>完全相同的实参</b>
        /// 再算一次同一个纯函数，而进入该分支的前提（<c>Speed &lt;= 0</c>）保证结果仍然是
        /// <c>Speed &lt;= 0</c> —— 那段代码从来没有执行过（它的注释却声称挡住了一个"被撞一次就黏住"
        /// 的自锁）。
        /// <para><b>真正撑住"击退之后还能重新贴上来"的是 <see cref="Steering.Resolve"/> 的几何：</b>
        /// 击退把人推出停止距离之后，距离判据本身就给回正的速度。这条不变量有测试钉住
        /// （<c>G10_受击滑停结束后必须还能重新贴上来</c>）。</para>
        /// </remarks>
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
