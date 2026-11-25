using UnityEngine;

namespace DeepseaOil.Data.Settings
{
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "DeepseaOil/Settings/AudioConfig")]
    public class AudioConfig : BaseConfig
    {
        [Header("初始音乐音量大小(0-1)")]
        public float bgmVolume;
        [Header("初始音效音量大小(0-1)")]
        public float sfxVolume;
        [Header("音频文件路径")]
        public string path;
        [Header("切换bgm时淡入淡出的时间")]
        public float bgmFadeDuration;

        [Header("UI 打字机短音资源 Key")]
        public string typewriterBlipKey;

        [Header("UI 弹窗滑入短音资源 Key")]
        public string popupSlideInKey;

        [Header("UI 弹窗滑出短音资源 Key")]
        public string popupSlideOutKey;

        [Header("打字机短音最小间隔（秒）")]
        public float typewriterBlipCooldown = 0.055f;

        [Header("UI 短音相对音效音量")]
        [Range(0f, 1f)]
        public float uiSfxVolumeScale = 0.95f;
    }
}
