using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一个格子在一次 Tick / 一次状态切换里能看到的全部上下文：格子身份、时间、以及两个端口。</summary>
    /// <remarks>时间由驱动方给（状态自己不读 <c>Time</c>），于是整条时序能在 EditMode 里喂 <c>dt</c> 复现；暂停时 <see cref="DeltaTime"/> 为 <c>0</c>，累加自然冻结。</remarks>
    public readonly struct TileContext
    {
        public readonly Vector3Int Cell;

        public readonly float Now;

        /// <summary>本帧时长（秒）；暂停时为 0。</summary>
        public readonly float DeltaTime;

        /// <summary>状态提交口（下一次 Tick 与状态转换）。</summary>
        public readonly ITileScheduler Scheduler;

        /// <summary>唯一的效果出口；可为 <c>null</c>（逻辑层单跑测试的场合）。</summary>
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

    /// <summary>状态提交口：下一次 Tick 与状态转换。Tick 请求进双缓冲队列（本帧提交、下帧消费），转换进待处理表、本帧 Tick 循环跑完后统一结算 —— 同帧递归不可能发生。</summary>
    public interface ITileScheduler
    {
        void ScheduleTick(Vector3Int cell);

        void Transition(Vector3Int cell, TileStateType next);
    }

    /// <summary><b>唯一的效果出口</b>：对站在该格上的目标施加一次效果；找人与击退方向（按"格心 → 受害者"逐个算，可能不止一个目标）由实现负责。</summary>
    /// <remarks>签名只收一个<b>已定值</b>的 <see cref="TileEffectValue"/>：档位 / 级别在数据层就消解掉了，所以本接口不会随效果数量增长。</remarks>
    public interface ITileResolver
    {
        /// <summary>对站在该格上的目标施加一次效果（伤害 / 减速 / 击退 / 麻痹 / 持续伤害）。</summary>
        void Apply(Vector3Int cell, in TileEffectValue effect);

        /// <summary>
        /// <b>地形改写通道</b>（D11）：改的是格子<b>自身</b>，不是格上的目标 —— 温湿度继承、清除植物、状态转换都走这里。
        /// </summary>
        /// <remarks>与 <see cref="Apply"/> 分成两个方法而不是一个：一个动"格上的东西"，一个动"格子本身"，混成一个会让"这条效果改了谁"无法从签名上看出来（D10 的出口原则是"靠 Kind 区分种类"，不是"靠 Kind 区分作用对象"）。
        /// 状态实现<b>仍然不许直接碰别的格</b>（D4）：跨格由执行者做，本方法只作用于 <paramref name="cell"/>。</remarks>
        void ApplyToCell(Vector3Int cell, in TileEffectValue effect);

        /// <summary>直接改写某格的元素四件（温湿度继承 / 清除植物 / 将来的地形脚本用）。端口面比 <see cref="ApplyToCell"/> 更窄：它不谈"效果"，只写值。</summary>
        void SetCellElement(Vector3Int cell, in ElementValue element);
    }

    /// <summary>「会被减速修饰影响」的目标；当前只有敌人实现（格子系统完全不认识玩家，D7）。</summary>
    public interface ISlowable
    {
        /// <summary>续一次减速修饰；<b>不是</b>"立刻把速度乘一下"，修饰由目标的状态效果层持有。<b>不是</b>帧式 <c>SetSlowMultiplier</c>：离开格子不再续命即自动过期。</summary>
        void ApplySlow(float speedScale, float seconds);
    }
}
