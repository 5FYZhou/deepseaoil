using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 单类特效的驱动器。<c>EffectModule</c> 只负责按 <see cref="EffectId"/> 分发与生命周期，
    /// 不感知任何具体实现（粒子 / 材质 / Shader / 相机震动）。
    /// </summary>
    /// <remarks>
    /// <para><b>职责边界</b>：单例还是多实例、合并策略、池化、跟随目标检测、Tick 逻辑，
    /// <b>全部由实现类自己决定</b>。<c>EffectModule</c> 不判断 <see cref="IsSingleton"/>，
    /// <c>Stop</c> 也原样转发。</para>
    ///
    /// <para><b>EffectModule 对实现类的三条要求</b>：</para>
    /// <list type="number">
    /// <item><b>不抛异常</b>。所有失败（无资源、池满、预制体缺组件）只 LogError / LogWarning 并返回
    /// <see cref="EffectHandle.None"/>——特效是表现层的锦上添花，不得阻塞游戏。</item>
    /// <item><b>幂等 + 空跑安全</b>。<c>Tick</c> / <c>CleanAll</c> / <c>Dispose</c> 在没有实例、
    /// 没有资源时必须是 no-op。</item>
    /// <item><b>不漏对象</b>。借出的对象必须在到期、被 Stop、被 CleanAll 三条路径上都归还自己的池。</item>
    /// </list>
    ///
    /// <para><b>资源生命周期</b>：需要资源的驱动在构造时<b>不</b>拿资源，而是由
    /// <see cref="AssetKey"/> 声明要什么；<c>EffectModule</c> 统一做预加载（同步）与懒加载（异步），
    /// 拿到后调 <see cref="OnAssetLoaded"/>。<see cref="AssetKey"/> 为空字符串的驱动
    /// 视为「不需要资源」，<see cref="IsAssetReady"/> 应恒为 true。</para>
    ///
    /// <para><b>照抄模板</b>：新增驱动只需
    /// ① 实现本接口；② 在 <c>EffectDriverFactory.Create</c> 里加一个分支；
    /// ③ 在 <c>EffectCatalog</c> 里加一行。<c>EffectModule</c> 一行都不用改。</para>
    /// </remarks>
    public interface IEffectDriver
    {
        /// <summary>
        /// 是否单例型（同名特效同时只有一个实例，重复 <c>Play</c> 合并到当前实例）。
        /// <b>声明式</b>：<c>EffectModule</c> 不据此分支，仅用于调试面板展示与实现类自述。
        /// </summary>
        bool IsSingleton { get; }

        /// <summary>
        /// 需要的资源 Key（<c>AssetModule</c> 的 Key 约定，例如 <c>"effects/HitSpark"</c>）。
        /// 空字符串 = 不需要资源。
        /// </summary>
        string AssetKey { get; }

        /// <summary>资源是否已就位。false 时 <c>EffectModule.Play</c> 会走懒加载并返回 None。</summary>
        bool IsAssetReady { get; }

        /// <summary>
        /// 资源到位回调。<paramref name="asset"/> 可能为 null（加载失败）——实现类必须处理，
        /// 且此后 <see cref="IsAssetReady"/> 应返回 false 以免反复进入加载路径。
        /// </summary>
        void OnAssetLoaded(Object asset);

        /// <summary>播放一次。失败返回 <see cref="EffectHandle.None"/>。</summary>
        EffectHandle Play(EffectId id, in EffectContext ctx);

        /// <summary>
        /// 更新一次<b>已经在播</b>的实例（位置 / 颜色 / 半径）。
        /// </summary>
        /// <param name="handle">该次播放的句柄。</param>
        /// <param name="ctx">新的上下文（语义与 <see cref="Play"/> 一致）。</param>
        /// <remarks>
        /// <b>持续型特效的入口</b>（瞄准高亮、将来的引导线与范围指示）：创建一次、
        /// 之后每帧只更新 —— 每帧 <c>Play</c> 一次会每帧新建一个实例。
        /// <para><b>默认实现是空的</b>：多数特效"播一次就不管"，让它们各写一个空方法只是样板；
        /// 需要持续更新的驱动覆写它。句柄无效 / 不属于本驱动时必须是 no-op（与 <see cref="Stop"/> 同一条纪律）。</para>
        /// </remarks>
        void UpdateInstance(EffectHandle handle, in EffectContext ctx)
        {
        }

        /// <summary>提前停掉一次播放。句柄过期 / 不属于本驱动时必须是 no-op。</summary>
        void Stop(EffectHandle handle);

        /// <summary>清空全部活跃实例（全部归还池）。切场景与 Dispose 时调用。</summary>
        void CleanAll();

        /// <summary>每帧推进（回收到期实例、刷新跟随）。</summary>
        void Tick(float dt);

        /// <summary>归还资源 / 销毁池对象。由 <c>EffectModule.Dispose</c> 调用，之后本实例不再被使用。</summary>
        void Dispose();

        /// <summary>活跃实例数。调试统计用。</summary>
        int ActiveInstanceCount { get; }

        /// <summary>池中待用对象数。调试统计用。</summary>
        int PooledObjectCount { get; }
    }
}
