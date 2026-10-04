using DeepseaOil.Foundation;
using DeepseaOil.Logic.Service;
using DeepseaOil.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局单例，由 GameRoot 驱动。
/// PlaySfx / PlayBgm 不直接执行，而是先进入消息队列，在 Tick 中统一消费。
/// PlaySfx：每次播放从池中取一个 AudioSource。
/// PlayBgm：固定使用一个 AudioSource。
/// AudioSource 都放在一个跨场景不销毁的 AudioRoot 下。
/// SetSfxVolume：修改音效音量大小，0-1f。
/// SetBgmVolume：修改音乐音量大小，0-1f。
/// </summary>
namespace DeepseaOil.Presentation
{
    
    public sealed class AudioManager : BaseManager<AudioManager>, IService
    {
        private enum AudioRequestType{ Sfx, Bgm }

        private readonly struct AudioRequest
        {
            public readonly AudioRequestType type;
            public readonly AudioId id;

            public AudioRequest(AudioRequestType type, AudioId id)
            {
                this.type = type;
                this.id = id;
            }
        }
        
        private sealed class PlayingEntry
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



        private bool initialized;

        private readonly Dictionary<AudioId, string> _idToFileName = new();
        private readonly Dictionary<AudioId, AudioClip> _cache = new();
        private readonly Queue<AudioRequest> _requestQueue = new();
        private readonly List<PlayingEntry> _playing = new();

        private string _path;

        private Transform _audioRootT;

        private GameObject _bgmGameObject;
        private AudioSource _bgmSource;

        private Pool<GameObject> _audioPool;
        private Pool<PlayingEntry> _entryPool;

        private float _bgmVolume;
        private float _sfxVolume;

        private const string CONFIGKEY = "Config/AudioConfig";

        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        private AudioManager()
        {
            LoadConfig(CONFIGKEY);
        }

        public void Init()
        {
            if (initialized)
                return;

            initialized = true;

            // 找到跨场景不销毁的 AudioRoot，作为所有播放器的父物体
            var rootgo = UnityEngine.Object.FindFirstObjectByType<AudioRoot>();
            if (rootgo == null)
            {
                GameObject g = new GameObject("AudioRoot");
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

            // 初始化 PlayingEntry 池
            _entryPool = new PoolInClass<PlayingEntry>(
                factory: () => new PlayingEntry()
            );

            // 初始化音乐播放器
            if (_bgmGameObject == null)
            {
                _bgmGameObject = new GameObject("MusicAudioSource", typeof(AudioSource));
                _bgmGameObject.transform.SetParent(_audioRootT, false);

                _bgmSource = _bgmGameObject.GetComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.volume = _bgmVolume;
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            ProcessRequests();
            UpdatePlaying(unscaledDeltaTime);
        }

        // 消息队列
        /// <summary>
        /// 请求播放音效，只入队，不立即操作 Unity AudioSource。
        /// </summary>
        public void EnqueueSfx(AudioId id)
        {
            if (id == AudioId.None)
                return;

            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Sfx, id));
        }

        /// <summary>
        /// 请求播放 BGM，只入队，不立即操作 Unity AudioSource。
        /// </summary>
        public void EnqueueBgm(AudioId id)
        {
            if (id == AudioId.None)
                return;

            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Bgm, id));
        }

        /// <summary>
        /// 消费本次 Tick 开始时已经存在的请求。
        /// 本 Tick 处理过程中产生的新请求留到下一 Tick。
        /// </summary>
        private void ProcessRequests()
        {
            int count = _requestQueue.Count;

            for (int i = 0; i < count; i++)
            {
                AudioRequest request = _requestQueue.Dequeue();

                switch (request.type)
                {
                    case AudioRequestType.Sfx:
                        PlaySfxInternal(request.id);
                        break;

                    case AudioRequestType.Bgm:
                        PlayBgmInternal(request.id);
                        break;
                }
            }
        }

        // 音效播放维护
        private void UpdatePlaying(float unscaledDeltaTime)
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var entry = _playing[i];
                entry.remaining -= unscaledDeltaTime;

                if (entry.remaining <= 0f || !entry.source.isPlaying)
                {
                    entry.source.clip = null;
                    _audioPool.Release(entry.go);
                    _playing.RemoveAt(i);

                    entry.Reset();
                    _entryPool.Release(entry);
                }
            }
        }

        // 实际播放
        /// <summary>
        /// 真正播放音效，只由 AudioManager 在 Tick 中调用。
        /// </summary>
        private void PlaySfxInternal(AudioId id)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            var go = _audioPool.Get();

            if (_audioRootT != null && go.transform.parent != _audioRootT)
                go.transform.SetParent(_audioRootT, false);

            var source = go.GetComponent<AudioSource>();
            if (source == null)
                source = go.AddComponent<AudioSource>();

            source.clip = clip;
            source.volume = _sfxVolume;
            source.Play();

            var entry = _entryPool.Get();
            entry.go = go;
            entry.source = source;
            entry.remaining = clip.length;

            _playing.Add(entry);
        }

        /// <summary>
        /// 真正播放 BGM，只由 AudioManager 在 Tick 中调用。
        /// </summary>
        private void PlayBgmInternal(AudioId id)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            _bgmSource.clip = clip;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
        }


        // 音频资源
        private AudioClip GetClip(AudioId id)
        {
            if (!_idToFileName.ContainsKey(id))
            {
                Debug.LogError($"无 {id} 配置");
                return null;
            }

            string name = _idToFileName[id];

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"{id} 对应的文件名为空");
                return null;
            }

            string key = _path + "\\" + name;

            if (!_cache.TryGetValue(id, out var clip))
            {
                clip = AssetModule.Load<AudioClip>(key);

                if (clip != null)
                    _cache[id] = clip;
                else
                    Debug.LogWarning($"未正确加载 {id} 对应的音频文件");
            }

            return clip;
        }


        // 配置
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
                    Debug.LogWarning($"AudioConfig: 重复的 AudioId：{m.id}");
            }
        }


        // 音量
        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);

            if (_bgmSource != null)
                _bgmSource.volume = _bgmVolume;
        }
        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++)
                _playing[i].source.volume = _sfxVolume;
        }


        // 销毁
        public void Dispose()
        {
            _requestQueue.Clear();

            foreach (var entry in _playing)
            {
                if (entry.source != null)
                    entry.source.clip = null;

                if (entry.go != null)
                    _audioPool.Release(entry.go);

                entry.Reset();
                _entryPool.Release(entry);
            }

            _playing.Clear();

            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }
        }
    }
}