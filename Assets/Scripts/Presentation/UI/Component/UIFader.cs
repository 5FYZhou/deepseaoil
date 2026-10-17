using System.Collections;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>CanvasGroup 淡入淡出：重复调用先停掉上一次，从当前 alpha 接着淡，连续切换不闪</summary>
    public sealed class UIFader
    {
        private readonly MonoBehaviour _host;
        private readonly CanvasGroup _group;

        private Coroutine _running;

        private UIFader(MonoBehaviour host, CanvasGroup group)
        {
            _host = host;
            _group = group;
        }

        /// <summary>host 或 root 为空时返回 null，调用方据此跳过</summary>
        public static UIFader Create(MonoBehaviour host, GameObject root)
        {
            if (host == null || root == null) return null;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
                group = root.AddComponent<CanvasGroup>();

            return new UIFader(host, group);
        }

        public void FadeIn(float duration)
        {
            Fade(1f, duration, false);
        }

        public void FadeOut(float duration)
        {
            Fade(0f, duration, true);
        }

        /// <summary>淡出途中为真：调用方据此等走完消失动画再收尾</summary>
        public bool IsVisible => _group.gameObject.activeInHierarchy;

        public void HideImmediate()
        {
            Stop();

            _group.alpha = 0f;
            _group.gameObject.SetActive(false);
        }

        public void ShowImmediate()
        {
            Stop();

            _group.alpha = 1f;
            _group.gameObject.SetActive(true);
        }

        private void Fade(float target, float duration, bool deactivateWhenDone)
        {
            Stop();

            if (target <= 0f && !_group.gameObject.activeSelf) return;

            if (!_group.gameObject.activeSelf)
            {
                _group.alpha = 0f;
                _group.gameObject.SetActive(true);
            }

            _running = _host.StartCoroutine(CoFade(target, duration, deactivateWhenDone));
        }

        private void Stop()
        {
            if (_running == null) return;

            _host.StopCoroutine(_running);
            _running = null;
        }

        private IEnumerator CoFade(float target, float duration, bool deactivateWhenDone)
        {
            float from = _group.alpha;
            float elapsed = 0f;

            // 对话期间 timeScale=0，缩放 deltaTime 恒为 0，淡入淡出必须吃 unscaled
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _group.alpha = target;
            _running = null;

            if (deactivateWhenDone)
                _group.gameObject.SetActive(false);
        }
    }
}
