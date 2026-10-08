using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一次 Tick/状态切换可见的上下文：格子身份、时间、两个端口；时间由驱动方给，状态不读 Time，暂停时 DeltaTime 为 0 冻结</summary>
    public readonly struct TileContext
    {
        public readonly Vector3Int Cell;

        public readonly float Now;

        /// <summary>本帧时长（秒），暂停时为 0</summary>
        public readonly float DeltaTime;

        public readonly ITileScheduler Scheduler;

        /// <summary>唯一效果出口；可为 null（逻辑层单跑测试）</summary>
        public readonly ITileResolver Resolver;

        public TileContext(
            Vector3Int cell,
            float now,
            float deltaTime,
            ITileScheduler scheduler,
            ITileResolver resolver)
        {
            Cell = cell;
            Now = now;
            DeltaTime = deltaTime;
            Scheduler = scheduler;
            Resolver = resolver;
        }
    }

    /// <summary>状态提交口：Tick 请求进双缓冲队列（本帧提交下帧消费），转换进待处理表、Tick 循环后统一结算</summary>
    public interface ITileScheduler
    {
        void ScheduleTick(Vector3Int cell);

        void Transition(Vector3Int cell, TileStateType next);
    }

    /// <summary>唯一效果出口：对格上目标施加一次效果，找人与击退由实现负责；只收已定值的 TileEffectValue，档位/级别在数据层已消解</summary>
    public interface ITileResolver
    {
        void Apply(Vector3Int cell, in TileEffectValue effect);

        /// <summary>地形改写通道：改格子自身而非格上目标；温湿度继承、清除植物、状态转换都走这里；与 Apply 分开，状态不许直接碰别的格，跨格由执行者做</summary>
        void ApplyToCell(Vector3Int cell, in TileEffectValue effect);

        /// <summary>直接改写某格元素四件；比 ApplyToCell 更窄，只写值</summary>
        void SetCellElement(Vector3Int cell, in ElementValue element);
    }

    /// <summary>会被减速修饰影响的目标；当前只有敌人实现</summary>
    public interface ISlowable
    {
        /// <summary>续一次减速修饰，由目标的状态效果层持有；不续命即过期</summary>
        void ApplySlow(float speedScale, float seconds);
    }
}
