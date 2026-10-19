using cfg.dso;

namespace DeepseaOil.Presentation
{
    /// <summary>一次性音效的占位时长：超上限就截断，别让静音尾巴占满 voice</summary>
    /// <remarks>出生音效 6.5s 里只有 4.5s 有声、受击 2.54s 里只有 0.4s 有声；素材裁切到位后可调大上限。</remarks>
    public static class SfxPlayback
    {
        public const float DefaultMaxSeconds = 1.5f;

        public const float FadeTailSeconds = 0.12f;

        public static float SecondsOf(AudioId id, float clipLength)
        {
            float length = float.IsNaN(clipLength) || clipLength < 0f ? 0f : clipLength;

            return IsCapped(id, length) ? DefaultMaxSeconds : length;
        }

        public static bool IsCapped(AudioId id, float clipLength)
        {
            if (id == AudioId.PlayerDie) return false;

            return clipLength > DefaultMaxSeconds;
        }
    }
}
