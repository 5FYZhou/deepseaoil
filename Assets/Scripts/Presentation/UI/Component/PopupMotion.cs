using System.Collections;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    public sealed class PopupMotion : MonoBehaviour
    {
        [SerializeField] private float enterSeconds = 0.26f;
        [SerializeField] private float exitSeconds = 0.22f;
        [SerializeField] private float edgeMargin = 48f;
        [SerializeField] private float wobbleAngle = 0.75f;
        [SerializeField] private float wobbleHertz = 1.1f;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private Coroutine _slide;
        private Coroutine _wobble;
        private float _wobbleElapsed;
        private bool _visible;
        private bool _entering;

        public bool Paused { get; set; }
        public bool IsAnimating => _slide != null;

        public void Configure(
            float enter,
            float exit,
            float margin,
            float angle,
            float hertz)
        {
            enterSeconds = enter;
            exitSeconds = exit;
            edgeMargin = margin;
            wobbleAngle = angle;
            wobbleHertz = hertz;
            CaptureBasePose();
        }

        public void PlayEnter()
        {
            if (!isActiveAndEnabled)
                return;

            StopSlide();
            CaptureBasePose();

            _visible = true;
            _entering = true;
            _wobbleElapsed = 0f;

            Vector2 from = OffsetFromEdge();

            _rect.anchoredPosition = from;
            _rect.localRotation = _baseRotation;
            _slide = StartCoroutine(CoSlide(from, _basePosition, enterSeconds, false));
            EnsureWobble();
        }

        public void PlayExit()
        {
            if (_rect == null)
                return;

            if (!isActiveAndEnabled)
            {
                HideImmediate();
                return;
            }

            StopSlide();
            _visible = false;
            _entering = false;
            _slide = StartCoroutine(
                CoSlide(_rect.anchoredPosition, OffsetFromEdge(), exitSeconds, true));
        }

        public void HideImmediate()
        {
            StopAllMotion();
            ApplyBasePose();
        }

        public static float SlideDistance(RectTransform rect, float margin)
        {
            if (rect == null)
                return 0f;

            return Mathf.Max(0f, rect.rect.width) + Mathf.Max(0f, margin);
        }

        private void Awake()
        {
            _rect = transform as RectTransform;
            CaptureBasePose();
        }

        private void OnEnable()
        {
            CaptureBasePose();
            ApplyBasePose();
            EnsureWobble();
        }

        private void OnDisable()
        {
            StopAllMotion();
            ApplyBasePose();
        }

        private void CaptureBasePose()
        {
            if (_rect == null)
                _rect = transform as RectTransform;

            if (_rect == null)
                return;

            _basePosition = _rect.anchoredPosition;
            _baseRotation = _rect.localRotation;
        }

        private Vector2 OffsetFromEdge()
        {
            float direction = EdgeDirection(_basePosition.x);
            float distance = SlideDistance(_rect, edgeMargin);

            return _basePosition + Vector2.right * direction * distance;
        }

        public static float EdgeDirection(float baseX)
        {
            return baseX <= 0f ? 1f : -1f;
        }

        private IEnumerator CoSlide(
            Vector2 from,
            Vector2 target,
            float duration,
            bool hideAtEnd)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0f, duration);

            while (elapsed < safeDuration)
            {
                if (!Paused)
                    elapsed += Time.unscaledDeltaTime;

                float progress = safeDuration > 0f
                    ? Mathf.Clamp01(elapsed / safeDuration)
                    : 1f;

                float eased = hideAtEnd
                    ? progress * progress * progress
                    : 1f - Mathf.Pow(1f - progress, 3f);

                _rect.anchoredPosition = Vector2.LerpUnclamped(from, target, eased);
                yield return null;
            }

            _rect.anchoredPosition = target;
            _slide = null;
            _entering = false;

            if (hideAtEnd)
                ApplyBasePose();
        }

        private void EnsureWobble()
        {
            if (_wobble == null && isActiveAndEnabled)
                _wobble = StartCoroutine(CoWobble());
        }

        private IEnumerator CoWobble()
        {
            while (true)
            {
                if (!Paused && _visible && !_entering)
                {
                    _wobbleElapsed += Time.unscaledDeltaTime;

                    float angle = Mathf.Sin(
                        _wobbleElapsed * wobbleHertz * Mathf.PI * 2f) * wobbleAngle;

                    _rect.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
                }

                yield return null;
            }
        }

        private void StopSlide()
        {
            if (_slide == null)
                return;

            StopCoroutine(_slide);
            _slide = null;
        }

        private void StopAllMotion()
        {
            StopSlide();

            if (_wobble != null)
            {
                StopCoroutine(_wobble);
                _wobble = null;
            }

            _visible = false;
            _entering = false;
        }

        private void ApplyBasePose()
        {
            if (_rect == null)
                return;

            _rect.anchoredPosition = _basePosition;
            _rect.localRotation = _baseRotation;
        }
    }
}
