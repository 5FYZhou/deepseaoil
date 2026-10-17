using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.Presentation.Lighting
{
    /// <summary>全局环境光照时段控制器：驱动场景唯一 Global Light 2D</summary>
    /// <remarks>强度下限由 TimeOfDaySetting 保证（>=0.35）；未绑定光源时静默降级不刷日志。过渡插值无堆分配。</remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentDirector : MonoBehaviour
    {
        [Header("光照引用")]
        [Tooltip("场景主全局光照；留空则从子节点抓取")]
        [SerializeField] private Light2D globalLight = default;

        [Header("初始配置")]
        [Tooltip("开局时段；选 Night 时全局光被压到 0.20，只有火光与随身光照得亮场景")]
        [SerializeField] private TimeOfDayKind initialTimeOfDay = TimeOfDayKind.Day;

        [Tooltip("时段过渡插值时长（秒）；0 会让昼夜切换瞬间跳变")]
        [Range(0.1f, 10f)]
        [SerializeField] private float transitionDuration = 1.5f;

        [Header("时段覆盖")]
        [Tooltip("按 Kind 覆盖内置时段参数；数组里没写的时段继续用 TimeOfDaySetting.GetDefault 的内置值")]
        [SerializeField] private TimeOfDaySetting[] customTimeOfDayOverrides = default;

        private TimeOfDaySetting _currentSetting;
        private TimeOfDaySetting _targetSetting;

        private float _transitionTime;
        private bool _isTransitioning;

        /// <summary>当前生效或正在过渡到的时段</summary>
        public TimeOfDayKind CurrentTimeOfDay => _targetSetting.Kind;

        private void Awake()
        {
            if (globalLight == null)
            {
                globalLight = GetComponentInChildren<Light2D>();
            }

            _currentSetting = ResolveSetting(initialTimeOfDay);
            _targetSetting = _currentSetting;

            ApplySettingDirectly(in _currentSetting);
        }

        private void Update()
        {
            if (!_isTransitioning) return;

            _transitionTime += Time.deltaTime;

            float t = Mathf.Clamp01(_transitionTime / Mathf.Max(transitionDuration, 0.01f));
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (globalLight != null)
            {
                globalLight.color = Color.Lerp(_currentSetting.LightColor, _targetSetting.LightColor, smoothT);
                globalLight.intensity = Mathf.Lerp(_currentSetting.Intensity, _targetSetting.Intensity, smoothT);
            }

            if (t < 1f) return;

            _isTransitioning = false;
            _currentSetting = _targetSetting;

            ApplySettingDirectly(in _currentSetting);
        }

        /// <summary>平滑切换到目标时段</summary>
        public void TransitionTo(TimeOfDayKind kind)
        {
            if (_targetSetting.Kind == kind && !_isTransitioning) return;

            _targetSetting = ResolveSetting(kind);
            _transitionTime = 0f;
            _isTransitioning = true;
        }

        /// <summary>瞬时设置时段，无过渡</summary>
        public void SetTimeOfDayImmediate(TimeOfDayKind kind)
        {
            _isTransitioning = false;
            _currentSetting = ResolveSetting(kind);
            _targetSetting = _currentSetting;

            ApplySettingDirectly(in _currentSetting);
        }

        /// <summary>取该时段的生效参数：Inspector 覆盖优先，缺项回落内置表</summary>
        private TimeOfDaySetting ResolveSetting(TimeOfDayKind kind)
        {
            if (customTimeOfDayOverrides != null)
            {
                for (int i = 0; i < customTimeOfDayOverrides.Length; i++)
                {
                    if (customTimeOfDayOverrides[i].Kind == kind) return customTimeOfDayOverrides[i];
                }
            }

            return TimeOfDaySetting.GetDefault(kind);
        }

        private void ApplySettingDirectly(in TimeOfDaySetting setting)
        {
            if (globalLight == null) return;

            globalLight.color = setting.LightColor;
            globalLight.intensity = setting.Intensity;
        }

        private void OnValidate()
        {
            if (customTimeOfDayOverrides == null) return;

            for (int i = 0; i < customTimeOfDayOverrides.Length; i++)
            {
                // 新加元素的默认色是 (0,0,0,0)：alpha 为 0 只可能是"加了没填值"，照写会让该时段全局光纯黑
                if (customTimeOfDayOverrides[i].LightColor.a <= 0f)
                {
                    Debug.LogWarning($"[Light] 时段覆盖第 {i} 条的 Light Color 未填写（alpha=0）：该时段会把全局光写成纯黑。", this);
                }
            }
        }
    }
}
