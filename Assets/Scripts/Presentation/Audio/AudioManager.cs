using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Service;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation
{
    /// <summary>一次音效播放的记账条目，池化复用</summary>
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

    /// <remarks>EnqueueSfx/EnqueueBgm 只入队，播放统一在 Tick 里消费；单帧只消费本 Tick 开始时已有的请求，播放中新产生的留到下一 Tick。由 GameRoot 持有并驱动。队列只为请求/消费解耦，非线程安全设施。</remarks>
    public sealed class AudioManager : IService
    {
        private enum AudioRequestType
        {
            Sfx,
            Bgm,
        }

        private readonly struct AudioRequest
        {
            public readonly AudioRequestType type;
            public readonly AudioId id;
            public readonly bool resumeBgm;   // 仅 Bgm 请求有效

            public AudioRequest(AudioRequestType type, AudioId id, bool resumeBgm = false)
            {
                this.type = type;
                this.id = id;
                this.resumeBgm = resumeBgm;
            }
        }

        /// <summary>BGM 渐变状态</summary>
        private enum BgmFadeState
        {
            None,       // 无渐变，正常播放
            FadingOut,  // 正在淡出，淡出完成后切歌/停止并淡入
            FadingIn,   // 正在淡入
        }

        private readonly Transform _host;

        private bool _initialized;

        private readonly Dictionary<AudioId, string> _idToFileName = new();
        private readonly Dictionary<AudioId, AudioClip> _cache = new();

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

        // BGM 淡入淡出
        private float _bgmFadeTime = 0.3f;
        private BgmFadeState _bgmFadeState = BgmFadeState.None;
        private AudioClip _pendingBgmClip;
        private bool _pendingBgmResume;   // 挂起的续播意图（淡出完成后切歌时使用）
        private float _bgmFadeElapsed;
        private float _bgmFadeStartVolume;
        private float _bgmFadeTargetVolume;

        // BGM 断点续播：每个 clip 上次停下的进度
        private readonly Dictionary<AudioClip, float> _bgmPlayedTime = new();

        public bool IsReady => _initialized;

        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        public AudioManager(Transform host)
        {
            _host = host;
        }

        public void Init()
        {
            if (_initialized)
            {
                Debug.LogError("[Audio] AudioManager.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            LoadConfig();

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

        /// <summary>先消费队列、再回收播完的音效，顺序反了会让本帧入队的音效晚一帧才播</summary>
        /// <remarks>用 unscaled 那个：音频不参与暂停冻结，否则 _playing 会在暂停时挂着播完的条目。</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized) return;

            ProcessRequests();

            UpdatePlaying(unscaledDeltaTime);

            UpdateBgmFade(unscaledDeltaTime);
        }

        // 消息队列

        /// <summary>请求播放音效，只入队</summary>
        public void EnqueueSfx(AudioId id)
        {
            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Sfx, id));
        }

        /// <summary>请求播放 BGM，只入队。resumeFromLastTime 决定本次是否从该 clip 上次停止的进度继续。</summary>
        public void EnqueueBgm(AudioId id, bool resumeFromLastTime = true)
        {
            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Bgm, id, resumeFromLastTime));
        }

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
                        PlayBgmInternal(request.id, request.resumeBgm);
                        break;
                }
            }
        }

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

        /// <summary>拆除：清空待播请求、停掉在播的音效、还清资源引用、销毁音频根，幂等</summary>
        /// <remarks>必须早于 AssetModule.Dispose（归还引用计数要经它）。</remarks>
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

            // BGM 渐变与续播状态复位（_bgmSource 随即置 null，保持幂等干净）
            _bgmFadeState = BgmFadeState.None;
            _pendingBgmClip = null;
            _pendingBgmResume = false;
            _bgmFadeElapsed = 0f;
            _bgmPlayedTime.Clear();

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

        private void PlayBgmInternal(AudioId id, bool resumeFromLastTime)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            // 正在播同一个目标（含淡入中）→ 忽略；淡出中允许覆盖待切目标
            if (clip == _bgmSource.clip
                && _bgmSource.isPlaying
                && _bgmFadeState != BgmFadeState.FadingOut)
                return;

            if (_bgmSource.clip == null || !_bgmSource.isPlaying)
            {
                // 当前没在播，直接切 clip 并淡入（按参数决定是否续播）
                SwitchBgmClip(clip, resumeFromLastTime);
                StartFadeIn();
            }
            else
            {
                // 正在播，先淡出，淡出完成后切 clip 再淡入
                _pendingBgmClip = clip;
                _pendingBgmResume = resumeFromLastTime;
                StartFadeOut();
            }
        }

        /// <summary>切换 BGM clip：从 0 音量起播，交给后续淡入推音量；按参数决定是否从上次进度续播</summary>
        private void SwitchBgmClip(AudioClip clip, bool resumeFromLastTime)
        {
            // 切走前先记住当前 clip 的进度（无论下次续不续播，进度都要更新，避免旧进度污染）
            RememberCurrentBgmTime();

            _bgmSource.clip = clip;
            _bgmSource.volume = 0f;

            if (resumeFromLastTime
                && _bgmPlayedTime.TryGetValue(clip, out float t)
                && t > 0f && t < clip.length)
            {
                _bgmSource.time = t;
            }
            else
            {
                _bgmSource.time = 0f;
            }

            _bgmSource.Play();
        }

        /// <summary>把当前 BGM 的播放进度写回字典（在切走/停止前调用）</summary>
        private void RememberCurrentBgmTime()
        {
            if (_bgmSource == null) return;
            var cur = _bgmSource.clip;
            if (cur == null) return;
            _bgmPlayedTime[cur] = _bgmSource.time;
        }

        private void StartFadeIn()
        {
            _bgmFadeState = BgmFadeState.FadingIn;
            _bgmFadeElapsed = 0f;
            _bgmFadeStartVolume = _bgmSource.volume;
            _bgmFadeTargetVolume = _bgmVolume;
            _pendingBgmClip = null;
            _pendingBgmResume = false;
        }

        private void StartFadeOut()
        {
            _bgmFadeState = BgmFadeState.FadingOut;
            _bgmFadeElapsed = 0f;
            _bgmFadeStartVolume = _bgmSource.volume;
            _bgmFadeTargetVolume = 0f;
        }

        /// <summary>推进 BGM 淡入淡出。用 unscaled 时间，和音效回收一致（音频不参与暂停）。</summary>
        private void UpdateBgmFade(float unscaledDeltaTime)
        {
            if (_bgmFadeState == BgmFadeState.None)
                return;

            if (_bgmFadeTime <= 0f)
            {
                // 时长为 0 视为瞬时切换，避免除零
                FinishBgmFade();
                return;
            }

            _bgmFadeElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_bgmFadeElapsed / _bgmFadeTime);
            _bgmSource.volume = Mathf.Lerp(_bgmFadeStartVolume, _bgmFadeTargetVolume, t);

            if (t >= 1f)
                FinishBgmFade();
        }

        private void FinishBgmFade()
        {
            switch (_bgmFadeState)
            {
                case BgmFadeState.FadingOut:
                    _bgmSource.volume = 0f;
                    if (_pendingBgmClip != null)
                    {
                        var next = _pendingBgmClip;
                        var resume = _pendingBgmResume;
                        _pendingBgmClip = null;
                        _pendingBgmResume = false;
                        SwitchBgmClip(next, resume); // 内部会先 RememberCurrentBgmTime
                        StartFadeIn();
                        return;              // 保持 FadingIn 状态，本帧不再收尾
                    }
                    RememberCurrentBgmTime();
                    _bgmSource.Stop();
                    _bgmFadeState = BgmFadeState.None;
                    break;

                case BgmFadeState.FadingIn:
                    _bgmSource.volume = _bgmVolume;
                    _bgmFadeState = BgmFadeState.None;
                    break;

                default:
                    _bgmFadeState = BgmFadeState.None;
                    break;
            }
        }

        // 音频资源

        /// <summary>音频资源 Key：&lt;配置目录&gt;/&lt;文件名&gt;，ResolvePath 会去掉扩展名</summary>
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

        private void LoadConfig()
        {
            var config = ConfigModule.GetAudioConfig();

            if (config == null)
            {
                Debug.LogError("AudioConfig 加载失败");
                return;
            }

            _path = config.path;
            _bgmVolume = config.bgmVolume;
            _sfxVolume = config.sfxVolume;

            // 淡入淡出时长：优先读配置；若 AudioConfig 暂无该字段，保留字段初始值 0.3f
            _bgmFadeTime = Mathf.Max(0f, config.bgmFadeDuration);

            _idToFileName.Clear();

            foreach (var m in ConfigModule.GetAudioMaps())
            {
                if (string.IsNullOrEmpty(m.FileName))
                {
                    Debug.LogWarning($"audio_map: {m.Id} 没有配置文件名");
                    continue;
                }

                if (!_idToFileName.TryAdd(m.Id, m.FileName))
                    Debug.LogWarning($"audio_map: 重复的 AudioId：{m.Id}");
            }
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);

            if (_bgmSource == null) return;

            if (_bgmFadeState == BgmFadeState.FadingIn)
            {
                // 淡入中：更新目标音量，让本帧起 Lerp 收敛到新值
                _bgmFadeTargetVolume = _bgmVolume;
            }
            else if (_bgmFadeState == BgmFadeState.FadingOut)
            {
                // 淡出中：目标恒为 0，不动
            }
            else
            {
                _bgmSource.volume = _bgmVolume;
            }
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++)
                _playing[i].source.volume = _sfxVolume;
        }
    }
}