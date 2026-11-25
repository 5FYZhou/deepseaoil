using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Service;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation
{
    public sealed class PlayingEntry
    {
        public GameObject go;
        public AudioSource source;
        public float remaining;
        public float fadeTail;

        public void Reset()
        {
            go = null;
            source = null;
            remaining = 0f;
            fadeTail = 0f;
        }
    }

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
            public readonly bool resumeBgm;
            public readonly bool isLoop;
            public readonly bool fade;

            public AudioRequest(AudioRequestType type, AudioId id, bool resumeBgm = false, bool isLoop = true, bool fade = true)
            {
                this.type = type;
                this.id = id;
                this.resumeBgm = resumeBgm;
                this.isLoop = isLoop;
                this.fade = fade;
            }
        }

        private enum BgmFadeState
        {
            None,
            FadingOut,
            FadingIn,
        }

        private enum UiClipKind
        {
            Blip,
            SlideIn,
            SlideOut,
        }

        private const int PriorityBgm = 0;
        private const int PriorityLoopSfx = 64;
        private const int PriorityUiSfx = 96;
        private const int PrioritySfx = 128;

        private const int MaxConcurrentSfx = 16;

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

        private AudioSource _loopSfxSource;

        private AudioSource _uiBlipSource;
        private AudioSource _uiSlideSource;

        private AudioClip _typewriterBlipClip;
        private AudioClip _popupSlideInClip;
        private AudioClip _popupSlideOutClip;

        private string _typewriterBlipLoadedKey;
        private string _popupSlideInLoadedKey;
        private string _popupSlideOutLoadedKey;

        private bool _typewriterBlipGenerated;
        private bool _popupSlideInGenerated;
        private bool _popupSlideOutGenerated;

        private float _typewriterBlipCooldown = SfxPlayback.DefaultUiBlipCooldown;
        private float _uiSfxVolumeScale = 0.95f;
        private float _nextUiBlipTime;
        private int _uiBlipSequence;

        private Pool<GameObject> _audioPool;
        private Pool<PlayingEntry> _entryPool;

        private float _bgmVolume;
        private float _sfxVolume;

        private float _bgmFadeTime = 0.3f;
        private BgmFadeState _bgmFadeState = BgmFadeState.None;

        private bool _bgmShouldPlay;
        private bool _bgmStopWarned;
        private AudioClip _pendingBgmClip;
        private bool _pendingBgmResume;
        private bool _pendingBgmIsLoop;
        private float _bgmFadeElapsed;
        private float _bgmFadeStartVolume;
        private float _bgmFadeTargetVolume;

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

            _rootGo = new GameObject("AudioRoot");
            _rootGo.transform.SetParent(_host, false);
            _audioRootT = _rootGo.transform;

            _audioPool = new Pool<GameObject>(
                factory: () =>
                {
                    var obj = new GameObject("Audio");
                    if (_audioRootT != null) obj.transform.SetParent(_audioRootT, false);
                    var src = obj.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.priority = PrioritySfx;
                    obj.SetActive(false);
                    return obj;
                },
                onGet: obj => obj.SetActive(true),
                onRelease: obj => obj.SetActive(false)
            );

            _entryPool = new Pool<PlayingEntry>(
                factory: () => new PlayingEntry()
            );

            _audioPool.Prewarm(MaxConcurrentSfx);
            _entryPool.Prewarm(MaxConcurrentSfx);

            _bgmGameObject = new GameObject("MusicAudioSource", typeof(AudioSource));
            _bgmGameObject.transform.SetParent(_audioRootT, false);

            _bgmSource = _bgmGameObject.GetComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.priority = PriorityBgm;

            var loopSfxGo = new GameObject("LoopSfxAudioSource", typeof(AudioSource));
            loopSfxGo.transform.SetParent(_audioRootT, false);

            _loopSfxSource = loopSfxGo.GetComponent<AudioSource>();
            _loopSfxSource.loop = true;
            _loopSfxSource.playOnAwake = false;
            _loopSfxSource.volume = _sfxVolume;
            _loopSfxSource.priority = PriorityLoopSfx;

            _uiBlipSource = CreateUiSource("UiBlipAudioSource");
            _uiSlideSource = CreateUiSource("UiSlideAudioSource");

            _initialized = true;
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized) return;

            ProcessRequests();

            UpdatePlaying(unscaledDeltaTime);

            UpdateBgmFade(unscaledDeltaTime);

            KeepBgmAlive();
        }

        private void KeepBgmAlive()
        {
            if (!_bgmShouldPlay || _bgmFadeState != BgmFadeState.None) return;
            if (_bgmSource == null || _bgmSource.clip == null || !_bgmSource.loop) return;
            if (_bgmSource.isPlaying) return;

            _bgmSource.Play();

            if (_bgmStopWarned) return;

            _bgmStopWarned = true;

            Debug.LogWarning("[Audio] 循环 BGM 被外部停掉，已自动恢复。若这条日志出现在战斗里，" +
                             "说明除 AudioManager 之外还有人在停 AudioSource。");
        }

        public void EnqueueSfx(AudioId id)
        {
            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Sfx, id));
        }

        public void EnqueueBgm(AudioId id, bool resumeFromLastTime = true, bool isLoop = true, bool fade = true)
        {
            _requestQueue.Enqueue(new AudioRequest(AudioRequestType.Bgm, id, resumeFromLastTime, isLoop, fade));
        }

        public void SetLoopSfx(AudioId id, bool play)
        {
            if (!_initialized || _loopSfxSource == null) return;

            if (!play || id == AudioId.None)
            {
                if (_loopSfxSource.isPlaying) _loopSfxSource.Stop();

                return;
            }

            var clip = GetClip(id);

            if (clip == null) return;

            if (_loopSfxSource.clip == clip && _loopSfxSource.isPlaying) return;

            _loopSfxSource.clip = clip;
            _loopSfxSource.volume = _sfxVolume;
            _loopSfxSource.Play();
        }

        public void StopAllSfx(bool clearPending = true)
        {
            if (!_initialized) return;

            if (clearPending)
                ClearPendingSfxRequests();

            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var entry = _playing[i];

                if (entry.source != null)
                {
                    entry.source.Stop();
                    entry.source.clip = null;
                }

                if (entry.go != null)
                    _audioPool?.Release(entry.go);

                entry.Reset();
                _entryPool?.Release(entry);
                _playing.RemoveAt(i);
            }

            if (_loopSfxSource != null)
            {
                if (_loopSfxSource.isPlaying) _loopSfxSource.Stop();
                _loopSfxSource.clip = null;
            }

            StopUiSfx();
        }

        private void ClearPendingSfxRequests()
        {
            if (_requestQueue.Count == 0) return;

            int count = _requestQueue.Count;
            var kept = new List<AudioRequest>(count);

            for (int i = 0; i < count; i++)
            {
                var req = _requestQueue.Dequeue();
                if (req.type != AudioRequestType.Sfx)
                    kept.Add(req);
            }

            for (int i = 0; i < kept.Count; i++)
                _requestQueue.Enqueue(kept[i]);
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
                        PlayBgmInternal(request.id, request.resumeBgm, request.isLoop, request.fade);
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

                if (entry.fadeTail > 0f && entry.remaining < entry.fadeTail && entry.remaining > 0f)
                    entry.source.volume = _sfxVolume * (entry.remaining / entry.fadeTail);

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

        public void Dispose()
        {
            if (!_initialized) return;

            _initialized = false;

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

            _bgmFadeState = BgmFadeState.None;
            _bgmShouldPlay = false;
            _pendingBgmClip = null;
            _pendingBgmResume = false;
            _pendingBgmIsLoop = true;
            _bgmFadeElapsed = 0f;
            _bgmPlayedTime.Clear();

            _audioPool?.Dispose();
            _entryPool?.Dispose();
            _audioPool = null;
            _entryPool = null;

            _bgmSource = null;
            _bgmGameObject = null;

            _loopSfxSource = null;
            _uiBlipSource = null;
            _uiSlideSource = null;

            ReleaseUiClip(_typewriterBlipLoadedKey);
            ReleaseUiClip(_popupSlideInLoadedKey);
            ReleaseUiClip(_popupSlideOutLoadedKey);

            DestroyGeneratedClip(ref _typewriterBlipClip, _typewriterBlipGenerated);
            DestroyGeneratedClip(ref _popupSlideInClip, _popupSlideInGenerated);
            DestroyGeneratedClip(ref _popupSlideOutClip, _popupSlideOutGenerated);

            _typewriterBlipLoadedKey = null;
            _popupSlideInLoadedKey = null;
            _popupSlideOutLoadedKey = null;
            _typewriterBlipGenerated = false;
            _popupSlideInGenerated = false;
            _popupSlideOutGenerated = false;

            if (_rootGo != null) Object.Destroy(_rootGo);
            _rootGo = null;
            _audioRootT = null;
        }

        public void PlayUiBlip()
        {
            if (!_initialized
                || _uiBlipSource == null
                || _typewriterBlipClip == null
                || !SfxPlayback.TryReserveUiBlip(
                    Time.unscaledTime,
                    _typewriterBlipCooldown,
                    ref _nextUiBlipTime))
            {
                return;
            }

            PlayUiClip(
                _uiBlipSource,
                _typewriterBlipClip,
                SfxPlayback.UiBlipPitch(_uiBlipSequence++));
        }

        public void PlayUiSlide(bool entering)
        {
            if (!_initialized || _uiSlideSource == null)
                return;

            AudioClip clip = entering ? _popupSlideInClip : _popupSlideOutClip;

            if (clip == null)
                return;

            PlayUiClip(_uiSlideSource, clip, 1f);
        }

        public void StopUiSfx()
        {
            if (_uiBlipSource != null)
                _uiBlipSource.Stop();

            if (_uiSlideSource != null)
                _uiSlideSource.Stop();
        }

        private void PlaySfxInternal(AudioId id)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            if (_playing.Count >= MaxConcurrentSfx)
                return;

            for (int i = 0; i < _playing.Count; i++)
            {
                if (_playing[i].source.clip == clip)
                    return;
            }

            var go = _audioPool.Get();

            if (go == null) return;

            if (_audioRootT != null && go.transform.parent != _audioRootT)
                go.transform.SetParent(_audioRootT, false);

            var a = go.GetComponent<AudioSource>();
            if (a == null)
            {
                a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
            }

            if (a.isPlaying) a.Stop();

            a.clip = clip;
            a.volume = _sfxVolume;
            a.priority = PrioritySfx;
            a.loop = false;
            a.Play();

            var entry = _entryPool.Get();
            entry.go = go;
            entry.source = a;
            entry.remaining = SfxPlayback.SecondsOf(id, clip.length);
            entry.fadeTail = SfxPlayback.IsCapped(id, clip.length) ? SfxPlayback.FadeTailSeconds : 0f;
            _playing.Add(entry);
        }

        private void PlayBgmInternal(AudioId id, bool resumeFromLastTime, bool isLoop, bool fade)
        {
            var clip = GetClip(id);
            if (clip == null)
                return;

            if (!isLoop)
                resumeFromLastTime = false;

            if (!fade)
            {
                _bgmFadeState = BgmFadeState.None;
                _bgmFadeElapsed = 0f;
                _pendingBgmClip = null;
                _pendingBgmResume = false;
                _pendingBgmIsLoop = true;

                SwitchBgmClip(clip, resumeFromLastTime, isLoop);
                _bgmSource.volume = _bgmVolume;
                return;
            }

            if (isLoop
                && clip == _bgmSource.clip
                && _bgmSource.isPlaying
                && _bgmFadeState != BgmFadeState.FadingOut)
                return;

            if (_bgmSource.clip == null || !_bgmSource.isPlaying)
            {
                SwitchBgmClip(clip, resumeFromLastTime, isLoop);
                StartFadeIn();
            }
            else
            {
                _pendingBgmClip = clip;
                _pendingBgmResume = resumeFromLastTime;
                _pendingBgmIsLoop = isLoop;
                StartFadeOut();
            }
        }

        private void SwitchBgmClip(AudioClip clip, bool resumeFromLastTime, bool isLoop)
        {
            RememberCurrentBgmTime();

            _bgmSource.clip = clip;
            _bgmSource.volume = 0f;
            _bgmSource.loop = isLoop;
            _bgmSource.priority = PriorityBgm;

            if (isLoop
                && resumeFromLastTime
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
            _bgmShouldPlay = true;
        }

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
            _pendingBgmIsLoop = true;
        }

        private void StartFadeOut()
        {
            _bgmFadeState = BgmFadeState.FadingOut;
            _bgmFadeElapsed = 0f;
            _bgmFadeStartVolume = _bgmSource.volume;
            _bgmFadeTargetVolume = 0f;
        }

        private void UpdateBgmFade(float unscaledDeltaTime)
        {
            if (_bgmFadeState == BgmFadeState.None)
                return;

            if (_bgmFadeTime <= 0f)
            {
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
                        var loop = _pendingBgmIsLoop;
                        _pendingBgmClip = null;
                        _pendingBgmResume = false;
                        _pendingBgmIsLoop = true;
                        SwitchBgmClip(next, resume, loop);
                        StartFadeIn();
                        return;
                    }
                    RememberCurrentBgmTime();
                    _bgmSource.Stop();
                    _bgmShouldPlay = false;
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

        private static string ClipKey(string fileName) => "audio/" + fileName;

        private AudioClip GetClip(AudioId id)
        {
            if(id == AudioId.None) return null;
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
            _typewriterBlipCooldown = config.typewriterBlipCooldown;
            _uiSfxVolumeScale = Mathf.Clamp01(config.uiSfxVolumeScale);

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

            LoadUiClip(
                config.typewriterBlipKey,
                "UiTypewriterBlip",
                UiClipKind.Blip,
                out _typewriterBlipClip,
                out _typewriterBlipLoadedKey,
                out _typewriterBlipGenerated);

            LoadUiClip(
                config.popupSlideInKey,
                "UiPopupSlideIn",
                UiClipKind.SlideIn,
                out _popupSlideInClip,
                out _popupSlideInLoadedKey,
                out _popupSlideInGenerated);

            LoadUiClip(
                config.popupSlideOutKey,
                "UiPopupSlideOut",
                UiClipKind.SlideOut,
                out _popupSlideOutClip,
                out _popupSlideOutLoadedKey,
                out _popupSlideOutGenerated);
        }

        private AudioSource CreateUiSource(string name)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(_audioRootT, false);

            AudioSource source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.priority = PriorityUiSfx;
            source.volume = _sfxVolume * _uiSfxVolumeScale;

            return source;
        }

        private void PlayUiClip(AudioSource source, AudioClip clip, float pitch)
        {
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = _sfxVolume * _uiSfxVolumeScale;
            source.Play();
        }

        private void UpdateUiSourceVolume(AudioSource source)
        {
            if (source != null)
                source.volume = _sfxVolume * _uiSfxVolumeScale;
        }

        private static void LoadUiClip(
            string configuredKey,
            string fallbackName,
            UiClipKind kind,
            out AudioClip clip,
            out string loadedKey,
            out bool generated)
        {
            loadedKey = null;
            generated = false;
            clip = null;

            if (!string.IsNullOrEmpty(configuredKey))
            {
                clip = AssetModule.Load<AudioClip>(configuredKey);

                if (clip != null)
                {
                    loadedKey = configuredKey;
                    return;
                }
            }

            clip = CreateFallbackClip(fallbackName, kind);
            generated = clip != null;
        }

        private static AudioClip CreateFallbackClip(string name, UiClipKind kind)
        {
            const int sampleRate = 44100;

            float duration = kind == UiClipKind.Blip
                ? 0.048f
                : kind == UiClipKind.SlideIn ? 0.095f : 0.08f;

            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];

            float startFrequency = kind == UiClipKind.Blip ? 940f : kind == UiClipKind.SlideIn ? 420f : 560f;
            float endFrequency = kind == UiClipKind.Blip ? 760f : kind == UiClipKind.SlideIn ? 620f : 360f;
            float gain = kind == UiClipKind.Blip ? 0.55f : 0.3f;

            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = (float)i / sampleCount;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                float envelope = Mathf.Exp(-7f * progress) * Mathf.Min(1f, progress * 40f);

                phase += frequency / sampleRate;

                float sine = Mathf.Sin(phase * Mathf.PI * 2f);
                float triangle = 1f - 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f);

                samples[i] = (sine * 0.7f + triangle * 0.3f) * envelope * gain;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        private static void ReleaseUiClip(string key)
        {
            if (!string.IsNullOrEmpty(key))
                AssetModule.Release(key);
        }

        private static void DestroyGeneratedClip(ref AudioClip clip, bool generated)
        {
            if (generated && clip != null)
                Object.Destroy(clip);

            clip = null;
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);

            if (_bgmSource == null) return;

            if (_bgmFadeState == BgmFadeState.FadingIn)
            {
                _bgmFadeTargetVolume = _bgmVolume;
            }
            else if (_bgmFadeState != BgmFadeState.FadingOut)
            {
                _bgmSource.volume = _bgmVolume;
            }
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++)
                _playing[i].source.volume = _sfxVolume;

            if (_loopSfxSource != null) _loopSfxSource.volume = _sfxVolume;

            UpdateUiSourceVolume(_uiBlipSource);
            UpdateUiSourceVolume(_uiSlideSource);
        }
    }
}
