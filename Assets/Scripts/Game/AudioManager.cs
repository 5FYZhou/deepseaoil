using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Pool;
using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        public List<AudioClip> audios;
        private Pool<GameObject> audioPool;
        private Pool<PlayingEntry> entryPool;
        private Transform _parent;

        private readonly List<PlayingEntry> _playing = new();

        public AudioManager() { }

        public void Init(Transform parent)
        {
            EventBus<RequestAudio>.Subscribe(Play);

            _parent = parent;
            audioPool = new PoolInClass<GameObject>(
                factory: () =>
                {
                    var go = new GameObject("Audio");
                    if (_parent != null) go.transform.SetParent(_parent, false);
                    go.AddComponent<AudioSource>();
                    return go;
                }
            );

            entryPool = new PoolInClass<PlayingEntry>(
                factory: () => new PlayingEntry()
            );
        }
        public void Init() => Init(null);

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
                    audioPool.Release(e.go);
                    _playing.RemoveAt(i);

                    e.Reset();
                    entryPool.Release(e);
                }
            }
        }

        public void Play(RequestAudio type)
        {
            ///
            AudioClip clip = audios[0];
            ///
            var go = audioPool.Get();

            if (_parent != null && go.transform.parent != _parent)
                go.transform.SetParent(_parent, false);

            var a = go.GetComponent<AudioSource>();
            if (a == null) a = go.AddComponent<AudioSource>();

            a.clip = clip;
            a.Play();

            var entry = entryPool.Get();
            entry.go = go;
            entry.source = a;
            entry.remaining = clip.length;
            _playing.Add(entry);
        }

        public void Dispose()
        {
            EventBus<RequestAudio>.Unsubscribe(Play);

            // 顺手把还在播的回收掉
            foreach (var e in _playing)
            {
                e.source.clip = null;
                audioPool.Release(e.go);
                e.Reset();
                entryPool.Release(e);
            }
            _playing.Clear();
        }
    }
}
