using DeepseaOil.Logic.Events;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>3 阶血量的全屏暗角/裂纹表现层</summary>
    [DisallowMultipleComponent]
    public sealed class LowHealthScreenEffect : MonoBehaviour
    {
        [Header("组件与节点绑定")]
        [Tooltip("暗角与血雾覆盖层的 CanvasGroup（控制边缘压暗深度，单图模式可留空）")]
        [SerializeField] private CanvasGroup vignetteCanvasGroup;
        [Tooltip("裂纹覆盖层的 CanvasGroup（控制裂纹深浅，必填）")]
        [SerializeField] private CanvasGroup cracksCanvasGroup;
        [Tooltip("裂纹覆盖层的 RectTransform（用于驱动心跳缩放，必填）")]
        [SerializeField] private RectTransform cracksRectTransform;

        [Header("状态透明度配置 (Alpha Thresholds)")]
        [Tooltip("满血 (>= 3HP) 时的暗角透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float healthyVignetteAlpha = 0f;
        [Tooltip("满血 (>= 3HP) 时的裂纹透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float healthyCracksAlpha = 0f;

        [Tooltip("受创 (2HP) 时的暗角透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float injuredVignetteAlpha = 0.08f;
        [Tooltip("受创 (2HP) 时的裂纹透明度")]
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
        [SerializeField] private float heartbeatBpm = 76f;
        [Tooltip("心房收缩时的最大缩放倍率（推荐 1.025~1.045，过大会引发眩晕）")]
        [Range(1.0f, 1.15f)]
        [SerializeField] private float heartbeatMaxScale = 1.035f;
        [Tooltip("真实心跳归一化波形曲线（0~1周期，横轴为时间比例，纵轴为收缩强度）")]
        [SerializeField] private AnimationCurve heartbeatCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.12f, 1.00f),
            new Keyframe(0.20f, 0.25f),
            new Keyframe(0.32f, 0.55f),
            new Keyframe(0.45f, 0.00f),
            new Keyframe(1.00f, 0.00f)
        );

        private float _targetVignetteAlpha;
        private float _targetCracksAlpha;
        private float _startVignetteAlpha;
        private float _startCracksAlpha;
        private float _transitionProgress = 1f;
        private bool _isTransitioning;
        private float _heartbeatTimer;
        private bool _isCritical;

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

        private void OnDestroy()
        {
            // 退订只能在 OnDestroy：满血休眠会走 OnDisable
            EventBus<PlayerHealthChanged>.Unsubscribe(OnPlayerHealthChanged);
        }

        private void OnPlayerHealthChanged(PlayerHealthChanged evt)
        {
            SetHealth(Mathf.CeilToInt(evt.Current), Mathf.CeilToInt(evt.Max));
        }

        private void Update()
        {
            UpdateAlphaTransition();
            UpdateHeartbeatScale();
            CheckSleepCondition();
        }

        /// <summary>外部血量通知：按 maxHealth 派生满血/受创/濒死/阵亡四档</summary>
        public void SetHealth(int currentHealth, int maxHealth = 3)
        {
            enabled = true;

            // 以当帧实时 Alpha 为起点，血量打断时不硬切
            _startVignetteAlpha = vignetteCanvasGroup != null ? vignetteCanvasGroup.alpha : 0f;
            _startCracksAlpha = cracksCanvasGroup != null ? cracksCanvasGroup.alpha : 0f;
            _transitionProgress = 0f;
            _isTransitioning = true;

            int full = Mathf.Max(1, maxHealth);

            if (currentHealth <= 0)
            {
                // 0 血只定格深裂纹，停搏动；黑屏由外部死亡逻辑接管
                _targetVignetteAlpha = criticalVignetteAlpha;
                _targetCracksAlpha = criticalCracksAlpha;
                _isCritical = false;
            }
            else if (currentHealth >= full)
            {
                _targetVignetteAlpha = healthyVignetteAlpha;
                _targetCracksAlpha = healthyCracksAlpha;
                _isCritical = false;
            }
            else if (currentHealth >= full - 1)
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
            }
        }

        private void UpdateAlphaTransition()
        {
            if (!_isTransitioning) return;

            float duration = Mathf.Max(transitionDuration, 0.001f);
            _transitionProgress += Time.deltaTime / duration;
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

        private void UpdateHeartbeatScale()
        {
            if (cracksRectTransform == null) return;

            if (_isCritical)
            {
                if (heartbeatCurve == null || heartbeatCurve.length == 0) return;

                float cycleDuration = 60f / Mathf.Max(heartbeatBpm, 1f);
                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= cycleDuration)
                {
                    _heartbeatTimer %= cycleDuration;
                }

                float normalizedTime = _heartbeatTimer / cycleDuration;
                // 曲线可被编出 0~1 之外，夹住防负缩放
                float intensity = Mathf.Clamp01(heartbeatCurve.Evaluate(normalizedTime));
                float targetScale = 1.0f + intensity * (heartbeatMaxScale - 1.0f);

                cracksRectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
            }
            else
            {
                if (cracksRectTransform.localScale != Vector3.one)
                {
                    cracksRectTransform.localScale = Vector3.MoveTowards(
                        cracksRectTransform.localScale,
                        Vector3.one,
                        Time.deltaTime * 3f
                    );
                }
            }
        }

        private void CheckSleepCondition()
        {
            if (_isCritical || _isTransitioning) return;
            if (!IsTargetClear()) return;

            if (cracksRectTransform != null && cracksRectTransform.localScale != Vector3.one) return;

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

        [ContextMenu("模拟：1血（深度裂纹+心跳脉动+暗角）")]
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
