using DeepseaOil.Logic.Events;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_2023_1_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace GameUI
{
    /// <summary>按最大生命分档的全屏暗角/裂纹表现层，含 Cinemachine 心跳联动</summary>
    [DisallowMultipleComponent]
    public sealed class LowHealthScreenEffect : MonoBehaviour
    {
        [Header("UI 组件与节点绑定")]
        [Tooltip("暗角与血雾覆盖层的 CanvasGroup（控制边缘压暗深度，单图模式可留空）")]
        [SerializeField] private CanvasGroup vignetteCanvasGroup;
        [Tooltip("裂纹覆盖层的 CanvasGroup（控制裂纹深浅，必填）")]
        [SerializeField] private CanvasGroup cracksCanvasGroup;
        [Tooltip("裂纹覆盖层的 RectTransform（用于驱动贴图心跳缩放，必填）")]
        [SerializeField] private RectTransform cracksRectTransform;

        [Header("血量分档")]
        [Tooltip("残血特效起始血量；当前 5 血上限下为 2")]
        [SerializeField, Min(1)] private int lowHealthThreshold = 2;

        [Header("状态透明度配置 (Alpha Thresholds)")]
        [Tooltip("未进入残血 (> 残血阈值) 时的暗角透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float healthyVignetteAlpha = 0f;
        [Tooltip("未进入残血 (> 残血阈值) 时的裂纹透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float healthyCracksAlpha = 0f;

        [Tooltip("残血档 (<= 残血阈值) 时的暗角透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float injuredVignetteAlpha = 0.08f;
        [Tooltip("残血档 (<= 残血阈值) 时的裂纹透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float injuredCracksAlpha = 0.32f;

        [Tooltip("濒死 (1HP) 时的暗角透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalVignetteAlpha = 0.78f;
        [Tooltip("濒死 (1HP) 时的裂纹透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalCracksAlpha = 1.0f;

        [Header("平滑过渡 (C0 连续)")]
        [Tooltip("血量状态切换时的透明度淡入淡出耗时（秒）")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float transitionDuration = 0.25f;

        [Header("心跳脉动算法 (ECG 双峰律动)")]
        [Tooltip("濒死状态下的心跳频率（次/分钟，BPM）")]
        [Range(40f, 180f)]
        [SerializeField] private float heartbeatBpm = 48f;
        [Tooltip("贴图心房收缩时的最大缩放倍率（推荐 1.025~1.045）")]
        [Range(1.0f, 1.15f)]
        [SerializeField] private float heartbeatMaxScale = 1.015f;
        [Tooltip("真实心跳归一化波形曲线（0~1周期，横轴为时间比例，纵轴为收缩强度）")]
        [SerializeField]
        private AnimationCurve heartbeatCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.12f, 1.00f),
            new Keyframe(0.20f, 0.25f),
            new Keyframe(0.32f, 0.55f),
            new Keyframe(0.45f, 0.00f),
            new Keyframe(1.00f, 0.00f)
        );

        [Header("全景画面心跳联动 (Cinemachine Camera Pulse)")]
        [Tooltip("是否开启游戏画面同步收缩与微偏移")]
        [SerializeField] private bool enableCameraPulse = true;
        [Tooltip("全屏画面向内收缩最大比例（推荐 0.015 ~ 0.025，即 1.5%~2.5%，严禁过大防眩晕）")]
        [Range(0.005f, 0.025f)]
        [SerializeField] private float maxCameraZoomRatio = 0.01f;
        [Tooltip("心跳收缩时集中点的随机微偏移最大距离（世界单位，推荐 0.04 ~ 0.08）")]
        [Range(0.001f, 0.15f)]
        [SerializeField] private float maxFocalJitterDistance = 0.01f;
        [Tooltip("偏移向中心归位的阻尼时间（秒）")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float jitterDampingTime = 0.22f;

        private float _targetVignetteAlpha;
        private float _targetCracksAlpha;
        private float _startVignetteAlpha;
        private float _startCracksAlpha;
        private float _transitionProgress = 1f;
        private bool _isTransitioning;

        private float _heartbeatTimer;
        private bool _isCritical;
        private float _currentPulseIntensity;

        private float _currentZoomPercent;
        private Vector2 _targetJitterOffset;
        private Vector2 _currentJitterOffset;
        private Vector2 _jitterVelocity;

        private void Awake()
        {
            // 全屏覆盖层必须不参与射线，否则会吃掉场景点击
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }

            if (vignetteCanvasGroup != null)
            {
                vignetteCanvasGroup.blocksRaycasts = false;
                vignetteCanvasGroup.interactable = false;
            }

            if (cracksCanvasGroup != null)
            {
                cracksCanvasGroup.blocksRaycasts = false;
                cracksCanvasGroup.interactable = false;
            }

            EventBus<PlayerHealthChanged>.Subscribe(OnPlayerHealthChanged);

            ApplyImmediateAlpha(healthyVignetteAlpha, healthyCracksAlpha);

            if (cracksRectTransform != null)
            {
                cracksRectTransform.localScale = Vector3.one;
            }

            enabled = false;
        }

        private void OnEnable()
        {
            // 官方相机更新后回调：只在启用期挂接，满血休眠由 OnDisable 摘除
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineCameraUpdated);
        }

        private void OnDisable()
        {
            // 摘除监听并清掉相机运行时偏移，下一帧 Cinemachine 会解算回基准
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);
            _currentZoomPercent = 0f;
            _currentJitterOffset = Vector2.zero;
            _jitterVelocity = Vector2.zero;
            _currentPulseIntensity = 0f;
        }

        private void OnDestroy()
        {
            // 退订只能在 OnDestroy：满血休眠会走 OnDisable
            EventBus<PlayerHealthChanged>.Unsubscribe(OnPlayerHealthChanged);
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);
        }

        private void OnPlayerHealthChanged(PlayerHealthChanged evt)
        {
            SetHealth(Mathf.CeilToInt(evt.Current), Mathf.CeilToInt(evt.Max));
        }

        private void Update()
        {
            UpdateAlphaTransition();
            UpdateHeartbeatDynamics();
            CheckSleepCondition();
        }

        /// <summary>外部血量通知：按残血阈值与最大生命派生满血/残血/濒死/阵亡四档</summary>
        public void SetHealth(int currentHealth, int maxHealth = 3)
        {
            enabled = true;

            // 以当帧实时 Alpha 为起点，血量打断时不硬切
            _startVignetteAlpha = vignetteCanvasGroup != null ? vignetteCanvasGroup.alpha : 0f;
            _startCracksAlpha = cracksCanvasGroup != null ? cracksCanvasGroup.alpha : 0f;
            _transitionProgress = 0f;
            _isTransitioning = true;

            bool wasCritical = _isCritical;
            int full = Mathf.Max(1, maxHealth);
            int low = Mathf.Clamp(lowHealthThreshold, 1, full);
            int critical = Mathf.Max(0, low - 1);

            if (currentHealth <= 0)
            {
                // 0 血只定格深裂纹，停搏动；黑屏由外部死亡逻辑接管
                _targetVignetteAlpha = criticalVignetteAlpha;
                _targetCracksAlpha = criticalCracksAlpha;
                _isCritical = false;
            }
            else if (currentHealth >= full || currentHealth > low)
            {
                _targetVignetteAlpha = healthyVignetteAlpha;
                _targetCracksAlpha = healthyCracksAlpha;
                _isCritical = false;
            }
            else if (currentHealth > critical)
            {
                _targetVignetteAlpha = injuredVignetteAlpha;
                _targetCracksAlpha = injuredCracksAlpha;
                _isCritical = false;
            }
            else
            {
                _targetVignetteAlpha = criticalVignetteAlpha;
                _targetCracksAlpha = criticalCracksAlpha;
                _isCritical = true;

                // 进入濒死立刻给首拍一个随机冲量，之后每个周期起点再刷新
                if (!wasCritical && enableCameraPulse)
                {
                    Vector2 randomDirection = Random.insideUnitCircle.normalized;
                    _targetJitterOffset = randomDirection * maxFocalJitterDistance;
                }
            }
        }

        private void UpdateAlphaTransition()
        {
            if (!_isTransitioning) return;

            float duration = Mathf.Max(transitionDuration, 0.001f);
            _transitionProgress += Time.unscaledDeltaTime / duration;
            float t = Mathf.Clamp01(_transitionProgress);

            if (vignetteCanvasGroup != null)
            {
                vignetteCanvasGroup.alpha = Mathf.Lerp(_startVignetteAlpha, _targetVignetteAlpha, t);
            }

            if (cracksCanvasGroup != null)
            {
                cracksCanvasGroup.alpha = Mathf.Lerp(_startCracksAlpha, _targetCracksAlpha, t);
            }

            if (t >= 1f)
            {
                _isTransitioning = false;
            }
        }

        private void UpdateHeartbeatDynamics()
        {
            if (_isCritical)
            {
                if (heartbeatCurve == null || heartbeatCurve.length == 0)
                {
                    _currentPulseIntensity = 0f;
                    _currentZoomPercent = 0f;
                    _currentJitterOffset = Vector2.zero;
                    _jitterVelocity = Vector2.zero;
                    if (cracksRectTransform != null) cracksRectTransform.localScale = Vector3.one;
                    return;
                }

                float cycleDuration = 60f / Mathf.Max(heartbeatBpm, 1f);
                _heartbeatTimer += Time.deltaTime;

                // 主峰冲量只在周期起点生成，舒张期由 SmoothDamp 归中
                if (_heartbeatTimer >= cycleDuration)
                {
                    _heartbeatTimer %= cycleDuration;
                    if (enableCameraPulse)
                    {
                        Vector2 randomDirection = Random.insideUnitCircle.normalized;
                        _targetJitterOffset = randomDirection * maxFocalJitterDistance;
                    }
                }

                float normalizedTime = _heartbeatTimer / cycleDuration;
                // 曲线可被编出 0~1 之外，夹住防负缩放与超安全线
                _currentPulseIntensity = Mathf.Clamp01(heartbeatCurve.Evaluate(normalizedTime));

                if (cracksRectTransform != null)
                {
                    float targetScale = 1.0f + _currentPulseIntensity * (heartbeatMaxScale - 1.0f);
                    cracksRectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
                }

                if (enableCameraPulse)
                {
                    _currentZoomPercent = _currentPulseIntensity * maxCameraZoomRatio;
                    Vector2 activeTarget = _targetJitterOffset * _currentPulseIntensity;
                    _currentJitterOffset = Vector2.SmoothDamp(
                        _currentJitterOffset,
                        activeTarget,
                        ref _jitterVelocity,
                        jitterDampingTime
                    );
                }
            }
            else
            {
                _currentPulseIntensity = 0f;
                _currentZoomPercent = Mathf.MoveTowards(_currentZoomPercent, 0f, Time.unscaledDeltaTime * 0.1f);
                _currentJitterOffset = Vector2.MoveTowards(_currentJitterOffset, Vector2.zero, Time.unscaledDeltaTime * 0.5f);
                _jitterVelocity = Vector2.zero;

                if (cracksRectTransform != null && cracksRectTransform.localScale != Vector3.one)
                {
                    cracksRectTransform.localScale = Vector3.MoveTowards(
                        cracksRectTransform.localScale,
                        Vector3.one,
                        Time.unscaledDeltaTime * 3f
                    );
                }
            }
        }

        /// <summary>相机解算完成后注入缩放/偏移，避免被 Cinemachine 覆盖</summary>
        private void OnCinemachineCameraUpdated(CinemachineBrain brain)
        {
            if (!enableCameraPulse || brain == null) return;

            Camera cam = brain.OutputCamera;
            if (cam == null || !cam.orthographic) return;

            if (_currentZoomPercent <= 0.0001f && _currentJitterOffset.sqrMagnitude <= 0.00001f) return;

            // 0.025 是 2.5% 生理安全硬上限，防曲线或 Inspector 越界
            float zoom = Mathf.Clamp(_currentZoomPercent, 0f, Mathf.Min(maxCameraZoomRatio, 0.025f));
            cam.orthographicSize *= (1f - zoom);

            Vector2 jitter = Vector2.ClampMagnitude(_currentJitterOffset, maxFocalJitterDistance);
            cam.transform.position += new Vector3(jitter.x, jitter.y, 0f);
        }

        private void CheckSleepCondition()
        {
            if (_isCritical || _isTransitioning) return;
            if (!IsTargetClear()) return;

            if (cracksRectTransform != null && cracksRectTransform.localScale != Vector3.one) return;

            if (_currentZoomPercent > 0.0001f || _currentJitterOffset.sqrMagnitude > 0.00001f) return;

            enabled = false;
        }

        // 单图模式忽略未挂的层，否则满血可能永不休眠
        private bool IsTargetClear()
        {
            bool vignetteClear = vignetteCanvasGroup == null || Mathf.Approximately(_targetVignetteAlpha, 0f);
            bool cracksClear = cracksCanvasGroup == null || Mathf.Approximately(_targetCracksAlpha, 0f);
            return vignetteClear && cracksClear;
        }

        private void ApplyImmediateAlpha(float vignetteAlpha, float cracksAlpha)
        {
            if (vignetteCanvasGroup != null) vignetteCanvasGroup.alpha = vignetteAlpha;
            if (cracksCanvasGroup != null) cracksCanvasGroup.alpha = cracksAlpha;

            _targetVignetteAlpha = vignetteAlpha;
            _targetCracksAlpha = cracksAlpha;
            _transitionProgress = 1f;
            _isTransitioning = false;
        }

#if UNITY_EDITOR
        [ContextMenu("模拟：3血（满血全清休眠）")]
        public void DebugSetHealth3() => DebugSetHealth(3);

        [ContextMenu("模拟：2血（轻度裂纹静止）")]
        public void DebugSetHealth2() => DebugSetHealth(2);

        [ContextMenu("模拟：1血（深度裂纹+画面双峰收缩+随机顿挫）")]
        public void DebugSetHealth1() => DebugSetHealth(1);

        [ContextMenu("模拟：0血（阵亡定格）")]
        public void DebugSetHealth0() => DebugSetHealth(0);

        private void DebugSetHealth(int hp)
        {
            SetHealth(hp);

            if (Application.isPlaying) return;

            ApplyImmediateAlpha(_targetVignetteAlpha, _targetCracksAlpha);

            if (cracksRectTransform != null)
            {
                cracksRectTransform.localScale = Vector3.one;
            }

            UnityEditor.EditorUtility.SetDirty(this);

            if (hp >= 3 && IsTargetClear())
            {
                enabled = false;
            }
        }
#endif
    }
}
