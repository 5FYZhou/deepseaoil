using DeepseaOil.Logic.Events;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using Unity.VisualScripting.FullSerializer;
using System;
using Unity.VisualScripting;

/// <summary>
/// 全局单例，由GameRoot驱动
/// PlaySfx(AudioId id)播放音效，对每个播放，从池中取一个AudioSource
/// PlayBgm(AudioId id)播放音乐，固定一个AudioSource
/// AudioSource都放在一个跨场景不销毁的AudioRoot下
/// SetSfxVolume(float value)修改音效音量大小，0-1f
/// SetBgmVolume(float value)修改音乐音量大小，0-1f
/// </summary>

namespace DeepseaOil.Presentation {

    public sealed class PlayingEntry
    {
        public GameObject go;
        public AudioSource source;
        public float remaining;

        // 复用前重置
        public void Reset()
        {
            go = null;
            source = null;
            remaining = 0f;
        }
    }

    public sealed class AudioManager : BaseManager<AudioManager>, IService
    {
        private bool initialized;

        private Dictionary<AudioId, string> _idToFileName = new();
        private string _path;
        private Dictionary<AudioId, AudioClip> _cache = new();

        private Transform _audioRootT;
        private GameObject _bgmGameObject;
        private AudioSource _bgmSource;
        private Pool<GameObject> _audioPool;
        private Pool<PlayingEntry> _entryPool;
        private readonly List<PlayingEntry> _playing = new();

        private float _bgmVolume;
        private float _sfxVolume;

        private const string CONFIGKEY = "Config/AudioConfig";

        public float BgmVolume { get => _bgmVolume; }
        public float SfxVolume { get => _sfxVolume; }

        private AudioManager()
        {
            LoadConfig(CONFIGKEY);
        }

        public void Init()
        {
            if (initialized)
                return;

            initialized = true;

            // 找到跨场景不销毁的作为所有播放器的父物体
            var rootgo = UnityEngine.Object.FindFirstObjectByType<AudioRoot>();
            if (rootgo == null)
            {
                GameObject g = new("AudioRoot");
                rootgo = g.AddComponent<AudioRoot>();
            }
            _audioRootT = rootgo.transform;

            // 初始化音效池
            _audioPool = new PoolInClass<GameObject>(
                factory: () =>
                {
                    var obj = new GameObject("Audio");
                    if (_audioRootT != null) obj.transform.SetParent(_audioRootT, false);
                    obj.AddComponent<AudioSource>();
                    return obj;
                },
                onGet: obj => obj.SetActive(true),
                onRelease: obj => obj.SetActive(false)
            );

            _entryPool = new PoolInClass<PlayingEntry>(
                factory: () => new PlayingEntry()
            );

            // 初始化音乐播放器
            if (_bgmGameObject == null)
            {
                _bgmGameObject = new GameObject("MusicAudioSource", typeof(AudioSource));
                _bgmGameObject.transform.parent = _audioRootT;

                _bgmSource = _bgmGameObject.GetComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.volume = _bgmVolume;
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var e = _playing[i];
                e.remaining -= unscaledDeltaTime;

                if (e.remaining <= 0f || !e.source.isPlaying)
                {
                    e.source.clip = null;

                    // 两个池各还各的
                    _audioPool.Release(e.go);
                    _playing.RemoveAt(i);

                    e.Reset();
                    _entryPool.Release(e);
                }
            }
        }

        public void Dispose()
        {
            //EventBus<RequestAudio>.Unsubscribe(PlaySfx);

            // 把还在播的回收掉
            foreach (var e in _playing)
            {
                e.source.clip = null;
                _audioPool.Release(e.go);
                e.Reset();
                _entryPool.Release(e);
            }
            _playing.Clear();
        }

        private void LoadConfig(string key)
        {
            var config = AssetModule.Load<AudioConfig>(key);

            if (config == null)
            {
                Debug.LogError("AudioConfig 加载失败");
                return;
            }

            _path = config.path;
            _bgmVolume = config.bgmVolume;
            _sfxVolume = config.sfxVolume;

            _idToFileName.Clear();

            // 把配置中的映射挪到字典中
            foreach (var m in config.AudioMaps)
            {
                if (m.id == AudioId.None)
                    continue;

                if (string.IsNullOrEmpty(m.fileName))
                {
                    Debug.LogWarning($"AudioConfig: {m.id} 没有配置文件名");
                    continue;
                }
                if (!_idToFileName.TryAdd(m.id, m.fileName))
                {
                    Debug.LogWarning($"AudioConfig: 重复的 AudioId：{m.id}");
                }
            }

        }

        private AudioClip GetClip(AudioId id)
        {
            if (!_idToFileName.ContainsKey(id))
            {
                Debug.LogError($"无{id}配置");
                return null;
            }
            string name = _idToFileName[id];
            if (name == null)
                Debug.LogWarning($"{id}对应的文件名为空");

            string key = _path + "\\" + name;

            if (!_cache.TryGetValue(id, out var clip))
            {
                clip = AssetModule.Load<AudioClip>(key);
                if (clip != null)
                    _cache[id] = clip;
                else
                    Debug.LogWarning($"未正确加载{id}对应的音频文件");
            }
            return clip;
        }

        public void PlaySfx(AudioId id)
        {
            var clip = GetClip(id);

            if (clip == null)
                return;

            var go = _audioPool.Get();

            if (_audioRootT != null && go.transform.parent != _audioRootT)
                go.transform.SetParent(_audioRootT, false);

            var a = go.GetComponent<AudioSource>();
            if (a == null) a = go.AddComponent<AudioSource>();

            a.clip = clip;
            a.volume = _sfxVolume;
            a.Play();

            var entry = _entryPool.Get();
            entry.go = go;
            entry.source = a;
            entry.remaining = clip.length;
            _playing.Add(entry);
        }

        public void PlayBgm(AudioId id)
        {
            var clip = GetClip(id);

            if (clip == null)
                return;

            _bgmSource.clip = clip;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);
            _bgmSource.GetComponent<AudioSource>().volume = _bgmVolume;
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
            for(int i = 0; i < _playing.Count; i++)
            {
                _playing[i].source.volume = _sfxVolume;
            }
        }

    }
}
