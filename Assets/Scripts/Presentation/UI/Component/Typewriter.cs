using System.Collections;
using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>逐字显示：只推进 maxVisibleCharacters，不改字符串、不逐帧重建文本</summary>
    public sealed class Typewriter
    {
        private const int AllCharacters = 99999;

        private readonly MonoBehaviour _host;

        private TMP_Text _text;
        private int _total;
        private Coroutine _running;

        /// <summary>暂停时冻结逐字；本协程吃 unscaled，缩放时间区分不出暂停</summary>
        public bool Paused { get; set; }

        public Typewriter(MonoBehaviour host)
        {
            _host = host;
        }

        /// <summary>charsPerSecond 小于等于 0 表示不逐字，直接显示全部</summary>
        public void Play(TMP_Text text, float charsPerSecond)
        {
            Stop();

            _text = text;
            if (text == null) return;

            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
            _total = text.textInfo.characterCount;

            if (_total == 0) return;

            if (charsPerSecond <= 0f)
            {
                text.maxVisibleCharacters = AllCharacters;
                return;
            }

            _running = _host.StartCoroutine(CoReveal(charsPerSecond));
        }

        /// <summary>返回 true：补全了一段正在进行的逐字，调用方据此吞掉这次点击</summary>
        public bool Complete()
        {
            if (_running == null) return false;

            Stop();

            if (_text != null)
                _text.maxVisibleCharacters = AllCharacters;

            return true;
        }

        public void Stop()
        {
            if (_running == null) return;

            _host.StopCoroutine(_running);
            _running = null;
        }

        private IEnumerator CoReveal(float charsPerSecond)
        {
            int total = _total;
            int shown = 0;
            float elapsed = 0f;

            // 对话期间 timeScale=0，缩放 deltaTime 恒为 0，逐字必须吃 unscaled
            while (shown < total)
            {
                if (!Paused) elapsed += Time.unscaledDeltaTime;
                shown = Mathf.Min(total, Mathf.FloorToInt(elapsed * charsPerSecond) + 1);
                _text.maxVisibleCharacters = shown;
                yield return null;
            }

            // 收尾交回 TMP 自身上限：characterCount 与可见字符的计数口径未必一致
            _text.maxVisibleCharacters = AllCharacters;
            _running = null;
        }
    }
}
