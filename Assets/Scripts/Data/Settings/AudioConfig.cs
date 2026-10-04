using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    public enum AudioId
    {
        None = 0,
        Bgm,
        Walk,
        Jump
    };

    [Serializable]
    public struct AudioMap
    {
        public AudioId id;
        public string fileName;
    }


    [CreateAssetMenu(fileName = "AudioConfig", menuName = "音频文件名映射")]
    public class AudioConfig : BaseConfig
    {
        [Header("初始音乐音量大小(0-1)")]
        public float bgmVolume;
        [Header("初始音效音量大小(0-1)")]
        public float sfxVolume;
        [Header("音频文件路径")]
        public string path;

        [SerializeField]
        [Tooltip("程序里用id区分各音频，这里确定id对应哪个音频文件，填文件名")]
        private List<AudioMap> audioMaps = new();

        public IReadOnlyList<AudioMap> AudioMaps => audioMaps;
    }
}
