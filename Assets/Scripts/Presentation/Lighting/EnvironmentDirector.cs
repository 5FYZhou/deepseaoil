using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.Presentation.Lighting
{
    /// <summary>全局环境光照时段控制器：驱动场景唯一 Global Light 2D</summary>
    /// <remarks>三段颜色与强度就是场景里的字段，改完即时生效；未绑定光源时静默降级不刷日志，过渡插值无堆分配。</remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentDirector : MonoBehaviour
    {
        [Header("光照引用")]
        [Tooltip("场景主全局光照；留空则从子节点抓取")]
        [SerializeField] private Light2D globalLight = default;

        [Header("初始配置")]
        [Tooltip("开局时段；切到 Night 后只有火光、泉眼与随身光照得亮场景")]
        [SerializeField] private TimeOfDayKind initialTimeOfDay = TimeOfDayKind.Day;

        [Tooltip("时段过渡插值时长（秒）；0 会让昼夜切换瞬间跳变")]
        [Range(0.1f, 10f)]
        [SerializeField] private float transitionDuration = 1.5f;

        [Header("白昼外观")]
        [SerializeField] private TimeOfDaySetting daySetting = TimeOfDaySetting.GetDefault(TimeOfDayKind.Day);

        [Header("黄昏外观")]
        [SerializeField] private TimeOfDaySetting duskSetting = TimeOfDaySetting.GetDefault(TimeOfDayKind.Dusk);

        [Header("夜晚外观")]
        [SerializeField] private TimeOfDaySetting nightSetting = TimeOfDaySetting.GetDefault(TimeOfDayKind.Night);

        private TimeOfDayKind _currentKind;
        private TimeOfDayKind _targetKind;

        private float _transitionTime;
        private bool _isTransitioning;

        /// <summary>当前生效或正在过渡到的时段</summary>
        public TimeOfDayKind CurrentTimeOfDay => _targetKind;

        private void Awake()
        {
            if (globalLight == null)
            {
                globalLight = GetComponentInChildren<Light2D>();
            }

            _currentKind = initialTimeOfDay;
            _targetKind = initialTimeOfDay;

            TimeOfDaySetting current = ResolveSetting(_currentKind);

            ApplySettingDirectly(in current);
        }

        private void Update()
        {
            if (!_isTransitioning) return;

            _transitionTime += Time.deltaTime;

            float t = Mathf.Clamp01(_transitionTime / Mathf.Max(transitionDuration, 0.01f));
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (globalLight != null)
            {
                TimeOfDaySetting from = ResolveSetting(_currentKind);
                TimeOfDaySetting to = ResolveSetting(_targetKind);

                globalLight.color = Color.Lerp(from.LightColor, to.LightColor, smoothT);
                globalLight.intensity = Mathf.Lerp(from.Intensity, to.Intensity, smoothT);
            }

            if (t < 1f) return;

            _isTransitioning = false;
            _currentKind = _targetKind;

            TimeOfDaySetting current = ResolveSetting(_currentKind);

            ApplySettingDirectly(in current);
        }

        /// <summary>平滑切换到目标时段</summary>
        public void TransitionTo(TimeOfDayKind kind)
        {
            if (_targetKind == kind && !_isTransitioning) return;

            _targetKind = kind;
            _transitionTime = 0f;
            _isTransitioning = true;
        }

        /// <summary>瞬时设置时段，无过渡</summary>
        public void SetTimeOfDayImmediate(TimeOfDayKind kind)
        {
            _isTransitioning = false;
            _currentKind = kind;
            _targetKind = kind;

            TimeOfDaySetting current = ResolveSetting(_currentKind);

            ApplySettingDirectly(in current);
        }

        /// <summary>取该时段的生效外观：三段各占一个字段，没有数组匹配与缺项回落</summary>
        private TimeOfDaySetting ResolveSetting(TimeOfDayKind kind)
        {
            switch (kind)
            {
                case TimeOfDayKind.Dusk:
                    return duskSetting;

                case TimeOfDayKind.Night:
                    return nightSetting;

                default:
                    return daySetting;
            }
        }

        private void ApplySettingDirectly(in TimeOfDaySetting setting)
        {
            if (globalLight == null) return;

            globalLight.color = setting.LightColor;
            globalLight.intensity = setting.Intensity;
        }

#if UNITY_EDITOR
        // 播放中改颜色要立刻反映到主光，否则每试一个色都得退出重进；过渡中不抢写，免得和插值打架
        private void OnValidate()
        {
            if (!Application.isPlaying || _isTransitioning) return;

            TimeOfDaySetting current = ResolveSetting(_currentKind);

            ApplySettingDirectly(in current);
        }
#endif
    }
}
