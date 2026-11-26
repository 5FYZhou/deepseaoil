using System.Collections;
using System;
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
        private Action _onCharRevealed;
        private Action _onCompleted;

        /// <summary>暂停时冻结逐字；本协程吃 unscaled，缩放时间区分不出暂停</summary>
        public bool Paused { get; set; }

        public bool IsPlaying => _running != null;

        public Typewriter(MonoBehaviour host)
        {
            _host = host;
        }

        /// <summary>charsPerSecond 小于等于 0 表示不逐字，直接显示全部</summary>
        public void Play(TMP_Text text, float charsPerSecond)
        {
            Play(text, charsPerSecond, null, null);
        }

        public void Play(
            TMP_Text text,
            float charsPerSecond,
            Action onCharRevealed,
            Action onCompleted = null)
        {
            Stop();

            _text = text;
            _onCharRevealed = onCharRevealed;
            _onCompleted = onCompleted;

            if (text == null)
            {
                CompleteCallbacks();
                return;
            }

            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
            _total = text.textInfo.characterCount;

            if (_total == 0)
            {
                CompleteCallbacks();
                return;
            }

            if (charsPerSecond <= 0f)
            {
                text.maxVisibleCharacters = AllCharacters;
                CompleteCallbacks();
                return;
            }

            _running = _host.StartCoroutine(CoReveal(charsPerSecond));
        }

        /// <summary>返回 true：补全了一段正在进行的逐字，调用方据此吞掉这次点击</summary>
        public bool Complete()
        {
            if (_running == null) return false;

            _host.StopCoroutine(_running);
            _running = null;

            if (_text != null)
            {
                _text.maxVisibleCharacters = AllCharacters;
            }

            CompleteCallbacks();

            return true;
        }

        public void Stop()
        {
            if (_running == null) return;

            _host.StopCoroutine(_running);
            _running = null;
            _onCharRevealed = null;
            _onCompleted = null;
        }

        public static int CountVisibleCharacters(TMP_Text text)
        {
            if (text == null)
                return 0;

            text.ForceMeshUpdate();

            return text.textInfo != null ? text.textInfo.characterCount : 0;
        }

        public static int RevealCount(float elapsed, float charsPerSecond, int total)
        {
            if (total <= 0)
                return 0;

            if (charsPerSecond <= 0f)
                return total;

            return Mathf.Min(total, Mathf.FloorToInt(elapsed * charsPerSecond) + 1);
        }

        public static bool IsRevealable(char character)
        {
            return !char.IsWhiteSpace(character);
        }

        private IEnumerator CoReveal(float charsPerSecond)
        {
            int total = _total;
            int shown = 0;
            int previousShown = 0;
            float elapsed = 0f;

            // 对话期间 timeScale=0，缩放 deltaTime 恒为 0，逐字必须吃 unscaled
            while (shown < total)
            {
                if (!Paused) elapsed += Time.unscaledDeltaTime;

                shown = RevealCount(elapsed, charsPerSecond, total);
                _text.maxVisibleCharacters = shown;

                if (shown > previousShown && HasVisibleCharacter(previousShown, shown))
                {
                    _onCharRevealed?.Invoke();
                }

                previousShown = shown;
                yield return null;
            }

            // 收尾交回 TMP 自身上限：characterCount 与可见字符的计数口径未必一致
            _text.maxVisibleCharacters = AllCharacters;
            _running = null;
            CompleteCallbacks();
        }

        private bool HasVisibleCharacter(int start, int end)
        {
            TMP_TextInfo info = _text.textInfo;

            if (info == null)
                return false;

            int limit = Mathf.Min(end, info.characterCount);

            for (int i = Mathf.Max(0, start); i < limit; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];

                if (IsRevealable(character.character))
                {
                    return true;
                }
            }

            return false;
        }

        private void CompleteCallbacks()
        {
            Action completed = _onCompleted;

            _onCompleted = null;
            _onCharRevealed = null;
            completed?.Invoke();
        }
    }
}
