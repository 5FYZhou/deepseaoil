using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using cfg.dso;

namespace DeepseaOil.Logic.Wave
{

    public enum WavePhase
    {

        Prep = 0,

        Battle = 1,

        Settle = 2,
    }

        // 波次状态机：只算何时刷、刷哪个怪种、刷几只，不建物体也不认识相机
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

        private readonly IReadOnlyList<WaveSpec> _waves;

        // 本波待刷序列：开战那刻按 count 展开
        private readonly List<WaveEnemySpec> _pending = new List<WaveEnemySpec>();

        private int _cursor;

        // 游标等于 Count 即本波刷完
        private int _pendingCursor;

        private int _waveIndex = 1;

        private int _remaining;

        private float _timer;

        private WavePhase _phase = WavePhase.Prep;

        private bool _phaseEntered;

        private SeedType _pendingSeed = SeedType.None;

        private float _spawnTimer;

        public int WaveIndex => _waveIndex;

        public int Remaining => _remaining;

        public WavePhase Phase => _phase;

        public float PhaseRemaining => _timer > 0f ? _timer : 0f;

        public bool IsSpawning => _phase == WavePhase.Battle && _pendingCursor < _pending.Count;

        public SeedType PendingSeedGrant => _pendingSeed;

        public string CurrentWaveName => Current != null ? Current.Name : string.Empty;

        public int WaveCount => _waves?.Count ?? 0;

        public WaveLogic(IReadOnlyList<WaveSpec> waves)
        {
            _waves = waves;
        }

        private WaveSpec Current
            => _waves == null || _waves.Count == 0 ? null : _waves[_cursor % _waves.Count];

        public void Reset()
        {
            _cursor = 0;
            _waveIndex = 1;
            _remaining = 0;
            _timer = 0f;
            _phase = WavePhase.Prep;
            _phaseEntered = false;
            _pendingSeed = SeedType.None;

            _pending.Clear();
            _pendingCursor = 0;
        }

        public SeedType ConsumeSeedGrant()
        {
            SeedType seed = _pendingSeed;

            _pendingSeed = SeedType.None;

            return seed;
        }

        // dt 暂停时为 0，节拍自然冻结
        public void Tick(
            float dt,
            bool anyEnemyAlive,
            List<SpawnRequest> output)
        {
            if (output == null) return;

            output.Clear();

            WaveSpec spec = Current;

            if (spec == null) return;

            if (!_phaseEntered)
            {
                EnterPhase(WavePhase.Prep);
                _timer = DurationOf(_phase, spec, anyEnemyAlive);
            }

            _timer -= dt;

            while (_timer <= 0f)
            {
                if (!AdvancePhase(spec, anyEnemyAlive)) return;

                _timer += DurationOf(_phase, spec, anyEnemyAlive);
            }

            if (_phase != WavePhase.Battle) return;

            SpawnDue(spec, dt, output);
        }

        private bool AdvancePhase(WaveSpec spec, bool anyEnemyAlive)
        {
            switch (_phase)
            {
                case WavePhase.Prep:
                    EnterPhase(WavePhase.Battle);
                    return true;

                case WavePhase.Battle:
                    EnterPhase(WavePhase.Settle);
                    return true;

                default:

                    if (anyEnemyAlive) return false;

                    _cursor++;
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

                    return anyEnemyAlive ? 0f : Sanitize(spec.SettleTime);
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
    }
}
