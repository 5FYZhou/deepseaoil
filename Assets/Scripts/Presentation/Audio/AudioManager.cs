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

        // 复用前重置
        public void Reset()
        {
            go = null;
            source = null;
            remaining = 0f;
        }
    }

    /// <summary>
    /// 音频：<c>PlaySfx</c> 从池里取一个 <c>AudioSource</c> 播一次、<c>PlayBgm</c> 用固定一个循环播。
    /// <b>普通类，由 <c>GameRoot</c> 持有</b>，作为 <c>IService</c> 每帧被驱动（回收播完的音效）。
    /// </summary>
    /// <remarks>
    /// 播放器都挂在一个跨场景不销毁的音频根下 —— 那个根现在是 <c>GameRoot</c> 的子物体
    /// （<c>GameRoot</c> 本身常驻，所以子物体自然跨场景）。
    /// <para><b>收口前它的问题（本次一并清掉）：</b></para>
    /// <list type="number">
    /// <item>它是 <c>BaseManager&lt;AudioManager&gt;</c>（反射取私有构造的伪单例）；</item>
    /// <item><b>私有构造里直接读配置</b>（<c>AssetModule.Load</c>）——"new 一个对象"不该产生资源 IO；</item>
    /// <item>音频根有<b>两条创建路径</b>（<c>AudioRoot.Awake</c> 自建 / <c>Init</c> 里找不到就 new 一个）；
    /// 现在唯一入口是 <see cref="Init"/>，宿主由构造参数给出；</item>
    /// <item><c>Dispose</c> 里留着一行注释掉的 <c>EventBus</c> 退订、<c>SetBgmVolume</c> 里绕了一层
    /// <c>GetComponent</c>、若干无用 <c>using</c>。</item>
    /// </list>
    /// <para><c>Dispose</c> 必须幂等（它经服务表被调，也可能在编辑器里被重复触发）。</para>
    /// </remarks>
    public sealed class AudioManager : IService
    {
        private const string CONFIGKEY = "Config/AudioConfig";

        /// <summary>音频根的宿主（<c>GameRoot</c> 的 <c>Transform</c>）。</summary>
        private readonly Transform _host;

        private bool _initialized;

        private readonly Dictionary<AudioId, string> _idToFileName = new();
        private readonly Dictionary<AudioId, AudioClip> _cache = new();
        private string _path;

        private GameObject _rootGo;
        private Transform _audioRootT;
        private GameObject _bgmGameObject;
        private AudioSource _bgmSource;
        private Pool<GameObject> _audioPool;
        private Pool<PlayingEntry> _entryPool;
        private readonly List<PlayingEntry> _playing = new();

        private float _bgmVolume;
        private float _sfxVolume;

        /// <summary>装配是否完成（未完成时播放与音量设置是 no-op）。</summary>
        public bool IsReady => _initialized;

        public float BgmVolume { get => _bgmVolume; }
        public float SfxVolume { get => _sfxVolume; }

        /// <param name="host">音频根的宿主；传场景根之外的对象会让音频随它一起消失。</param>
        public AudioManager(Transform host)
        {
            // 构造**不做事**：只记宿主，资源 IO 全在 Init 里
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

            // 音频根：GameRoot 的子物体（GameRoot 常驻 ⇒ 音频根常驻）
            _rootGo = new GameObject("AudioRoot");
            _rootGo.transform.SetParent(_host, false);
            _audioRootT = _rootGo.transform;

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
            _bgmGameObject = new GameObject("MusicAudioSource", typeof(AudioSource));
            _bgmGameObject.transform.SetParent(_audioRootT, false);

            _bgmSource = _bgmGameObject.GetComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;

            _initialized = true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_initialized) return;

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

        /// <summary>拆除：停掉在播的音效、还清资源引用、销毁音频根。<b>幂等。</b></summary>
        /// <remarks>必须早于 <c>AssetModule.Dispose</c>（归还引用计数要经它）。</remarks>
        public void Dispose()
        {
            if (!_initialized) return;

            _initialized = false;

            // 把还在播的回收掉
            foreach (var e in _playing)
            {
                e.source.clip = null;
                _audioPool.Release(e.go);
                e.Reset();
                _entryPool.Release(e);
            }
            _playing.Clear();

            // 把加载过的音频还给 AssetModule
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

        /// <summary>
        /// 音频资源 Key：<c>&lt;配置里的目录&gt;/&lt;文件名&gt;</c>。
        /// </summary>
        /// <remarks>
        /// 配置里 <c>path = "audio"</c>、映射里是 <c>Jump.wav</c>，拼出来是
        /// <c>audio/Jump.wav</c> —— <c>AssetRegistry.ResolvePath</c> 会去掉扩展名。
        /// （收口前这里写的是硬编码反斜杠：<c>ResolvePath</c> 第一步就把 <c>\</c> 换成 <c>/</c>，
        /// 所以它能工作，但"能工作"与"该这么写"是两件事。）
        /// </remarks>
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

        public void PlaySfx(AudioId id)
        {
            if (!_initialized) return;

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

        public void PlayBgm(AudioId id)
        {
            if (!_initialized) return;

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

            if (_bgmSource != null) _bgmSource.volume = _bgmVolume;
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++)
            {
                _playing[i].source.volume = _sfxVolume;
            }
        }
    }
}
