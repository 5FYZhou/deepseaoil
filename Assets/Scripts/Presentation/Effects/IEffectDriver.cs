using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>实现纪律：① 不抛异常 —— 无资源 / 池满 / 预制体缺组件只 LogError / LogWarning 并返回 <see cref="EffectHandle.None"/>，特效不得阻塞游戏；② 空跑安全 —— <c>Tick</c>（每帧）/ <c>CleanAll</c>（切场景、Dispose 时）/ <c>Dispose</c> 在无实例、无资源时必须是 no-op；③ 不漏对象 —— 借出的对象必须在到期 / 被 Stop / 被 CleanAll 三条路径上都归还自己的池。
    /// 新增驱动：① 实现本接口；② <c>EffectDriverFactory.Create</c> 加分支；③ <c>EffectCatalog</c> 加一行 —— <c>EffectModule</c> 一行都不用改；只做①不做②③时 <c>Play</c> 会打一条 LogError 并返回 None，这就是「未注册」的可见形态。</remarks>
    public interface IEffectDriver
    {
        bool IsSingleton { get; }

        string AssetKey { get; }

        bool IsAssetReady { get; }

        /// <summary>资源到位回调：资源可能为 null（加载失败），实现类必须处理；此后 <see cref="IsAssetReady"/> 应返回 false 以免反复进入加载路径。</summary>
        void OnAssetLoaded(Object asset);

        EffectHandle Play(EffectId id, in EffectContext ctx);

        /// <summary>更新一次<b>已经在播</b>的实例（位置 / 颜色 / 半径）；持续型特效（瞄准高亮等）走这里 —— 每帧 <c>Play</c> 会每帧新建实例。</summary>
        void UpdateInstance(EffectHandle handle, in EffectContext ctx)
        {
        }

        /// <summary>提前停掉一次播放；句柄过期 / 不属于本驱动时必须是 no-op（<c>UpdateInstance</c> 同此纪律）。</summary>
        void Stop(EffectHandle handle);

        void CleanAll();

        void Tick(float dt);

        void Dispose();

        int ActiveInstanceCount { get; }

        int PooledObjectCount { get; }
    }
}
