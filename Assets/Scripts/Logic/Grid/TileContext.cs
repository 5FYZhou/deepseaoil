using UnityEngine;
using cfg.demo;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Grid
{
    public readonly struct TileContext
    {
        public readonly Vector3Int Cell;

        public readonly float Now;

        /// <summary>本帧时长（秒）；暂停时为 0。</summary>
        public readonly float DeltaTime;

        public readonly ITileScheduler Scheduler;

<<<<<<< HEAD
        /// <summary>唯一的效果出口；可为 <c>null</c>（逻辑层单跑测试的场合）。</summary>
        public readonly ITileResolver Resolver;

        public TileContext(
            Vector3Int cell,
            float now,
            float deltaTime,
            ITileScheduler scheduler,
            ITileResolver resolver)
=======
        public TileContext(Vector3Int cell, float now, float deltaTime, ITileScheduler scheduler)
>>>>>>> main
        {
            Cell = cell;
            Now = now;
            DeltaTime = deltaTime;
            Scheduler = scheduler;
<<<<<<< HEAD
            Resolver = resolver;
=======
>>>>>>> main
        }
    }

    /// <summary>状态提交口：下一次 Tick 与状态转换。Tick 请求进双缓冲队列（本帧提交、下帧消费），转换进待处理表、本帧 Tick 循环跑完后统一结算 —— 同帧递归不可能发生。</summary>
    public interface ITileScheduler
    {
        void ScheduleTick(Vector3Int cell);

        void Transition(Vector3Int cell, TileStateType next);
    }

<<<<<<< HEAD
    public enum TileEffectKind
    {
        None = 0,

        Damage,

        Slow,
    }

    /// <summary>一次格子效果：<see cref="Kind"/> ＋ 该种类用到的槽位；只能经 <see cref="Damage"/> / <see cref="Slow"/> 工厂构造，读取时必须先看 Kind 再取字段。</summary>
    public readonly struct TileEffect
    {
        public readonly TileEffectKind Kind;

        /// <summary>伤害值（<see cref="TileEffectKind.Damage"/>）；<c>0</c> = 只击退。</summary>
        public readonly float Amount;

        /// <summary>击退冲量（<see cref="TileEffectKind.Damage"/>）；<c>0</c> = 不击退。</summary>
        public readonly float Knockback;

        public readonly DamageSource Source;

        /// <summary>速度乘数（<see cref="TileEffectKind.Slow"/>）；<c>1</c> = 不减速。</summary>
        public readonly float SpeedScale;

        public readonly float Seconds;

        private TileEffect(
            TileEffectKind kind,
            float amount,
            float knockback,
            DamageSource source,
            float speedScale,
            float seconds)
        {
            Kind = kind;
            Amount = amount;
            Knockback = knockback;
            Source = source;
            SpeedScale = speedScale;
            Seconds = seconds;
        }

        public static TileEffect Damage(float amount, float knockback, DamageSource source)
            => new TileEffect(TileEffectKind.Damage, amount, knockback, source, 1f, 0f);

        public static TileEffect Slow(float speedScale, float seconds)
            => new TileEffect(TileEffectKind.Slow, 0f, 0f, default, speedScale, seconds);
    }

    /// <summary><b>唯一的效果出口</b>：对站在该格上的目标施加一次效果；找人与击退方向（按"格心 → 受害者"逐个算，可能不止一个目标）由实现负责。</summary>
    public interface ITileResolver
    {
        void Apply(Vector3Int cell, in TileEffect effect);
    }

    /// <summary>「会被减速修饰影响」的目标；当前只有敌人实现（格子系统完全不认识玩家）。</summary>
    public interface ISlowEffectTarget
    {
        /// <summary>续一次减速修饰；<b>不是</b>"立刻把速度乘一下"，修饰由目标的状态效果层持有。</summary>
        void ApplySlow(float speedScale, float seconds);
    }
=======
>>>>>>> main
}
