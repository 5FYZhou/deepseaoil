using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <remarks>纪律：不抛异常（失败只 LogError/LogWarning 并返回 EffectHandle.None）；无实例时 Tick/CleanAll/Dispose 是 no-op；借出的对象在到期/被 Stop/被 CleanAll 三条路径上都归还池；新增驱动需改 EffectDriverFactory.Create 与 EffectCatalog，否则 Play 只打 LogError 返回 None。</remarks>
    public interface IEffectDriver
    {
        bool IsSingleton { get; }

        string AssetKey { get; }

        bool IsAssetReady { get; }

        /// <summary>资源到位回调；资源可能为 null（加载失败）必须处理</summary>
        void OnAssetLoaded(Object asset);

        EffectHandle Play(EffectId id, in EffectContext ctx);

        void UpdateInstance(EffectHandle handle, in EffectContext ctx)
        {
        }

        void Stop(EffectHandle handle);

        void CleanAll();

        void Tick(float dt);

        void Dispose();

        int ActiveInstanceCount { get; }

        int PooledObjectCount { get; }
    }
}
