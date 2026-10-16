using System;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Level;
using cfg.dso;

namespace DeepseaOil.Logic.Wave
{

    public enum WavePhase
    {

        Prep = 0,

        Battle = 1,

        Settle = 2,
    }

    public sealed class WaveLogic
    {
        public readonly struct SpawnRequest
        {
            // 要刷的怪种；出生点由表现层算
            public readonly int EnemyId;

            public readonly int WaveIndex;

            public readonly int Remaining;

            public SpawnRequest(int enemyId, int waveIndex, int remaining)
            {
                EnemyId = enemyId;
                WaveIndex = waveIndex;
                Remaining = remaining;
            }
        }

        private const float MinSpawnInterval = 0.05f;
        private const int MaxPhaseTransitionsPerTick = 3;

        private readonly IReadOnlyList<WaveSpec> _waves;
        private readonly WavePlayMode _playMode;

        // 本波待刷序列：开战那刻按 count 展开
        private readonly List<WaveEnemySpec> _pending = new List<WaveEnemySpec>();

        private int _cursor;

        private int _pendingCursor;

        private int _waveIndex = 1;

        private int _remaining;

        private float _timer;

        private WavePhase _phase = WavePhase.Prep;

        private bool _phaseEntered;

        private SeedType _pendingSeed = SeedType.None;

        private float _spawnTimer;

        private bool _isCompleted;

        private bool _settleDelayStarted;

        private bool _hasPublishedCountdown;

        private int _publishedCountdownWaveIndex;

        private WavePhase _publishedCountdownPhase;

        private int _publishedCountdownSeconds;

        public int WaveIndex => _waveIndex;

        public int Remaining => _remaining;

        public int WaveCount => _waves.Count;

        public WavePlayMode PlayMode => _playMode;

        public WavePhase Phase => _phase;

        public float PhaseRemaining => _timer > 0f ? _timer : 0f;

        public bool IsSequenceCompleted => _isCompleted;

        public bool IsSpawning => _phase == WavePhase.Battle && _pendingCursor < _pending.Count;

        public SeedType PendingSeedGrant => _pendingSeed;

        public string CurrentWaveName => Current != null ? Current.Name : string.Empty;

        public WaveLogic(
            IReadOnlyList<WaveSpec> waves,
            WavePlayMode playMode = WavePlayMode.Sequential)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (waves.Count == 0) throw new ArgumentException("At least one wave is required.", nameof(waves));
            if (!Enum.IsDefined(typeof(WavePlayMode), playMode))
                throw new ArgumentOutOfRangeException(nameof(playMode), playMode, "Unknown wave play mode.");

            _waves = waves;
            _playMode = playMode;
        }

        private WaveSpec Current
            => _waves[_playMode == WavePlayMode.Loop ? _cursor % _waves.Count : _cursor];

        public void Reset()
        {
            _cursor = 0;
            _waveIndex = 1;
            _remaining = 0;
            _timer = 0f;
            _phase = WavePhase.Prep;
            _phaseEntered = false;
            _pendingSeed = SeedType.None;
            _spawnTimer = 0f;
            _isCompleted = false;
            _settleDelayStarted = false;
            _hasPublishedCountdown = false;

            _pending.Clear();
            _pendingCursor = 0;
        }

        /// <summary>回填；待刷序列按 cursor 重展开</summary>
        public void ImportSave(WaveSaveData data)
        {
            if (data == null) return;

            _cursor = data.cursor < 0 ? 0 : (data.cursor >= _waves.Count ? _waves.Count - 1 : data.cursor);

            _waveIndex = data.waveIndex > 0 ? data.waveIndex : 1;

            _phase = data.phase == (int)WavePhase.Battle
                ? WavePhase.Battle
                : data.phase == (int)WavePhase.Settle ? WavePhase.Settle : WavePhase.Prep;

            _timer = Sanitize(data.timer);

            _spawnTimer = Sanitize(data.spawnTimer);

            _pendingSeed = Enum.IsDefined(typeof(SeedType), data.pendingSeed)
                ? (SeedType)data.pendingSeed
                : SeedType.None;

            _isCompleted = data.isCompleted;

            _settleDelayStarted = data.settleDelayStarted;

            FillPending(Current);

            _pendingCursor = data.pendingCursor < 0
                ? 0
                : (data.pendingCursor > _pending.Count ? _pending.Count : data.pendingCursor);

            _remaining = _pending.Count - _pendingCursor;

            // 必须置真，否则下一次 Tick 重入 EnterPhase 洗掉种子与待刷序列
            _phaseEntered = true;

            PublishPhase();

            PublishCountdownIfChanged(true);
        }

        public WaveSaveData ExportSave()
        {
            return new WaveSaveData
            {
                waveIndex = _waveIndex,
                cursor = _cursor,
                pendingCursor = _pendingCursor,
                phase = (int)_phase,
                timer = _timer,
                pendingSeed = (int)_pendingSeed,
                spawnTimer = _spawnTimer,
                isCompleted = _isCompleted,
                settleDelayStarted = _settleDelayStarted
            };
        }

        public SeedType ConsumeSeedGrant()
        {
            SeedType seed = _pendingSeed;

            _pendingSeed = SeedType.None;

            return seed;
        }

        public void Tick(
            float dt,
            bool anyEnemyAlive,
            List<SpawnRequest> output)
        {
            if (output == null) return;

            output.Clear();

            if (_isCompleted)
            {
                PublishCountdownIfChanged();
                return;
            }

            WaveSpec spec = Current;

            if (!_phaseEntered)
            {
                EnterPhase(WavePhase.Prep);
                _timer = DurationOf(_phase, spec, anyEnemyAlive);
            }

            _timer -= dt;

            if (_phase == WavePhase.Battle
                && _pendingCursor >= _pending.Count
                && !anyEnemyAlive)
                _timer = 0f;

            int transitions = 0;

            while (_timer <= 0f && transitions < MaxPhaseTransitionsPerTick)
            {
                if (!AdvancePhase(anyEnemyAlive)) break;

                transitions++;
                _timer += DurationOf(_phase, Current, anyEnemyAlive);
            }

            PublishCountdownIfChanged();

            if (_phase != WavePhase.Battle) return;

            SpawnDue(Current, dt, output);
        }

        private bool AdvancePhase(bool anyEnemyAlive)
        {
            switch (_phase)
            {
                case WavePhase.Prep:
                    EnterPhase(WavePhase.Battle);
                    return true;

                case WavePhase.Battle:
                    if (_pendingCursor < _pending.Count) return false;

                    _settleDelayStarted = !anyEnemyAlive;
                    EnterPhase(WavePhase.Settle);
                    return true;

                default:
                    if (anyEnemyAlive)
                    {
                        _settleDelayStarted = false;
                        return false;
                    }

                    if (!_settleDelayStarted)
                    {
                        _settleDelayStarted = true;
                        _timer = DurationOf(WavePhase.Settle, Current, anyEnemyAlive);
                        return false;
                    }

                    if (_playMode == WavePlayMode.Sequential && _cursor == _waves.Count - 1)
                    {
                        _isCompleted = true;
                        _timer = 0f;
                        return false;
                    }

                    _cursor = (_cursor + 1) % _waves.Count;
                    _waveIndex++;

                    EnterPhase(WavePhase.Prep);
                    return true;
            }
        }

        private float DurationOf(WavePhase phase, WaveSpec spec, bool anyEnemyAlive)
        {
            switch (phase)
            {
                case WavePhase.Prep: return Sanitize(spec.PrepTime);
                case WavePhase.Battle: return Sanitize(spec.BattleTime);
                default:
                    return _settleDelayStarted ? Sanitize(spec.SettleTime) : 0f;
            }
        }

        private void EnterPhase(WavePhase phase)
        {
            _phase = phase;
            _phaseEntered = true;

            if (phase == WavePhase.Prep)
            {
                WaveSpec prepSpec = Current;

                _pendingSeed = prepSpec != null ? prepSpec.GrantSeed : SeedType.None;

                _spawnTimer = 0f;
            }
            else if (phase == WavePhase.Battle)
            {
                FillPending(Current);
            }

            // 每个阶段都要播：HUD 文案与倒计时靠它刷新
            PublishPhase();
        }

        // <=0 的行跳过，不当成刷 0 只
        private void FillPending(WaveSpec spec)
        {
            _pending.Clear();

            IReadOnlyList<WaveEnemySpec> source = spec != null ? spec.Enemies : null;

            int sourceCount = source != null ? source.Count : 0;

            for (int i = 0; i < sourceCount; i++)
            {
                WaveEnemySpec row = source[i];

                if (row == null) continue;

                int count = row.Count;

                if (count <= 0) continue;

                for (int n = 0; n < count; n++) _pending.Add(row);
            }

            _pendingCursor = 0;
            _remaining = _pending.Count;
        }

        private void SpawnDue(
            WaveSpec spec,
            float dt,
            List<SpawnRequest> output)
        {
            float interval = spec.SpawnInterval > MinSpawnInterval ? spec.SpawnInterval : MinSpawnInterval;

            _spawnTimer -= dt;

            if (_spawnTimer > 0f) return;

            if (_pendingCursor >= _pending.Count) return;

            _spawnTimer = interval;

            int enemyId = _pending[_pendingCursor].EnemyId;

            _pendingCursor++;
            _remaining--;

            output.Add(new SpawnRequest(enemyId, _waveIndex, _remaining));
        }

        private static float Sanitize(float seconds)
        {
            return float.IsNaN(seconds) || seconds < 0f ? 0f : seconds;
        }

        private void PublishPhase()
        {
            EventBus<WavePhaseChanged>.Publish(new WavePhaseChanged(
                _waveIndex,
                _phase,
                Current?.GrantSeed ?? SeedType.None,
                PhaseRemaining));
        }

        private void PublishCountdownIfChanged(bool force = false)
        {
            int seconds = Mathf.CeilToInt(PhaseRemaining);
            if (!force
                && _hasPublishedCountdown
                && _publishedCountdownWaveIndex == _waveIndex
                && _publishedCountdownPhase == _phase
                && _publishedCountdownSeconds == seconds)
                return;

            _hasPublishedCountdown = true;
            _publishedCountdownWaveIndex = _waveIndex;
            _publishedCountdownPhase = _phase;
            _publishedCountdownSeconds = seconds;

            EventBus<WaveCountdownChanged>.Publish(
                new WaveCountdownChanged(_waveIndex, _phase, seconds));
        }

        public void Announce()
        {
            PublishPhase();
            PublishCountdownIfChanged(true);
        }
    }
}
