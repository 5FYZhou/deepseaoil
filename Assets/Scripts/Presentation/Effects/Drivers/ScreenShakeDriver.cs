using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>震屏驱动：把 ctx.Value 折算成相机偏移幅度，实际施加由 CameraShaker 在 LateUpdate 完成</summary>
    public sealed class ScreenShakeDriver : IEffectDriver
    {
        /// <summary>满强度的偏移幅度（世界单位），改它即改全局震感</summary>
        private const float AmplitudePerIntensity = 0.15f;

        private const float FallbackAmplitude = 0.1f;

        private const float DefaultDuration = 0.15f;

        private int _activeId;
        private int _epoch = 1;

        public bool IsSingleton => true;

        public string AssetKey => string.Empty;

        public bool IsAssetReady => true;

        /// <summary>本驱动不持有实例：播放次数归 CameraShaker 自己的衰减账本</summary>
        public int ActiveInstanceCount => 0;

        public int PooledObjectCount => 0;

        public void OnAssetLoaded(Object asset) { }

        public EffectHandle Play(EffectId id, in EffectContext ctx)
        {
            EnsureShaker();

            CameraShaker shaker = CameraShaker.Instance;

            if (shaker == null) return EffectHandle.None;

            float intensity = Mathf.Clamp01(ctx.Value);
            float amplitude = intensity > 0f ? intensity * AmplitudePerIntensity : FallbackAmplitude;
            float duration = ctx.Duration > 0f ? ctx.Duration : DefaultDuration;

            shaker.Shake(amplitude, duration);

            _activeId++;

            return new EffectHandle(_activeId, _epoch, this);
        }

        /// <summary>相机随场景重建，挂件要跟着补</summary>
        private void EnsureShaker()
        {
            if (CameraShaker.Instance != null) return;

            Camera camera = Camera.main;

            if (camera != null && camera.GetComponent<CameraShaker>() == null)
            {
                camera.gameObject.AddComponent<CameraShaker>();
            }
        }

        public void Stop(EffectHandle handle) { }

        public void CleanAll() => _epoch++;

        public void Dispose() => CleanAll();

        public void Tick(float dt) { }
    }
}
