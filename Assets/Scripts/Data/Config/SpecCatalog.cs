using System.Collections.Generic;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 玩法数值的<b>唯一折算点</b>：把 Luban 生成的行转成纯数据结构体，供逻辑层与表现层消费。
    /// </summary>
    /// <remarks>
    /// <b>为什么不让消费者直接读 <c>ConfigModule.Tables.TbXxx</c>：</b>那样"表里加一列"的影响面
    /// 就是所有读它的地方，而生成类的字段名（<c>PascalCase</c>）与 JSON 键（<c>snake_case</c>）
    /// 会在十几个文件里各出现一次。收在这里之后，加列的改动面 = 一个结构体 + 一个方法。
    /// <para><b>这里不做缓存：</b>表是进程级常驻的只读对象，一次查询就是一次字典查找 + 造一个值类型，
    /// 缓存反而要多维护一份状态（还要处理 ConfigModule 不可重置这件事）。
    /// 唯一按需构造成列表的是 <see cref="TileInitials"/>，因为它只在开局读一次。</para>
    /// <para><b>前置条件：</b>所有查询都要求 <c>ConfigModule</c> 已 Init（由 <c>GameRoot.Awake</c> 完成）。
    /// 未 Init 时 <c>ConfigModule.Tables</c> 会抛 <c>InvalidOperationException</c> —— 这是刻意的：
    /// 带病数据不进运行时，也不要静默返回零值让玩法"看起来能跑但全是 0"。</para>
    /// <para><b>行缺失时不静默兜底：</b><c>TbXxx.Get</c> 在键不存在时抛 <c>KeyNotFoundException</c>。
    /// 这里不 catch —— 表里少一行属于配置事故，应该在启动时就炸出来，而不是让敌人以速度 0 待机。</para>
    /// </remarks>
    public static class SpecCatalog
    {
        /// <summary>默认敌人编号（表主键）。</summary>
        public const int DefaultEnemyId = 1;

        /// <summary>默认玩家编号（表主键）。</summary>
        public const int DefaultPlayerId = 1;

        /// <summary>默认波次编号（表主键）。</summary>
        public const int DefaultWaveId = 1;

        /// <summary>读一个球种。</summary>
        public static BallSpec Ball(BallType type)
        {
            Projectile row = ConfigModule.Tables.TbProjectile.Get(type);

            var throwSpec = new ThrowSpec(
                row.FlightDuration,
                row.MaxHeight,
                row.MaxThrowDistance,
                row.MinThrowDistance);

            var elementSpec = new ElementSpec(
                row.Type,
                row.Tags,
                row.Temp,
                row.Wet,
                row.Conductive);

            return new BallSpec(row.Id, row.Name, in throwSpec, in elementSpec);
        }

        /// <summary>全部球种（按表顺序）。</summary>
        public static IReadOnlyList<BallSpec> AllBalls()
        {
            IReadOnlyList<Projectile> rows = ConfigModule.Tables.TbProjectile.DataList;

            var result = new List<BallSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                Projectile row = rows[i];

                var throwSpec = new ThrowSpec(
                    row.FlightDuration,
                    row.MaxHeight,
                    row.MaxThrowDistance,
                    row.MinThrowDistance);

                var elementSpec = new ElementSpec(
                    row.Type,
                    row.Tags,
                    row.Temp,
                    row.Wet,
                    row.Conductive);

                result.Add(new BallSpec(row.Id, row.Name, in throwSpec, in elementSpec));
            }

            return result;
        }

        /// <summary>读一个格子状态。</summary>
        public static TileStateSpec TileState(TileStateType id)
        {
            cfg.demo.TileState row = ConfigModule.Tables.TbTileState.Get(id);

            var element = new ElementSpec(
                ElementType.Environment,
                row.Tags,
                row.Temp,
                row.Wet,
                row.Cond);

            var effectInfo = new TileEffectInfoSpec(
                row.Effects,
                row.EffectValuePos);

            return new TileStateSpec(
                row.Id,
                row.Name,
                1f,
                row.Duration,
                1f,
                0.65f,
                row.WillSpread,
                row.CanReact,
                element,
                effectInfo,
                row.Icon);
        }

        /// <summary>全部格子状态（按表顺序）。</summary>
        public static IReadOnlyList<TileStateSpec> AllTileStates()
        {
            IReadOnlyList<cfg.demo.TileState> rows = ConfigModule.Tables.TbTileState.DataList;

            var result = new List<TileStateSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                cfg.demo.TileState row = rows[i];

                var element = new ElementSpec(
                ElementType.Environment,
                row.Tags,
                row.Temp,
                row.Wet,
                row.Cond);

                var effectInfo = new TileEffectInfoSpec(
                    row.Effects,
                    row.EffectValuePos);

                result.Add(new TileStateSpec(
                    row.Id,
                    row.Name,
                    1f,
                    row.Duration,
                    1f,
                    0.65f,
                    row.WillSpread,
                    row.CanReact,
                    element,
                    effectInfo,
                    row.Icon));
            }

            return result;
        }


        /// <summary>全部元素反应规则（按表顺序）。 </summary>
        public static IReadOnlyList<ElementRuleSpec> AllElementRules()
        {
            IReadOnlyList<cfg.demo.ElementRule> rows = ConfigModule.Tables.TbElementRule.DataList;

            var result = new List<ElementRuleSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                cfg.demo.ElementRule row = rows[i];

                var effectInfo = new TileEffectInfoSpec(
                    row.Effects,
                    row.EffectValuePos);

                result.Add(new ElementRuleSpec(
                    row.Priority,
                    row.RequireTag,
                    row.ExcludeTag,
                    row.RequireTempMin,
                    row.RequireTempMax,
                    row.RequireWetMin,
                    row.RequireWetMax,
                    row.RequireCondMin,
                    row.ResultId,
                    effectInfo));

            }

            return result;
        }

        
        /// <summary>全部效果（按表顺序）。 </summary>
        public static IReadOnlyList<TileEffectSpec> AllTileEffects()
        {
            IReadOnlyList<cfg.demo.TileEffect> rows = ConfigModule.Tables.TbTileEffect.DataList;

            var result = new List<TileEffectSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                cfg.demo.TileEffect row = rows[i];

                result.Add(new TileEffectSpec(
                    row.Id,
                    row.Name,
                    row.Value1,
                    row.Value2,
                    row.Interval,
                    row.Flag));
            }
            return result;
        }


        /// <summary>读一个敌人种类。</summary>
        public static EnemySpec Enemy(int id = DefaultEnemyId)
        {
            Enemy row = ConfigModule.Tables.TbEnemy.Get(id);

            return new EnemySpec(
                row.Id,
                row.Name,
                row.Radius,
                row.MaxSpeed,
                row.Acceleration,
                row.KnockbackDecay,
                row.StopDistance,
                row.ChaseRange,
                row.StunSeconds,
                row.Hp,
                row.FlashHz);
        }

        /// <summary>读玩家数值。</summary>
        public static PlayerSpec Player(int id = DefaultPlayerId)
        {
            cfg.demo.Player row = ConfigModule.Tables.TbPlayer.Get(id);

            return new PlayerSpec(
                row.Id,
                row.Name,
                row.MaxHp,
                row.ContactDamage,
                row.InvulnerableDuration,
                row.RetryDelay,
                row.AttackInterval,
                row.KnockbackImpulse,
                row.KnockbackSpeedLimit,
                row.ContactRadius);
        }

        /// <summary>读波次数值。</summary>
        public static WaveSpec Wave(int id = DefaultWaveId)
        {
            cfg.demo.Wave row = ConfigModule.Tables.TbWave.Get(id);

            return new WaveSpec(
                row.Id,
                row.Name,
                row.EnemiesPerWave,
                row.SpawnInterval,
                row.InitialDelay,
                row.RespawnDelay,
                row.SpawnRadius);
        }

        /// <summary>全部关卡初始格子状态。</summary>
        public static List<TileInitialSpec> TileInitials()
        {
            IReadOnlyList<TileInitial> rows = ConfigModule.Tables.TbTileInitial.DataList;

            var result = new List<TileInitialSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                TileInitial row = rows[i];

                result.Add(new TileInitialSpec(row.CellX, row.CellY, row.StateId));
            }

            return result;
        }
    }
}
