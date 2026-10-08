using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Wave
{
    /// <summary>波次状态机，只算何时刷/刷几只/刷在哪，不建物体；存活数由驱动方喂入，本类不认识敌人</summary>
    public sealed class WaveLogic
    {
        public readonly struct SpawnRequest
        {
            public readonly Vector2 Position;

            public readonly int WaveIndex;

            public readonly int Remaining;

            public SpawnRequest(Vector2 position, int waveIndex, int remaining)
            {
                Position = position;
                WaveIndex = waveIndex;
                Remaining = remaining;
            }
        }

        private readonly WaveSpec _spec;

        private int _waveIndex;

        private int _remaining;

        private float _timer;

        private bool _waitingForNextWave;

        public int WaveIndex => _waveIndex;

        public int Remaining => _remaining;

        public bool IsSpawning => _remaining > 0;

        public bool IsWaitingForNextWave => _waitingForNextWave;

        public WaveLogic(in WaveSpec spec)
        {
            _spec = spec;

            Reset();
        }

        public void Reset()
        {
            _waveIndex = 1;
            _remaining = _spec.EnemiesPerWave;
            _timer = _spec.InitialDelay;
            _waitingForNextWave = false;
        }

        /// <summary>推进一次，要生成的敌人追加到 output</summary>
        /// <remarks>now 单位秒。dt 单位秒，暂停时为 0。anyEnemyAlive 由驱动方数。</remarks>
        public void Tick(
            float now,
            float dt,
            bool anyEnemyAlive,
            Vector2 playerPosition,
            List<SpawnRequest> output)
        {
            if (output == null) return;

            output.Clear();

            if (_remaining <= 0)
            {
                if (anyEnemyAlive)
                {
                    _waitingForNextWave = false;
                    return;
                }

                if (!_waitingForNextWave)
                {
                    _waitingForNextWave = true;
                    _timer = _spec.RespawnDelay;
                    return;
                }

                _timer -= dt;

                if (_timer > 0f) return;

                _remaining = _spec.EnemiesPerWave;
                _waitingForNextWave = false;

                _waveIndex++;
            }

            _timer -= dt;

            if (_timer > 0f) return;

            _remaining--;

            output.Add(new SpawnRequest(SpawnPosition(now, playerPosition), _waveIndex, _remaining));

            _timer = _spec.SpawnInterval;
        }

        /// <summary>出生点，以玩家为圆心按波次错开；不判地形，越界由刚体撞墙兜住</summary>
        private Vector2 SpawnPosition(float now, Vector2 playerPosition)
        {
            float baseAngle = now * 0.7f + _waveIndex * 1.3f;

            float radians = baseAngle + _remaining * Mathf.PI * 0.5f;

            var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * _spec.SpawnRadius;

            return playerPosition + offset;
        }
    }
}
