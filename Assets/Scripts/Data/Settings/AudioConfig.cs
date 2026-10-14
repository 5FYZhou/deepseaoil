using UnityEngine;

namespace DeepseaOil.Data
{
    public enum AudioId
    {
        None = 0,
        Bgm,
        Walk,
        Jump,
        PlayerHurt,
        PlayerDie
    };

    [CreateAssetMenu(fileName = "AudioConfig", menuName = "DeepseaOil/Settings/AudioConfig")]
    public class AudioConfig : BaseConfig
    {
        [Header("初始音乐音量大小(0-1)")]
        public float bgmVolume;
        [Header("初始音效音量大小(0-1)")]
        public float sfxVolume;
        [Header("音频文件路径")]
        public string path;
    }
}
