using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Service;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>一次音效播放的记账条目（池化复用）。</summary>
    public sealed class PlayingEntry
    {
        public GameObject go;
        public AudioSource source;
        public float remaining;

        public void Reset()
        {
            go = null;
            source = null;
            remaining = 0f;
        }
    }

    /// <summary>
    /// 音频：<c>EnqueueSfx</c> / <c>EnqueueBgm</c> <b>只入队</b>，真正的播放统一在 <see cref="Tick"/> 里消费 —— 播放请求与实际播放在时间上解耦，同一帧里的多次请求按入队顺序各播各的。
    /// 普通类、由 <c>GameRoot</c> 持有，作为 <c>IService</c> 每帧被驱动（先消费队列、再回收播完的音效）。
    /// </summary>
    /// <remarks>
    /// <b>单帧只消费"本 Tick 开始时已存在的请求"</b>：消费循环先取 <c>count</c> 再跑，于是播放过程中新产生的请求留到下一 Tick —— 没有它，一个在回调里自排队的调用点能让这一帧停不下来。
    /// 队列不是线程安全设施：全程主线程，它解决的是"请求/消费解耦"，不是并发。
    /// </remarks>
    public sealed class AudioManager : IService
    {
        private const string CONFIGKEY = "Config/AudioConfig";

        private enum AudioRequestType
        {
            Sfx,
            Bgm,
        }

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

        private readonly Transform _host;

        private bool _initialized;

        private readonly Dictionary<AudioId, string> _idToFileName = new();
        private readonly Dictionary<AudioId, AudioClip> _cache = new();

        /// <summary>待播放请求（先入先出）。只在 <c>GameRoot</c> 的服务通道（unscaled 时间）里被消费。</summary>
        private readonly Queue<AudioRequest> _requestQueue = new();

        private readonly List<PlayingEntry> _playing = new();

        private string _path;

        private GameObject _rootGo;
        private Transform _audioRootT;

        private GameObject _bgmGameObject;
        private AudioSource _bgmSource;

        private Pool<GameObject> _audioPool;
        private Pool<PlayingEntry> _entryPool;

        private float _bgmVolume;
        private float _sfxVolume;

        /// <summary>装配是否完成（未完成时播放与音量设置是 no-op）。</summary>
        public bool IsReady => _initialized;

        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        /// <param name="host">音频根的宿主；传场景根之外的对象会让音频随它一起消失。</param>
        public AudioManager(Transform host)
        {
            _host = host;
        }

        /// <summary>装配：读配置 + 建音频根 + 建两个池 + 建音乐播放器。<b>唯一调用点是 <c>GameRoot</c>。</b></summary>
        public void Init()
        {
            if (_initialized)
            {
                Debug.LogError("[Audio] AudioManager.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            LoadConfig(CONFIGKEY);

            // 音频根：GameRoot 的子物体（GameRoot 常驻 ⇒ 音频根跨场景常驻）
            _rootGo = new GameObject("AudioRoot");
            _rootGo.transform.SetParent(_host, false);
            _audioRootT = _rootGo.transform;

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

            _bgmGameObject = new GameObject("MusicAudioSource", typeof(AudioSource));
            _bgmGameObject.transform.SetParent(_audioRootT, false);

            _bgmSource = _bgmGameObject.GetComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;

            _initialized = true;
        }

        /// <summary>先消费队列、再回收播完的音效；顺序反了会让"本帧入队的音效"晚一帧才播。</summary>
        /// <remarks>用 <b>unscaled</b> 那个：音频不参与暂停冻结（暂停时已经在放的音效该照常回收，否则 <c>_playing</c> 会挂着一堆播完的条目）。</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized) return;

            ProcessRequests();

            UpdatePlaying(unscaledDeltaTime);
        }

        // 消息队列

        /// <summary>请求播放音效，<b>只入队</b>，不立即操作 Unity AudioSource；<c>AudioId.None</c> 直接丢弃。</summary>
        public void EnqueueSfx(AudioId id)
        {
            if (id == AudioId.None)
                return;

            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Sfx, id));
        }

        /// <summary>请求播放 BGM，<b>只入队</b>，不立即操作 Unity AudioSource；<c>AudioId.None</c> 直接丢弃。</summary>
        public void EnqueueBgm(AudioId id)
        {
            if (id == AudioId.None)
                return;

            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Bgm, id));
        }

        /// <summary>消费本次 Tick 开始时已经存在的请求；过程中新产生的请求留到下一 Tick。</summary>
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

        /// <summary>音效播放维护：倒序遍历，播完（或剩余时长耗尽）就归还两个池。</summary>
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

        /// <summary>拆除：清空待播请求、停掉在播的音效、还清资源引用、销毁音频根。<b>幂等。</b></summary>
        /// <remarks>必须早于 <c>AssetModule.Dispose</c>（归还引用计数要经它）。</remarks>
        public void Dispose()
        {
            if (!_initialized) return;

            _initialized = false;

            // 待播请求先丢：拆除之后再播出来的音效没有根可挂。
            _requestQueue.Clear();

            foreach (var e in _playing)
            {
                if (e.source != null) e.source.clip = null;

                if (e.go != null) _audioPool?.Release(e.go);

                e.Reset();
                _entryPool?.Release(e);
            }

            _playing.Clear();

            foreach (KeyValuePair<AudioId, AudioClip> kv in _cache)
            {
                if (_idToFileName.TryGetValue(kv.Key, out string fileName) && !string.IsNullOrEmpty(fileName))
                    AssetModule.Release(ClipKey(fileName));
            }
            _cache.Clear();

            // 池与音频根：销毁根即回收全部 AudioSource（池里的对象都是根的子物体）
            _audioPool?.Dispose();
            _entryPool?.Dispose();
            _audioPool = null;
            _entryPool = null;

            _bgmSource = null;
            _bgmGameObject = null;

            if (_rootGo != null) Object.Destroy(_rootGo);
            _rootGo = null;
            _audioRootT = null;
        }

        // 实际播放（只由 Tick → ProcessRequests 调用）

        private void PlaySfxInternal(AudioId id)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            var go = _audioPool.Get();

            if (go == null) return;

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

        /// <summary>音频资源 Key：<c>&lt;配置里的目录&gt;/&lt;文件名&gt;</c>（<c>AssetRegistry.ResolvePath</c> 会去掉扩展名）。</summary>
        private static string ClipKey(string fileName) => "audio/" + fileName;

        private AudioClip GetClip(AudioId id)
        {
            if (!_idToFileName.TryGetValue(id, out string name))
            {
                Debug.LogError($"无{id}配置");
                return null;
            }
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"{id}对应的文件名为空");
                return null;
            }

            if (_cache.TryGetValue(id, out var cached)) return cached;

            string key = ClipKey(name);
            var clip = AssetModule.Load<AudioClip>(key);

            if (clip != null) _cache[id] = clip;
            else Debug.LogWarning($"未正确加载{id}对应的音频文件");

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

            if (_bgmSource != null) _bgmSource.volume = _bgmVolume;
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++)
                _playing[i].source.volume = _sfxVolume;
        }
    }
}
